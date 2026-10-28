using System.Collections.Concurrent;
using System.Reflection;
using EAP.EC.KS_CM300;
using EAP.HC.Core;
using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
using EAP.RC.MES;
using Microsoft.Extensions.Options;
using Secs4Net;

var checks = new (string Name, Func<Task> Run)[]
{
    ("Reconnected HSMS traffic restores a stale disconnected equipment status", async () =>
    {
        const string eqpId = "reconnect-status-test";
        AppCache.Current.EqpInfos = new Dictionary<string, EqpInfo>
        {
            [eqpId] = new EqpInfo { EQPID = eqpId, ConnectStatus = ConnectStatus.DISCONNECTED }
        };

        var module = new KS_CM300();
        var args = new Arguments
        {
            Header = new Dictionary<string, object>
            {
                [CItems.EqpId] = eqpId,
                [CItems.ConnectionState] = ConnectionState.Communicating,
                [CItems.ConnectionVersion] = 10L,
                [CItems.ConnectionSessionId] = 2L
            }
        };
        await module.ConnectionChange(args).WaitAsync(TimeSpan.FromSeconds(2));
        Check(AppCache.Current.EqpInfos[eqpId].ConnectStatus == ConnectStatus.CONNECTED,
            "Communicating after reconnect did not clear stale DISCONNECTED status");

        args.Header[CItems.ConnectionState] = ConnectionState.Disconnected;
        args.Header[CItems.ConnectionVersion] = 9L;
        args.Header[CItems.ConnectionSessionId] = 1L;
        await module.ConnectionChange(args).WaitAsync(TimeSpan.FromSeconds(2));
        Check(AppCache.Current.EqpInfos[eqpId].ConnectStatus == ConnectStatus.CONNECTED,
            "A stale pre-reconnect disconnect overwrote the recovered status");
    }),
    ("SENDMAPRESULT XML retains the map fields", () =>
    {
        var request = new MQData
        {
            mQHeader = new MQHeader { MESSAGENAME = "SENDMAPRESULT" }
        };
        var body = XmlSerialization.SerializeOnlyBody(new MapResult
        {
            EQUIPMENTID = "EQP1",
            PORT = "2",
            MapStr = "1;2;3;"
        });
        var xml = XmlSerialization.XmlSerializeMessage(request, body);
        var decoded = XmlSerialization.DeserializeNodeObj<MapResult>(xml, "Request", "Body");
        Check(decoded != null && decoded.EQUIPMENTID == "EQP1" && decoded.PORT == "2"
            && decoded.MapStr == "1;2;3;", "SENDMAPRESULT body fields were lost in the MQ envelope");
        return Task.CompletedTask;
    }),
    ("REMOTE reply and request contents", async () =>
    {
        var result = await ControlStateQuery.ExecuteAsync((request, token) =>
        {
            Check(request.S == 1 && request.F == 3 && request.ReplyExpected, "Wrong query message");
            Check(request.SecsItem.Count == 1 && request.SecsItem[0].GetMemory<uint>().Span[0] == 29,
                "Wrong SVID");
            Check(token.CanBeCanceled, "Missing transport cancellation");
            return Task.FromResult(Reply(Item.L(Item.U1(5))));
        }, TimeSpan.FromSeconds(1));
        Check(result.Success && result.State == ControlState.REMOTE, "REMOTE not parsed");
    }),
    ("LOCAL remains a successful query", async () =>
    {
        var result = await Query(Reply(Item.L(Item.U1(4))));
        Check(result.Success && result.State == ControlState.LOCAL, "LOCAL mistaken for query failure");
    }),
    ("Invalid replies cannot grant REMOTE", async () =>
    {
        var replies = new[]
        {
            Reply(Item.L()), Reply(Item.L(Item.U1())), Reply(Item.L(Item.U1(99))),
            Reply(Item.L(Item.U1(5), Item.U1(5))), Reply(Item.L(Item.U1(5, 4))),
            Reply(Item.U1(5)), Reply(Item.L(Item.L(Item.U1(5)))),
            new SecsMessage(1, 0) { SecsItem = Item.L(Item.U1(5)) }
        };
        foreach (var reply in replies)
        {
            var result = await Query(reply);
            Check(result.Status == ControlStateQueryStatus.InvalidReply && !result.State.HasValue,
                $"Invalid S{reply.S}F{reply.F} granted a state");
        }
    }),
    ("No reply and transport exception retain failure details", async () =>
    {
        Check((await Query(null)).Status == ControlStateQueryStatus.NoReply, "NoReply lost");
        var error = new InvalidOperationException("test transport error");
        var result = await ControlStateQuery.ExecuteAsync((_, _) => throw error, TimeSpan.FromSeconds(1));
        Check(result.Status == ControlStateQueryStatus.Failed && result.Exception == error, "Exception lost");
    }),
    ("Deadline returns even if transport ignores cancellation", async () =>
    {
        var pending = new TaskCompletionSource<SecsMessage>();
        CancellationToken observed = default;
        var result = await ControlStateQuery.ExecuteAsync((_, token) =>
        {
            observed = token;
            return pending.Task;
        }, TimeSpan.FromMilliseconds(30)).WaitAsync(TimeSpan.FromSeconds(2));
        Check(result.Status == ControlStateQueryStatus.Timeout && observed.IsCancellationRequested,
            "Deadline did not cancel transport");
        pending.SetResult(Reply(Item.L(Item.U1(5))));
        Check(!result.State.HasValue, "Late reply changed timeout result");
    }),
    ("Cancellation before and during send", async () =>
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var sends = 0;
        var result = await ControlStateQuery.ExecuteAsync((_, _) =>
        {
            sends++;
            return Task.FromResult<SecsMessage>(null);
        }, TimeSpan.FromSeconds(1), cancelled.Token);
        Check(result.Status == ControlStateQueryStatus.Cancelled && sends == 0, "Cancelled query was sent");

        using var during = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var pending = new TaskCompletionSource<SecsMessage>();
        var task = ControlStateQuery.ExecuteAsync((_, _) =>
        {
            started.SetResult();
            return pending.Task;
        }, TimeSpan.FromSeconds(1), during.Token);
        await started.Task;
        during.Cancel();
        Check((await task.WaitAsync(TimeSpan.FromSeconds(2))).Status == ControlStateQueryStatus.Cancelled,
            "Caller cancellation became timeout");
    }),
    ("Transport cancellation removes only its own pending reply", async () =>
    {
        var (session, replies) = CreateSession();
        using var cancellation = new CancellationTokenSource();
        var first = AddPending(session, replies, 11, cancellation.Token);
        var second = AddPending(session, replies, 12);
        cancellation.Cancel();
        try
        {
            await first.WaitAsync(TimeSpan.FromSeconds(2));
            throw new Exception("Expected cancellation");
        }
        catch (OperationCanceledException) { }
        Check(!replies.ContainsKey(11) && replies.ContainsKey(12), "Cancellation removed another query");
        Invoke(session, "OnMessageReplyTimeout", (uint)12);
        Check(await second == null && replies.IsEmpty, "Timeout did not remove pending task");
    }),
    ("Watchdog cleans real transport pending entry without a native timeout callback", async () =>
    {
        var (session, replies) = CreateSession();
        var result = await ControlStateQuery.ExecuteAsync((_, token) => AddPending(session, replies, 31, token),
            TimeSpan.FromMilliseconds(30)).WaitAsync(TimeSpan.FromSeconds(2));
        Check(result.Status == ControlStateQueryStatus.Timeout, "Watchdog did not expire");
        Check(replies.IsEmpty, "Watchdog returned but leaked transport pending entry");
    }),
    ("Pre-cancelled transport send never calls native code", async () =>
    {
        var (session, replies) = CreateSession();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var send = (Task<SecsMessage>)session.GetType().GetMethod("SendAsync")
            .Invoke(session, new object[] { new SecsMessage(1, 3), cancellation.Token });
        try
        {
            await send;
            throw new Exception("Expected cancellation");
        }
        catch (OperationCanceledException) { }
        Check(replies.IsEmpty, "Pre-cancelled send registered a reply");
    }),
    ("Disconnect completes all pending replies", async () =>
    {
        var (session, replies) = CreateSession();
        var waits = Enumerable.Range(1, 100).Select(id => AddPending(session, replies, (uint)id)).ToArray();
        Invoke(session, "OnDisconnect");
        var results = await Task.WhenAll(waits).WaitAsync(TimeSpan.FromSeconds(2));
        Check(replies.IsEmpty && results.All(reply => reply == null), "Disconnect left pending tasks");
    }),
    ("Reply completes wait and cancellation cannot remove a later registration", async () =>
    {
        var (session, replies) = CreateSession();
        using var cancellation = new CancellationTokenSource();
        var wait = AddPending(session, replies, 21, cancellation.Token);
        Invoke(session, "OnReceiveMessage", (byte)1, (byte)4, new SecsLite.Model.SecsItem((byte)5), (uint)21, true);
        Check((await wait.WaitAsync(TimeSpan.FromSeconds(2)))?.F == 4, "Reply did not complete task");
        var later = new TaskCompletionSource<SecsMessage>();
        replies[21] = later;
        cancellation.Cancel();
        Check(replies.TryGetValue(21, out var registered) && registered == later,
            "Old cancellation removed a newer registration");
        Invoke(session, "OnMessageReplyTimeout", (uint)21);
    }),
    ("Reply before native send returns completes query", async () =>
    {
        var (session, replies) = CreateSession();
        var wait = SendTracked(session, () =>
        {
            ReceiveReply(session, 41);
            return 41;
        });
        Check((await wait.WaitAsync(TimeSpan.FromSeconds(2)))?.F == 4, "Early reply lost");
        Check(replies.IsEmpty && EarlyCount(session) == 0, "Early reply leaked entries");
    }),
    ("Timeout before registration completes query without reply", async () =>
    {
        var (session, replies) = CreateSession();
        var wait = SendTracked(session, () =>
        {
            Invoke(session, "OnMessageReplyTimeout", (uint)42);
            return 42;
        });
        Check(await wait.WaitAsync(TimeSpan.FromSeconds(2)) == null && replies.IsEmpty,
            "Early timeout lost");
    }),
    ("Early reply and timeout honor the first terminal result", async () =>
    {
        var (session, _) = CreateSession();
        var replyFirst = SendTracked(session, () =>
        {
            ReceiveReply(session, 43);
            Invoke(session, "OnMessageReplyTimeout", (uint)43);
            return 43;
        });
        Check((await replyFirst)?.F == 4, "Timeout overwrote early reply");
        var timeoutFirst = SendTracked(session, () =>
        {
            Invoke(session, "OnMessageReplyTimeout", (uint)44);
            ReceiveReply(session, 44);
            return 44;
        });
        Check(await timeoutFirst == null, "Late reply overwrote early timeout");
    }),
    ("Disconnect during native send leaves no registered task", async () =>
    {
        var (session, replies) = CreateSession();
        var wait = SendTracked(session, () =>
        {
            ReceiveReply(session, 45);
            Invoke(session, "OnDisconnect");
            return 45;
        });
        Check(await wait == null && replies.IsEmpty && EarlyCount(session) == 0, "Disconnected send registered task");
    }),
    ("Failed send and native -1 do not leak pending entries", async () =>
    {
        var (session, replies) = CreateSession();
        foreach (var id in new[] { 0u, uint.MaxValue })
        {
            Check(await SendTracked(session, () => id) == null, "Failed send waited for reply");
        }
        Check(await SendTracked(session, () => throw new InvalidOperationException("native failure")) == null,
            "Send exception did not produce failure");
        Check(replies.IsEmpty && EarlyCount(session) == 0, "Failed send leaked entries");
    }),
    ("Concurrent early replies are matched by message id", async () =>
    {
        var (session, replies) = CreateSession();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(() => SendTracked(session, () =>
        {
            started.SetResult();
            release.Task.GetAwaiter().GetResult();
            return 46;
        }));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<SecsMessage> second;
        try
        {
            second = SendTracked(session, () =>
            {
                ReceiveReply(session, 46, 4);
                ReceiveReply(session, 47, 5);
                return 47;
            });
        }
        finally { release.TrySetResult(); }
        var firstReply = await first.WaitAsync(TimeSpan.FromSeconds(2));
        var secondReply = await second.WaitAsync(TimeSpan.FromSeconds(2));
        Check(SecsItemUtil.GetItemValue(firstReply.SecsItem)?.ToString() == "4"
            && SecsItemUtil.GetItemValue(secondReply.SecsItem)?.ToString() == "5", "Concurrent replies crossed");
        Check(replies.IsEmpty && EarlyCount(session) == 0, "Concurrent sends leaked entries");
    }),
    ("Primary event with query id is delivered and never completes query", async () =>
    {
        var (session, replies) = CreateSession();
        var wait = SendTracked(session, () => 48);
        Invoke(session, "OnReceiveMessage", (byte)6, (byte)11, null, (uint)48, true);
        Check(!wait.IsCompleted && replies.ContainsKey(48), "Primary event completed query");
        var source = (IAsyncEnumerable<PrimaryMessageWrapper>)session.GetType().GetMethod("ReadPrimaryMessagesAsync")
            .Invoke(session, new object[] { CancellationToken.None });
        await using var reader = source.GetAsyncEnumerator();
        Check(await reader.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2))
            && reader.Current.PrimaryMessage.F == 11, "Primary event was lost");
        ReceiveReply(session, 48);
        Check((await wait)?.F == 4, "Actual reply did not complete query");
    }),
    ("Unmatched replies are bounded and cleared after send window", async () =>
    {
        var (session, _) = CreateSession();
        var wait = SendTracked(session, () =>
        {
            for (uint id = 1000; id < 2200; id++) ReceiveReply(session, id);
            Check(EarlyCount(session) <= 1024, "Unbounded early reply cache");
            return 49;
        });
        Check(EarlyCount(session) == 0, "Unmatched replies retained after send");
        ReceiveReply(session, 49);
        await wait.WaitAsync(TimeSpan.FromSeconds(2));
    }),
    ("Late reply cannot become a new primary message", async () =>
    {
        var (session, _) = CreateSession();
        var wait = SendTracked(session, () => 50);
        Invoke(session, "OnMessageReplyTimeout", (uint)50);
        await wait;
        ReceiveReply(session, 50);
        var source = (IAsyncEnumerable<PrimaryMessageWrapper>)session.GetType().GetMethod("ReadPrimaryMessagesAsync")
            .Invoke(session, new object[] { CancellationToken.None });
        using var cancellation = new CancellationTokenSource();
        await using var reader = source.GetAsyncEnumerator(cancellation.Token);
        var read = reader.MoveNextAsync().AsTask();
        cancellation.Cancel();
        try { await read; throw new Exception("Late secondary was forwarded as primary"); }
        catch (OperationCanceledException) { }
    }),
    ("Reflection dispatch waits through async completion and preserves NG", async () =>
    {
        var fixture = new DispatchFixture();
        var args = new Arguments();
        var task = MessageInvocation.InvokeAsync(fixture, nameof(DispatchFixture.WaitThenFail), args);
        Check(!task.IsCompleted && args.GetResultCode() == null, "Dispatch returned before await");
        fixture.Release.SetResult();
        await task;
        Check(args.GetResultCode() == "NG" && args.GetResultMsg() == "business rejected", "Dispatch lost failure");
    }),
    ("Reflection dispatch retains synchronous and asynchronous exceptions", async () =>
    {
        var fixture = new DispatchFixture();
        foreach (var method in new[] { nameof(DispatchFixture.ThrowSync), nameof(DispatchFixture.ThrowAsync) })
        {
            try { await MessageInvocation.InvokeAsync(fixture, method, new Arguments()); throw new Exception("Missing error"); }
            catch (InvalidOperationException ex) { Check(ex.Message == "original failure", "Original exception lost"); }
        }
    }),
    ("Async void and missing handlers fail before invocation", async () =>
    {
        var fixture = new DispatchFixture();
        try
        {
            await MessageInvocation.InvokeAsync(fixture, nameof(DispatchFixture.Unobservable), new Arguments());
            throw new Exception("Async void handler accepted");
        }
        catch (InvalidOperationException) { }
        Check(!fixture.VoidCalled, "async void invoked despite unobservable result");
        try { await MessageInvocation.InvokeAsync(fixture, "Missing", new Arguments()); throw new Exception("Missing method accepted"); }
        catch (MissingMethodException) { }
    }),
    ("Actual message handler awaits reply and preserves reply failure", async () =>
    {
        var (session, _) = CreateSession();
        EAPContext.contexts = new ConcurrentBag<ContextObject>(new[] { new ContextObject { EquipmentID = "test" } });
        var primary = (PrimaryMessageWrapper)Activator.CreateInstance(typeof(PrimaryMessageWrapper),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { session, new SecsMessage(1, 1), 51u }, null);
        // Simulate a rejected reply without loading native code; hold the lock to force a real async boundary.
        typeof(PrimaryMessageWrapper).GetField("<SecondaryMessage>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(primary, new SecsMessage(1, 2));
        var replyLock = (SemaphoreSlim)typeof(PrimaryMessageWrapper).GetField("_replyLock", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(primary);
        await replyLock.WaitAsync();
        var args = new Arguments { Header = new() { [CItems.EqpId] = "test" }, SecsData = primary };
        var module = new KS_CM300();
        var task = module.SecsMsgHandler(args);
        Check(!task.IsCompleted && args.GetResultCode() == null, "Outer handler marked success before reply");
        replyLock.Release();
        await task;
        Check(args.GetResultCode() == "NG" && args.GetResultMsg().Contains("S1F2"), "Outer handler overwrote NG");
    }),
    ("Production reflection dispatch loads module and preserves handler failure", async () =>
    {
        EAPContext.RootPath = AppContext.BaseDirectory;
        var args = new Arguments { Header = new() { [CItems.EqpId] = "missing" } };
        await SecsBase.ReciveSecsMessageAsync(new ContextObject { EquipmentID = "missing", EqpType = "KS_CM300" },
            args, "SecsMsgHandler");
        Check(args.GetResultCode() == "NG" && args.GetResultMsg().Contains("No SECS context"), "Dispatcher overwrote handler failure");
    }),
    ("Trace scope is isolated across concurrent awaits and nested steps", async () =>
    {
        var firstArgs = new Arguments();
        var secondArgs = new Arguments();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = SecsExecution.RunAsync(firstArgs, "first", async () =>
        {
            Check(SecsExecution.CurrentTraceId == firstArgs.TraceId, "Wrong initial first trace");
            await gate.Task;
            Check(SecsExecution.CurrentTraceId == firstArgs.TraceId, "First trace crossed awaits");
            await SecsExecution.RunAsync(secondArgs, "nested", async () =>
            {
                await Task.Yield();
                Check(SecsExecution.CurrentTraceId == secondArgs.TraceId, "Nested trace not installed");
            });
            Check(SecsExecution.CurrentTraceId == firstArgs.TraceId, "Nested trace not restored");
        });
        var second = SecsExecution.RunAsync(secondArgs, "second", async () =>
        {
            await Task.Yield();
            Check(SecsExecution.CurrentTraceId == secondArgs.TraceId, "Parallel trace crossed");
            gate.SetResult();
        });
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Check(SecsExecution.CurrentTraceId == null, "Trace leaked to caller");
    }),
    ("Channel duplicate starts create only one run and stop cancels delay", async () =>
    {
        var worker = new ChannelPollingTask();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;
        Exception failure = null;
        Func<CancellationToken, Task> poll = async token =>
        {
            Interlocked.Increment(ref runs);
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        var starts = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => worker.Start(poll, ex => failure = ex))));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(starts.Count(started => started) == 1 && runs == 1 && worker.IsRunning,
            "Duplicate channel runs created");
        await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Check(!worker.IsRunning && failure == null, "Cancelled polling did not exit normally");
        await worker.StopAsync();
    }),
    ("Channel restart waits for old run and old cleanup cannot stop new run", async () =>
    {
        var worker = new ChannelPollingTask();
        var oldEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failures = new ConcurrentQueue<Exception>();
        worker.Start(async token =>
        {
            oldEntered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await oldExit.Task; }
        }, failures.Enqueue);
        await oldEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stopped = worker.StopAsync();
        Check(!worker.IsRunning && !stopped.IsCompleted, "Stop did not track old task");
        Check(worker.Start(async token =>
        {
            nextEntered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, failures.Enqueue), "Restart rejected after cancellation");
        Check(!nextEntered.Task.IsCompleted, "New polling overlapped old run");
        oldExit.SetResult();
        await stopped.WaitAsync(TimeSpan.FromSeconds(2));
        await nextEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(worker.IsRunning, "Old finally cleared new run");
        await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Check(failures.IsEmpty, "Cancellation reported as polling failure");
    }),
    ("Channel exits and errors clear running state and allow restart", async () =>
    {
        var worker = new ChannelPollingTask();
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new InvalidOperationException("poll failure");
        worker.Start(_ => throw error, ex => observed.SetResult(ex));
        Check(await observed.Task.WaitAsync(TimeSpan.FromSeconds(2)) == error, "Background exception lost");
        await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Check(!worker.IsRunning, "Faulted channel still marked running");
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Check(worker.Start(_ => { exited.SetResult(); return Task.CompletedTask; }, _ => { }), "Restart rejected");
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Check(!worker.IsRunning, "Natural exit still marked running");
    }),
    ("Channel stop cancels an actual SVID29 pending reply and removes it", async () =>
    {
        var worker = new ChannelPollingTask();
        var (session, replies) = CreateSession();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<ControlStateQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.Start(async token =>
        {
            var query = ControlStateQuery.ExecuteAsync((_, transportToken) =>
            {
                var reply = AddPending(session, replies, 61, transportToken);
                entered.SetResult();
                return reply;
            }, TimeSpan.FromSeconds(45), token);
            result.SetResult(await query);
        }, ex => result.SetException(ex));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Check((await result.Task).Status == ControlStateQueryStatus.Cancelled && replies.IsEmpty,
            "Channel stop left a SVID29 waiter registered");
    }),
    ("Production stop event waits for channel cleanup and stop-all cancels all channels", async () =>
    {
        var module = new KS_CM300();
        var map = (Dictionary<int, ChannelContext>)typeof(KS_CM300)
            .GetField("_channelMap", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartChannel(map[20900], async token =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await exit.Task; }
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stop = (Task)typeof(KS_CM300).GetMethod("HandleChannelCEIDAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(module, new object[] { 20901, new Arguments() });
        Check(!stop.IsCompleted && !map[20900].IsRunning, "Stop event returned before task cleanup");
        exit.SetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(2));
        foreach (var channel in map.Values)
            StartChannel(channel, token => Task.Delay(Timeout.Infinite, token));
        typeof(KS_CM300).GetMethod("StopChannelPolling", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(module, null);
        await Task.WhenAll(map.Values.Select(StopChannel)).WaitAsync(TimeSpan.FromSeconds(2));
        Check(map.Values.All(channel => !channel.IsRunning), "Stop-all left channels running");
    }),
    ("Overlapping SVID29 callers share one transport query and query id", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        var (session, replies) = CreateSession();
        var sends = 0;
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joined = 0;
        var allJoined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ids = new ConcurrentBag<string>();
        var tasks = Enumerable.Range(0, 24).Select(_ => Task.Run(() => coordinator.RunAsync("eqp", 0,
            (id, token) =>
            {
                Interlocked.Increment(ref sends);
                return ControlStateQuery.ExecuteAsync((_, transportToken) =>
                    {
                        var reply = AddPending(session, replies, 70, transportToken);
                        registered.SetResult();
                        return reply;
                    },
                    TimeSpan.FromSeconds(10), token, id);
            }, joined: (id, shared) =>
            {
                ids.Add(id);
                if (Interlocked.Increment(ref joined) == 24) allJoined.SetResult();
            }))).ToArray();
        await allJoined.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(sends == 1 && ids.Distinct().Count() == 1, "Concurrent callers sent duplicate SVID29 queries");
        var replyItem = new SecsLite.Model.SecsItem(SecsLite.SecsFormat.List);
        replyItem.ItemList.Add(new SecsLite.Model.SecsItem((byte)5));
        Invoke(session, "OnReceiveMessage", (byte)1, (byte)4, replyItem, 70u, true);
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(2));
        Check(results.All(result => result.Success && result.State == ControlState.REMOTE) && replies.IsEmpty,
            "Shared reply not delivered to all callers");
    }),
    ("Completed SVID29 results are never reused and equipment queries are isolated", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        var sends = 0;
        var remote = new ControlStateQueryResult(ControlStateQueryStatus.Success, ControlState.REMOTE);
        for (var query = 0; query < 3; query++)
            await coordinator.RunAsync("first", 0, (_, _) => { sends++; return Task.FromResult(remote); });
        Check(sends == 3, "Completed REMOTE result cached");
        var pending = new TaskCompletionSource<ControlStateQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = coordinator.RunAsync("first", 0, (_, _) => pending.Task);
        var second = await coordinator.RunAsync("second", 0,
            (_, _) => Task.FromResult(new ControlStateQueryResult(ControlStateQueryStatus.Success, ControlState.LOCAL)));
        Check(second.State == ControlState.LOCAL && !first.IsCompleted, "Different equipment shared a query");
        pending.SetResult(remote);
        await first;
    }),
    ("Cancelling one SVID29 caller leaves the other caller and transport active", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<ControlStateQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken transport = default;
        var first = coordinator.RunAsync("eqp", 0, (_, token) => { transport = token; return pending.Task; }, cancellation.Token);
        var second = coordinator.RunAsync("eqp", 0, (_, _) => throw new Exception("Duplicate refresh"));
        cancellation.Cancel();
        try { await first; throw new Exception("Caller cancellation ignored"); }
        catch (OperationCanceledException) { }
        Check(!transport.IsCancellationRequested && !second.IsCompleted, "One caller cancelled shared transport");
        pending.SetResult(new(ControlStateQueryStatus.Success, ControlState.REMOTE));
        Check((await second).Success, "Remaining caller lost reply");
    }),
    ("Final SVID29 waiter cancellation cancels transport and old cleanup preserves a new query", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        using var cancellation = new CancellationTokenSource();
        var oldPending = new TaskCompletionSource<ControlStateQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextPending = new TaskCompletionSource<ControlStateQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken oldTransport = default;
        var old = coordinator.RunAsync("eqp", 0, (_, token) => { oldTransport = token; return oldPending.Task; }, cancellation.Token);
        cancellation.Cancel();
        try { await old; throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Check(oldTransport.IsCancellationRequested, "Final waiter did not cancel transport");
        var next = coordinator.RunAsync("eqp", 0, (_, _) => nextPending.Task);
        oldPending.SetResult(new(ControlStateQueryStatus.NoReply));
        var last = coordinator.RunAsync("eqp", 0, (_, _) => throw new Exception("Old cleanup removed next query"));
        nextPending.SetResult(new(ControlStateQueryStatus.Success, ControlState.LOCAL));
        Check((await next).State == ControlState.LOCAL && (await last).State == ControlState.LOCAL, "Next query lost");
    }),
    ("Invalidating shared SVID29 cancels actual pending reply and permits a fresh query", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        var (session, replies) = CreateSession();
        var first = coordinator.RunAsync("eqp", 0, (id, token) => ControlStateQuery.ExecuteAsync(
            (_, transportToken) => AddPending(session, replies, 71, transportToken), TimeSpan.FromSeconds(45), token, id));
        var second = coordinator.RunAsync("eqp", 0, (_, _) => throw new Exception("Duplicate refresh"));
        coordinator.Invalidate("eqp");
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Check(results.All(result => result.Status == ControlStateQueryStatus.Cancelled) && replies.IsEmpty,
            "Invalidation left transport wait or old result active");
        var next = await coordinator.RunAsync("eqp", 1,
            (_, _) => Task.FromResult(new ControlStateQueryResult(ControlStateQueryStatus.Success, ControlState.LOCAL)));
        Check(next.State == ControlState.LOCAL, "New generation reused invalidated query");
    }),
    ("Last shared caller cancellation immediately removes the managed transport waiter", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        var (session, replies) = CreateSession();
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var first = coordinator.RunAsync("eqp", 0, (id, token) => ControlStateQuery.ExecuteAsync(
            (_, transportToken) => AddPending(session, replies, 72, transportToken), TimeSpan.FromSeconds(45), token, id),
            firstCancellation.Token);
        var second = coordinator.RunAsync("eqp", 0, (_, _) => throw new Exception("Duplicate refresh"), secondCancellation.Token);
        firstCancellation.Cancel();
        try { await first; throw new Exception("First cancellation ignored"); }
        catch (OperationCanceledException) { }
        Check(replies.Count == 1, "First cancellation removed remaining caller's reply waiter");
        secondCancellation.Cancel();
        try { await second; throw new Exception("Last cancellation ignored"); }
        catch (OperationCanceledException) { }
        Check(replies.IsEmpty, "Last cancellation returned before transport cleanup");
    }),
    ("Rapid channel starts and stops never overlap polling bodies", async () =>
    {
        var worker = new ChannelPollingTask();
        var active = 0;
        var overlaps = 0;
        var failures = new ConcurrentQueue<Exception>();
        Func<CancellationToken, Task> poll = async token =>
        {
            if (Interlocked.Increment(ref active) > 1) Interlocked.Increment(ref overlaps);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Interlocked.Decrement(ref active); }
        };
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            for (var cycle = 0; cycle < 12; cycle++)
            {
                worker.Start(poll, failures.Enqueue);
                await Task.Yield();
                await worker.StopAsync();
            }
        }))).WaitAsync(TimeSpan.FromSeconds(3));
        await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Check(active == 0 && overlaps == 0 && failures.IsEmpty && !worker.IsRunning, "Channel runs overlapped or leaked");
    }),
    ("New connection generation starts a separate query and shared failures allow retry", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        var oldPending = new TaskCompletionSource<ControlStateQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = coordinator.RunAsync("eqp", 0, (_, _) => oldPending.Task);
        var changed = await coordinator.RunAsync("eqp", 1,
            (_, _) => Task.FromResult(new ControlStateQueryResult(ControlStateQueryStatus.Success, ControlState.LOCAL)));
        Check(changed.State == ControlState.LOCAL && !old.IsCompleted, "Generation reused old query");
        oldPending.SetResult(new(ControlStateQueryStatus.NoReply));
        await old;
        var error = new InvalidOperationException("refresh failed");
        var failed = await coordinator.RunAsync("eqp", 1, (_, _) => throw error);
        Check(failed.Status == ControlStateQueryStatus.Failed && failed.Exception == error, "Shared failure lost");
        Check((await coordinator.RunAsync("eqp", 1,
            (_, _) => Task.FromResult(new ControlStateQueryResult(ControlStateQueryStatus.Success, ControlState.REMOTE)))).Success,
            "Failure prevented retry");
    }),
    ("Pre-cancelled shared query never sends", async () =>
    {
        var coordinator = new ControlStateQueryCoordinator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var called = false;
        try
        {
            await coordinator.RunAsync("eqp", 0, (_, _) => { called = true; throw new Exception("Sent after stop"); }, cancellation.Token);
            throw new Exception("Pre-cancelled query accepted");
        }
        catch (OperationCanceledException) { }
        Check(!called, "Cancelled query sent");
    }),
    ("MES lot reply serializes database PORTID and client deserializes it", async () =>
    {
        var reply = new EAPManage.Models.MQ.MESLotInfoDownloadReply
        {
            EQUIPMENTID = "eqp",
            CARRIERID = "carrier",
            PORTID = "2",
            BATCHCHID = "batch",
            LOTLIST = new()
            {
                new EAPManage.Models.MQ.Lot
                {
                    LOT_ID = "lot",
                    QTY = 1,
                    WAFERS = new() { new EAPManage.Models.MQ.WaferInfo { WAFER_ID = "wafer", SLOT = 1 } }
                }
            }
        };
        var body = XmlSerialization.SerializeOnlyBody(reply);
        Check(body.Contains("<PORTID>2</PORTID>", StringComparison.Ordinal), "Server omitted PORTID from XML");
        var response = $"<Response><Header><RESULT>SUCCESS</RESULT></Header>{body}</Response>";
        var parsed = XmlSerialization.DeserializeNodeObj<EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply>(
            response, "Response", "Body");
        Check(parsed?.PORTID == "2" && parsed.BATCHCHID == "batch", "Client did not deserialize PORTID");
        Check(parsed.LOTLIST?.SingleOrDefault()?.WAFERS?.SingleOrDefault()?.SLOT == 1,
            "Adding PORTID broke existing lot payload parsing");
        await Task.CompletedTask;
    }),
    ("MES cache port lookup finds replies identified by port", async () =>
    {
        AppCache.Current.MesBatchInfos = new Dictionary<string, EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply>
        {
            ["batch"] = new() { BATCHCHID = "batch", CARRIERID = "different", PORTID = "2" }
        };
        var find = typeof(MESClient).GetMethod("FindBatchInfo", BindingFlags.Static | BindingFlags.NonPublic);
        var found = (EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply)find.Invoke(null, new object[] { "missing", "2" });
        Check(found?.BATCHCHID == "batch", "Port fallback did not find batch");
        found = (EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply)find.Invoke(null, new object[] { "missing", "LP2" });
        Check(found?.BATCHCHID == "batch", "LP-prefixed port did not match numeric PORTID");
        var stale = new EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply { BATCHCHID = "stale", CARRIERID = "carrier" };
        AppCache.Current.MesBatchInfos["stale"] = stale;
        found = (EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply)find.Invoke(null, new object[] { "carrier", "LP3" });
        Check(found == stale && stale.PORTID == "3", "Existing null-PORTID cache entry was not repaired");
        await Task.CompletedTask;
    }),
    ("Carrier with multiple batches selects the requested port first", async () =>
    {
        AppCache.Current.MesBatchInfos = new Dictionary<string, EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply>
        {
            ["port1"] = new() { BATCHCHID = "port1", CARRIERID = "carrier", PORTID = "1" },
            ["port2"] = new() { BATCHCHID = "port2", CARRIERID = "carrier", PORTID = "2" }
        };
        var find = typeof(MESClient).GetMethod("FindBatchInfo", BindingFlags.Static | BindingFlags.NonPublic);
        var found = (EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply)find.Invoke(null, new object[] { "carrier", "LP2" });
        Check(found?.BATCHCHID == "port2", "Carrier match selected a batch on another port");
        await Task.CompletedTask;
    }),
    ("Legacy MES replies receive the request port fallback", async () =>
    {
        var legacyReply = new EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply { BATCHCHID = "legacy" };
        var normalize = typeof(EAP.RC.MES.MESClient).GetMethod("NormalizeLotInfoDownloadReply",
            BindingFlags.Static | BindingFlags.NonPublic);
        var normalized = (EAP.HC.Core.Entities.MES.MESLotInfoDownloadReply)
            normalize.Invoke(null, new object[] { legacyReply, " LP2 " });
        Check(normalized.PORTID == "LP2", "Legacy reply did not retain requested port");

        legacyReply.PORTID = " LP3 ";
        normalize.Invoke(null, new object[] { legacyReply, "2" });
        Check(legacyReply.PORTID == "LP3", "Server PORTID was overwritten by request fallback");
        await Task.CompletedTask;
    }),
    ("Typed step preserves returned value and reports thrown failure", async () =>
    {
        var args = new Arguments();
        Check(await SecsExecution.RunAsync(args, "typed", () => Task.FromResult(42)) == 42, "Typed step lost value");
        try
        {
            await SecsExecution.RunAsync(args, "throw", async () => { await Task.Yield(); throw new InvalidOperationException("step failure"); });
            throw new Exception("Step exception swallowed");
        }
        catch (InvalidOperationException) { }
        Check(args.GetResultCode() == "NG" && args.GetResultMsg() == "step failure", "Step lost failure");
    })
};

