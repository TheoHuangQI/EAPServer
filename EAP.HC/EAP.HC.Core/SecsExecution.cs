using System.Diagnostics;
using System.Runtime.CompilerServices;
using EAPManage.Utils;
using Secs4Net;

namespace EAP.HC.Core;

public static class SecsExecution
{
    private static readonly AsyncLocal<string> Trace = new();
    public static string CurrentTraceId => Trace.Value;

    public static async Task<T> RunAsync<T>(Arguments args, string step, Func<Task<T>> action)
    {
        T result = default;
        await RunAsync(args, step, async () => { result = await action().ConfigureAwait(false); })
            .ConfigureAwait(false);
        return result;
    }

    public static Task<SecsMessage> SendAsync(Arguments args, SecsGem transport, SecsMessage message,
        [CallerMemberName] string source = null) =>
        RunAsync(args, $"{source}/S{message.S}F{message.F}", () => transport.SendAsync(message));

    public static Task<SecsMessage> SendAsync(Arguments args, SecsGem transport, SecsMessage message,
        CancellationToken cancellationToken, [CallerMemberName] string source = null) =>
        RunAsync(args, $"{source}/S{message.S}F{message.F}", () => transport.SendAsync(message, cancellationToken));

    public static Task<bool> ReplyAsync(Arguments args, SecsMessage message,
        [CallerMemberName] string source = null) =>
        RunAsync(args, $"{source}/ReplyS{message.S}F{message.F}", async () =>
        {
            var sent = await args.SecsData.TryReplyAsync(message).ConfigureAwait(false);
            if (!sent)
                args.SetResultNG($"S{message.S}F{message.F} reply was not sent.");
            return sent;
        });

    public static async Task RunAsync(Arguments args, string step, Func<Task> action)
    {
        var previousTrace = Trace.Value;
        Trace.Value = args.TraceId;
        var sw = Stopwatch.StartNew();
        var eqpId = args.Header?.TryGetValue(CItems.EqpId, out var id) == true ? id?.ToString() : "";
        var identity = $"TraceId={args.TraceId} EqpId={eqpId} Step={step} MessageId={args.SecsData?.Id}";
        Exception failure = null;
        try
        {
            Logger.Inst().Info($"SECS Step Begin {identity}");
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            args.SetResultNG("Step cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            failure = ex;
            args.SetResultNG(ex.Message);
            throw;
        }
        finally
        {
            Trace.Value = previousTrace;
            var log = $"SECS Step End {identity} ElapsedMs={sw.ElapsedMilliseconds} "
                + $"Result={args.GetResultCode() ?? "Completed"} Detail={args.GetResultMsg()}";
            if (failure != null)
                Logger.Inst().Error($"{log} Exception={failure}");
            else if (args.GetResultCode() == "NG")
                Logger.Inst().Warn(log);
            else
                Logger.Inst().Info(log);
        }
    }
}
