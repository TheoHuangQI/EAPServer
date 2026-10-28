using Apache.NMS.ActiveMQ;
using Apache.NMS;
using EAP.HC.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
using EAP.RC.MES;
using System.Net;
using Apache.NMS.ActiveMQ.Util.Synchronization;
using System.Collections.Concurrent;

namespace EAPServer
{
    public class StartUp
    {

      //  protected static FALoggerClass CimLogger;
        protected static Object obj = new Object();
   
        private static ISecsGemLogger _logger = new SecsLogger(obj);
        private static readonly ConcurrentDictionary<string, DeviceRuntime> DeviceRuntimes =
            new ConcurrentDictionary<string, DeviceRuntime>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> DeviceOperationLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        private static readonly object ConfigurationLock = new object();
        private static readonly object EquipmentInfoLock = new object();
        private static readonly object ContextCollectionLock = new object();

        public StartUp()
        {
           // CimLogger = FALoggerClass.Inst();
        }


        /// <summary>
        /// 初始化加载
        /// </summary>
        public static void InitLoad(IConfiguration config)
        {
            try
            {
                EAPContext.Configuration = config;
                EAPContext.ServerName = config.GetSection("EAPNo").Value.ToString();
                EAPContext.DeviceConnectionControlHandler = ControlEquipmentAsync;
                ActiveMQClient activeMQClient = ActiveMQClient.Current;
                // 注册事件
                activeMQClient.ConnectionInterrupted += () => Console.WriteLine("Connection lost");
                activeMQClient.ConnectionRestored += () => Console.WriteLine("Connection restored");
                activeMQClient.PermanentFailure += () => Console.WriteLine("Permanent failure");
                // 使用消息类型过滤器
                activeMQClient.MessageReceived += (s, e) =>
                {
                    switch (e.Message)
                    {
                        case ITextMessage textMsg:
                            MessageProcessor.HandleMessage(textMsg, e);
                            break;
                        case IMapMessage mapMsg:
                            MessageProcessor.HandleMessage(mapMsg, e);
                            break;
                        case IBytesMessage bytesMsg:
                            MessageProcessor.HandleMessage(bytesMsg, e);
                            break;
                    }
                };
                activeMQClient.InitializeAsync().GetAsyncResult();
                InitializeEAP();
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
               // CimLogger.Error($"EAPServer.InitLoad Error:{ex.Message},{ex.StackTrace}");
            }
        }
        