foreach (var (name, run) in checks)
{
    try
    {
        await run().WaitAsync(TimeSpan.FromSeconds(10));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        Environment.ExitCode = 1;
        return;
    }
    Console.WriteLine($"PASS {name}");
}
Console.WriteLine($"Passed {checks.Length} regression scenarios.");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static object ChannelWorker(ChannelContext channel) => typeof(ChannelContext)
    .GetProperty("Worker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(channel);

static void StartChannel(ChannelContext channel, Func<CancellationToken, Task> poll)
{
    var worker = ChannelWorker(channel);
    worker.GetType().GetMethod("Start").Invoke(worker, new object[] { poll, (Action<Exception>)(_ => { }) });
}

static Task StopChannel(ChannelContext channel)
{
    var worker = ChannelWorker(channel);
    return (Task)worker.GetType().GetMethod("StopAsync").Invoke(worker, null);
}

static SecsMessage Reply(Item item) => new(1, 4) { SecsItem = item };
static Task<ControlStateQueryResult> Query(SecsMessage reply) =>
    ControlStateQuery.ExecuteAsync((_, _) => Task.FromResult(reply), TimeSpan.FromSeconds(1));

// Exercise the real managed transport callbacks without connecting to equipment or loading the native DLL.
static (object Session, ConcurrentDictionary<uint, TaskCompletionSource<SecsMessage>> Replies) CreateSession()
{
    var connection = new HsmsConnection(Options.Create(new SecsGemOptions { AutoReconnect = false }), null);
    var session = typeof(HsmsConnection).GetProperty("Session", BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(connection);
    var replies = (ConcurrentDictionary<uint, TaskCompletionSource<SecsMessage>>)session.GetType()
        .GetField("_pendingReplies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
    return (session, replies);
}

static Task<SecsMessage> AddPending(object session,
    ConcurrentDictionary<uint, TaskCompletionSource<SecsMessage>> replies, uint id,
    CancellationToken token = default)
{
    var completion = new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
    replies[id] = completion;
    return (Task<SecsMessage>)session.GetType().GetMethod("WaitForReplyAsync", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(session, new object[] { id, completion, token });
}

static void Invoke(object session, string method, params object[] arguments) =>
    session.GetType().GetMethod(method).Invoke(session, arguments);

static Task<SecsMessage> SendTracked(object session, Func<uint> send) =>
    (Task<SecsMessage>)session.GetType().GetMethod("SendWithReplyTracking", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(session, new object[] { new SecsMessage(1, 3), CancellationToken.None, send });

static void ReceiveReply(object session, uint id, byte state = 5) =>
    Invoke(session, "OnReceiveMessage", (byte)1, (byte)4, new SecsLite.Model.SecsItem(state), id, true);

static int EarlyCount(object session) =>
    ((System.Collections.IDictionary)session.GetType().GetField("_earlyReplies", BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(session)).Count;

public sealed class DispatchFixture
{
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool VoidCalled { get; private set; }
    public async Task WaitThenFail(Arguments args)
    {
        await Release.Task;
        args.SetResultNG("business rejected");
    }
    public void ThrowSync(Arguments args) => throw new InvalidOperationException("original failure");
    public async Task ThrowAsync(Arguments args)
    {
        await Task.Yield();
        throw new InvalidOperationException("original failure");
    }
    public async void Unobservable(Arguments args)
    {
        VoidCalled = true;
        await Task.Yield();
    }
}
