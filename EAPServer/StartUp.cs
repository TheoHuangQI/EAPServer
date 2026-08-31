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

namespace EAPServer
{
    public class StartUp
    {

      //  protected static FALoggerClass CimLogger;
        protected static Object obj = new Object();
   
        private static ISecsGemLogger _logger = new SecsLogger(obj);

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
        
        private static  void InitializeEAP()
        {
            try
            {
                AppCache.Current.InitializeObjectCache();
                bool res = EAPContext.Initialization();
                if (res)
                {
                    foreach (var eqpconfig in EAPContext.EqpConfigures)
                    {
                        var currentConfig = eqpconfig;
                        Task.Run(async () => {
                            var isActive = currentConfig.ConnectionMode == ConnectionMode.ACTIVE;

                            var options = Options.Create(new SecsGemOptions
                            {
                                IsActive = isActive,
                                IpAddress = isActive ? currentConfig.RemoteIP : currentConfig.LocalIP,
                                Port = isActive ? currentConfig.RemotePort : currentConfig.LocalPort,
                                LocalIpAddress = currentConfig.LocalIP,
                                LocalPort = currentConfig.LocalPort,
                                SocketReceiveBufferSize = (int)65535,
                                DeviceId = (ushort)currentConfig.DeviceID,
                                T3 = currentConfig.T3 * 1000,
                                T5 = currentConfig.T5 * 1000,
                                T6 = currentConfig.T6 * 1000,
                                T7 = currentConfig.T7 * 1000,
                                T8 = currentConfig.T8 * 1000,
                                LinkTestInterval = (currentConfig.HeartbeatInterval ?? 30) * 1000,
                                AutoReconnect = currentConfig.AutoReconnect,
                            });
                            CancellationTokenSource _cancellationTokenSource = new();
                            HsmsConnection _connector = new HsmsConnection(options, _logger);
                            SecsGem _secsGem = new SecsGem(options, _connector, _logger);

                            var eqpid = currentConfig.Name;
                            var eqpType = currentConfig.EQPType;
                            ContextObject context = new ContextObject()
                            {
                                EquipmentID = eqpid,
                                EqpType = eqpType,
                                SecsGem = _secsGem,
                            };
                            EAPContext.contexts.Add(context);

                            var interval = currentConfig.HeartbeatInterval ?? 30;
                            var maxFailures = currentConfig.HeartbeatMaxFailures ?? 3;
                            var connectionVersion = 0L;

                            _connector.ConnectionChanged += delegate(object sender, ConnectionState state)
                            {
                                var currentVersion = Interlocked.Increment(ref connectionVersion);
                                UpdateEquipmentConnectionSnapshot(eqpid, currentConfig, _connector);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>(),
                                };
                                args.Header[CItems.EqpId] = context.EquipmentID;
                                args.Header[CItems.EqpType] = context.EqpType;
                                args.Header[CItems.ConnectionState] = state;
                                args.Header[CItems.ConnectionVersion] = currentVersion;
                                args.Header[CItems.ConnectionRetryCount] = _connector.RetryCount;
                                args.Header[CItems.ConnectionLastError] = _connector.LastError;
                                args.Header[CItems.ConnectionLastErrorCode] = _connector.LastErrorCode;
                                args.Header[CItems.ConnectionLastErrorTime] = _connector.LastErrorTime;
                                args.Header[CItems.ConnectionLastAttemptTime] = _connector.LastAttemptTime;
                                args.Header[CItems.ConnectionNextRetryTime] = _connector.NextRetryTime;
                                args.Header[CItems.ConnectionDisconnectReason] = _connector.LastDisconnectReason;
                                args.Header[CItems.ConnectionRemoteAddress] = _connector.RemoteAddress;
                                args.Header[CItems.ConnectionAutoReconnect] = _connector.AutoReconnect;
                                SecsBase.ReciveSecsMessage(context,args, "ConnectionChange"); 
                            };
                            MESClient.Send_EqpState(eqpid, EQPState.DisConnected);
                            _connector.Start(_cancellationTokenSource.Token);
                            _connector.LinkTestEnabled = true;
                            _ = RunConnectionMonitorAsync(eqpid, _connector, isActive, interval, _cancellationTokenSource.Token);
                            _ = RunHeartbeatAsync(eqpid, _connector, interval, maxFailures, _cancellationTokenSource.Token);
                            MESClient.Send_EapState(eqpid, currentConfig.LoadPort, EAPState.Run);
                            try
                            {
                                while (_connector.State != ConnectionState.Selected &&
                                       _connector.State != ConnectionState.Communicating &&
                                       !_cancellationTokenSource.Token.IsCancellationRequested)
                                {
                                    await Task.Delay(200, _cancellationTokenSource.Token);
                                }

                                await foreach (var primaryMessage in _secsGem.GetPrimaryMessageAsync(_cancellationTokenSource.Token))
                                {
                                    Arguments args = new Arguments()
                                    {
                                        Header = new Dictionary<string, object>(),
                                        SecsData = primaryMessage
                                    };
                                    args.Header[CItems.EqpId] = context.EquipmentID;
                                    args.Header[CItems.EqpType] = context.EqpType;
                                    SecsBase.ReciveSecsMessage(context, args, "SecsMsgHandler");
                                }
                            }
                            catch (OperationCanceledException)
                            {

                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
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