        private static void InitializeEAP()
        {
            try
            {
                AppCache.Current.InitializeObjectCache();
                bool res = EAPContext.Initialization();
                if (res)
                {
                    foreach (var eqpconfig in EAPContext.EqpConfigures.ToList())
                    {
                        if (!eqpconfig.Enabled)
                            continue;

                        try
                        {
                            var deviceLock = DeviceOperationLocks.GetOrAdd(eqpconfig.Name, _ => new SemaphoreSlim(1, 1));
                            deviceLock.Wait();
                            try
                            {
                                StartDevice(eqpconfig, initializeEquipmentState: true);
                            }
                            finally
                            {
                                deviceLock.Release();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Failed to start device {eqpconfig.Name}: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static async Task<EquipmentConnectionControl> ControlEquipmentAsync(EquipmentConnectionControl request)
        {
            var result = new EquipmentConnectionControl
            {
                EQUIPMENTID = request?.EQUIPMENTID ?? string.Empty,
                ENABLE = request?.ENABLE ?? 0
            };

            if (request == null || string.IsNullOrWhiteSpace(request.EQUIPMENTID) ||
                (request.ENABLE != 0 && request.ENABLE != 1))
            {
                result.MESSAGE = "Invalid equipment connection request.";
                return result;
            }

            var operationLock = DeviceOperationLocks.GetOrAdd(request.EQUIPMENTID, _ => new SemaphoreSlim(1, 1));
            await operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (request.ENABLE == 0)
                {
                    await StopDeviceAsync(request.EQUIPMENTID).ConfigureAwait(false);
                    var existingConfig = EAPContext.EqpConfigures?.FirstOrDefault(item =>
                        string.Equals(item.Name, request.EQUIPMENTID, StringComparison.OrdinalIgnoreCase));
                    if (existingConfig != null)
                        existingConfig.Enabled = false;

                    result.SUCCESS = true;
                    result.MESSAGE = "Device connection stopped.";
                    return result;
                }

                // Validate the latest on-disk JSON before stopping an existing connection.
                var wasConfigured = EAPContext.EqpConfigures?.Any(item =>
                    string.Equals(item.Name, request.EQUIPMENTID, StringComparison.OrdinalIgnoreCase)) == true;
                var latestConfig = EAPContext.LoadConnectionConfigure(request.EQUIPMENTID);
                latestConfig.Enabled = true;
                await StopDeviceAsync(request.EQUIPMENTID).ConfigureAwait(false);
                UpdateInMemoryConfiguration(latestConfig);
                StartDevice(latestConfig, initializeEquipmentState: !wasConfigured);
                result.SUCCESS = true;
                result.MESSAGE = "Latest JSON configuration loaded and device connection restarted.";
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Device connection control failed for {request.EQUIPMENTID}: {ex}");
                result.MESSAGE = ex.Message;
                return result;
            }
            finally
            {
                operationLock.Release();
            }
        }

        private static void UpdateInMemoryConfiguration(ConnectionConfigure config)
        {
            lock (ConfigurationLock)
            {
                var configurations = EAPContext.EqpConfigures?.ToList() ?? new List<ConnectionConfigure>();
                var existing = configurations.FirstOrDefault(item =>
                    string.Equals(item.Name, config.Name, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                    configurations.Add(config);
                else
                {
                    var index = configurations.IndexOf(existing);
                    configurations[index] = config;
                }
                EAPContext.EqpConfigures = configurations;
            }

            lock (EquipmentInfoLock)
            {
                var equipmentInfo = AppCache.Current.EqpInfos ?? new Dictionary<string, EqpInfo>();
                if (!equipmentInfo.TryGetValue(config.Name, out var info))
                {
                    info = new EqpInfo
                    {
                        IsInitial = false,
                        EQPID = config.Name,
                        EqpStatus = EQPStatus.IDLE,
                        ControlStatus = ControlState.EQPOFFLINE,
                        ConnectStatus = ConnectStatus.DISCONNECTED,
                        MESConnectStatus = MESConnectStatus.DISCONNECTED,
                        HsmsConnectionState = "Disconnected",
                        HsmsRetryCount = 0,
                        HsmsLastError = string.Empty,
                        HsmsDisconnectReason = string.Empty,
                        PortInfo = new Dictionary<int, PortInfo>()
                    };
                    equipmentInfo = new Dictionary<string, EqpInfo>(equipmentInfo)
                    {
                        [config.Name] = info
                    };
                    AppCache.Current.EqpInfos = equipmentInfo;
                }

                info.IsInitial = false;
                info.EQPType = config.EQPType;
                info.Chamber = config.Chamber;
                info.LoadPort = config.LoadPort;
                info.PortInfo ??= new Dictionary<int, PortInfo>();
                foreach (var obsoletePort in info.PortInfo.Keys.Where(portNo => portNo > config.LoadPort).ToList())
                    info.PortInfo.Remove(obsoletePort);
                for (var portNo = 1; portNo <= config.LoadPort; portNo++)
                {
                    if (!info.PortInfo.ContainsKey(portNo))
                    {
                        info.PortInfo.Add(portNo, new PortInfo
                        {
                            PortNo = portNo,
                            CarrierId = string.Empty,
                            PortStatus = PortStatus.Idle,
                            BatchId = string.Empty,
                            LotIds = new List<string>()
                        });
                    }
                }
                info.EQPType = config.EQPType;
                info.Chamber = config.Chamber;
                info.LoadPort = config.LoadPort;
                info.HsmsRemoteAddress = config.ConnectionMode == ConnectionMode.ACTIVE
                    ? $"{config.RemoteIP}:{config.RemotePort}"
                    : $"{config.LocalIP}:{config.LocalPort}";
                info.HsmsAutoReconnect = config.AutoReconnect;
            }
        }

        private static void StartDevice(ConnectionConfigure config, bool initializeEquipmentState = false)
        {
            UpdateInMemoryConfiguration(config);
            var isActive = config.ConnectionMode == ConnectionMode.ACTIVE;
            var options = Options.Create(new SecsGemOptions
            {
                IsActive = isActive,
                IpAddress = isActive ? config.RemoteIP : config.LocalIP,
                Port = isActive ? config.RemotePort : config.LocalPort,
                LocalIpAddress = config.LocalIP,
                LocalPort = config.LocalPort,
                SocketReceiveBufferSize = 65535,
                DeviceId = (ushort)config.DeviceID,
                T3 = config.T3 * 1000,
                T5 = config.T5 * 1000,
                T6 = config.T6 * 1000,
                T7 = config.T7 * 1000,
                T8 = config.T8 * 1000,
                LinkTestInterval = (config.HeartbeatInterval ?? 30) * 1000,
                AutoReconnect = config.AutoReconnect,
            });
            var connector = new HsmsConnection(options, _logger);
            var secsGem = new SecsGem(options, connector, _logger);
            var context = EAPContext.contexts?.FirstOrDefault(item =>
                string.Equals(item.EquipmentID, config.Name, StringComparison.OrdinalIgnoreCase));
            if (context == null)
            {
                context = new ContextObject { EquipmentID = config.Name };
                lock (ContextCollectionLock)
                {
                    EAPContext.contexts ??= new ConcurrentBag<ContextObject>();
                    EAPContext.contexts.Add(context);
                }
            }
            context.EqpType = config.EQPType;
            context.SecsGem = secsGem;

            var runtime = new DeviceRuntime(config, connector, secsGem, context);
            if (!DeviceRuntimes.TryAdd(config.Name, runtime))
                throw new InvalidOperationException($"Device {config.Name} is already running.");

            var connectionVersion = DateTime.UtcNow.Ticks;
            connector.ConnectionChanged += delegate(object sender, ConnectionState state)
            {
                var currentVersion = Interlocked.Increment(ref connectionVersion);
                UpdateEquipmentConnectionSnapshot(config.Name, config, connector);
                var args = new Arguments { Header = new Dictionary<string, object>() };
                args.Header[CItems.EqpId] = context.EquipmentID;
                args.Header[CItems.EqpType] = context.EqpType;
                args.Header[CItems.ConnectionState] = state;
                args.Header[CItems.ConnectionVersion] = currentVersion;
                args.Header[CItems.ConnectionSessionId] = connector.SessionId;
                args.Header[CItems.ConnectionRetryCount] = connector.RetryCount;
                args.Header[CItems.ConnectionLastError] = connector.LastError;
                args.Header[CItems.ConnectionLastErrorCode] = connector.LastErrorCode;
                args.Header[CItems.ConnectionLastErrorTime] = connector.LastErrorTime;
                args.Header[CItems.ConnectionLastAttemptTime] = connector.LastAttemptTime;
                args.Header[CItems.ConnectionNextRetryTime] = connector.NextRetryTime;
                args.Header[CItems.ConnectionDisconnectReason] = connector.LastDisconnectReason;
                args.Header[CItems.ConnectionRemoteAddress] = connector.RemoteAddress;
                args.Header[CItems.ConnectionAutoReconnect] = connector.AutoReconnect;
                SecsBase.ReciveSecsMessage(context, args, "ConnectionChange");
            };

            try
            {
                UpdateEquipmentConnectionSnapshot(config.Name, config, connector);
                _ = MESClient.Send_EqpStateAsync(config.Name, EQPState.DisConnected, Interlocked.Read(ref connectionVersion));
                connector.Start(runtime.Cancellation.Token);
                connector.LinkTestEnabled = true;
                var interval = config.HeartbeatInterval ?? 30;
                runtime.MonitorTask = RunConnectionMonitorAsync(config.Name, connector, isActive, interval, runtime.Cancellation.Token);
                runtime.HeartbeatTask = RunHeartbeatAsync(config.Name, connector, interval,
                    config.HeartbeatMaxFailures ?? 3, runtime.Cancellation.Token);
                runtime.MessageTask = Task.Run(() => RunPrimaryMessageLoopAsync(runtime));
                if (initializeEquipmentState)
                    MESClient.Send_EapState(config.Name, config.LoadPort, EAPState.Run);
            }
            catch
            {
                DeviceRuntimes.TryRemove(config.Name, out _);
                connector.Stop();
                runtime.Cancellation.Cancel();
                runtime.Cancellation.Dispose();
                throw;
            }
        }

        private static async Task RunPrimaryMessageLoopAsync(DeviceRuntime runtime)
        {
            var token = runtime.Cancellation.Token;
            try
            {
                while (runtime.Connector.State != ConnectionState.Selected &&
                       runtime.Connector.State != ConnectionState.Communicating && !token.IsCancellationRequested)
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                }

                await foreach (var primaryMessage in runtime.SecsGem.GetPrimaryMessageAsync(token).ConfigureAwait(false))
                {
                    var args = new Arguments
                    {
                        Header = new Dictionary<string, object>(),
                        SecsData = primaryMessage
                    };
                    args.Header[CItems.EqpId] = runtime.Context.EquipmentID;
                    args.Header[CItems.EqpType] = runtime.Context.EqpType;
                    SecsBase.ReciveSecsMessage(runtime.Context, args, "SecsMsgHandler");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.Error($"Primary message loop failed for {runtime.Config.Name}: {ex}");
            }
        }

        private static async Task StopDeviceAsync(string equipmentId)
        {
            if (!DeviceRuntimes.TryGetValue(equipmentId, out var runtime))
                return;

            runtime.Connector.Stop();
            runtime.Cancellation.Cancel();
            var tasks = new[] { runtime.MessageTask, runtime.MonitorTask, runtime.HeartbeatTask }
                .Where(task => task != null).ToArray();
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error while stopping device {equipmentId}: {ex.Message}");
            }

            DeviceRuntimes.TryRemove(equipmentId, out _);
            lock (ContextCollectionLock)
            {
                EAPContext.contexts = new ConcurrentBag<ContextObject>(
                    (EAPContext.contexts ?? new ConcurrentBag<ContextObject>())
                    .Where(context => !string.Equals(context.EquipmentID, equipmentId, StringComparison.OrdinalIgnoreCase)));
            }
            runtime.Cancellation.Dispose();
        }

        private sealed class DeviceRuntime
        {
            public DeviceRuntime(ConnectionConfigure config, HsmsConnection connector, SecsGem secsGem, ContextObject context)
            {
                Config = config;
                Connector = connector;
                SecsGem = secsGem;
                Context = context;
            }

            public ConnectionConfigure Config { get; }
            public HsmsConnection Connector { get; }
            public SecsGem SecsGem { get; }
            public ContextObject Context { get; }
            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
            public Task MessageTask { get; set; }
            public Task MonitorTask { get; set; }
            public Task HeartbeatTask { get; set; }
        }

        private static void UpdateEquipmentConnectionSnapshot(string eqpid, ConnectionConfigure config, HsmsConnection connector)
        {
            if (AppCache.Current.EqpInfos == null ||
                !AppCache.Current.EqpInfos.TryGetValue(eqpid, out var eqpInfo))
            {
                return;
            }

            eqpInfo.HsmsConnectionState = connector.State.ToString();
            eqpInfo.HsmsRemoteAddress = connector.RemoteAddress;
            eqpInfo.HsmsRetryCount = connector.RetryCount;
            eqpInfo.HsmsLastError = connector.LastError;
            eqpInfo.HsmsLastErrorCode = connector.LastErrorCode;
            eqpInfo.HsmsLastErrorTime = connector.LastErrorTime?.LocalDateTime;
            eqpInfo.HsmsLastAttemptTime = connector.LastAttemptTime?.LocalDateTime;
            eqpInfo.HsmsNextRetryTime = connector.NextRetryTime?.LocalDateTime;
            eqpInfo.HsmsDisconnectReason = connector.LastDisconnectReason.ToString();
            eqpInfo.HsmsAutoReconnect = connector.AutoReconnect;

            if (string.IsNullOrWhiteSpace(eqpInfo.HsmsRemoteAddress))
            {
                eqpInfo.HsmsRemoteAddress = config.ConnectionMode == ConnectionMode.ACTIVE
                    ? $"{config.RemoteIP}:{config.RemotePort}"
                    : $"{config.LocalIP}:{config.LocalPort}";
            }
        }

        private static async Task RunHeartbeatAsync(
            string eqpid,
            HsmsConnection connector,
            int intervalSeconds,
            int maxFailures,
            CancellationToken cancellationToken)
        {
            var consecutiveFailures = 0;

            try
            {
                using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
                while (await heartbeatTimer.WaitForNextTickAsync(cancellationToken))
                {
                    if (!connector.HasConnected)
                    {
                        consecutiveFailures = 0;
                        continue;
                    }

                    try
                    {
                        if ((connector.State == ConnectionState.Selected ||
                             connector.State == ConnectionState.Communicating) &&
                            connector.IsTransportConnected)
                        {
                            consecutiveFailures = 0;
                            continue;
                        }

                        if (connector.State == ConnectionState.Connecting ||
                            connector.State == ConnectionState.Connected ||
                            connector.State == ConnectionState.Selecting ||
                            connector.State == ConnectionState.Retry ||
                            connector.State == ConnectionState.Reconnecting ||
                            connector.State == ConnectionState.Disabled)
                        {
                            consecutiveFailures = 0;
                            continue;
                        }

                        consecutiveFailures++;
                        _logger.Warning($"HSMS transport heartbeat failed for {eqpid}. State: {connector.State}. Attempt: {consecutiveFailures}.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;
                        _logger.Warning($"Heartbeat for {eqpid} threw an exception: {ex.Message}. Attempt: {consecutiveFailures}.");
                    }

                    if (consecutiveFailures >= maxFailures)
                    {
                        _logger.Error($"Heartbeat failed {maxFailures} times for {eqpid}. Reconnecting...");
                        consecutiveFailures = 0;
                        connector.Reconnect();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private static async Task RunConnectionMonitorAsync(
            string eqpid,
            HsmsConnection connector,
            bool isActive,
            int intervalSeconds,
            CancellationToken cancellationToken)
        {
            if (!isActive)
            {
                return;
            }

            var idlePollInterval = TimeSpan.FromSeconds(Math.Max(1, Math.Min(intervalSeconds, 5)));

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!connector.AutoReconnect ||
                        connector.State == ConnectionState.Disabled)
                    {
                        return;
                    }

                    if (connector.State == ConnectionState.Connecting ||
                        connector.State == ConnectionState.Connected ||
                        connector.State == ConnectionState.Selecting ||
                        connector.State == ConnectionState.Selected ||
                        connector.State == ConnectionState.Communicating)
                    {
                        await Task.Delay(idlePollInterval, cancellationToken);
                        continue;
                    }

                    var nextRetryTime = connector.NextRetryTime;
                    if (nextRetryTime.HasValue)
                    {
                        var delay = nextRetryTime.Value - DateTimeOffset.Now;
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, cancellationToken);
                            continue;
                        }
                    }

                    try
                    {
                        _logger.Info($"Connection monitor reconnecting {eqpid}. State: {connector.State}. RetryCount: {connector.RetryCount}. LastError: {connector.LastError}");
                        connector.Reconnect();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"Connection monitor reconnect for {eqpid} failed: {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}
