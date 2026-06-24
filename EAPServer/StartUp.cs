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
                                SocketReceiveBufferSize = (int)65535,
                                DeviceId = (ushort)currentConfig.DeviceID,
                                T3 = currentConfig.T3 * 1000,
                                T5 = currentConfig.T5 * 1000,
                                T6 = currentConfig.T6 * 1000,
                                T7 = currentConfig.T7 * 1000,
                                T8 = currentConfig.T8 * 1000,
                                LinkTestInterval = (currentConfig.HeartbeatInterval ?? 30) * 1000,
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
                            _ = RunHeartbeatAsync(eqpid, _secsGem, _connector, interval, maxFailures, _cancellationTokenSource.Token);

                            _connector.ConnectionChanged += delegate(object sender, ConnectionState state)
                            {
                                var currentVersion = Interlocked.Increment(ref connectionVersion);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>(),
                                };
                                args.Header[CItems.EqpId] = context.EquipmentID;
                                args.Header[CItems.EqpType] = context.EqpType;
                                args.Header[CItems.ConnectionState] = state;
                                args.Header[CItems.ConnectionVersion] = currentVersion;
                                SecsBase.ReciveSecsMessage(context,args, "ConnectionChange"); 
                            };
                            MESClient.Send_EqpState(eqpid, EQPState.DisConnected);
                            _connector.Start(_cancellationTokenSource.Token);
                            _connector.LinkTestEnabled = true;
                            MESClient.Send_EapState(eqpid, currentConfig.LoadPort, EAPState.Run);
                            try
                            {
                                while (_connector.State != ConnectionState.Selected && !_cancellationTokenSource.Token.IsCancellationRequested)
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

        private static async Task RunHeartbeatAsync(
            string eqpid,
            SecsGem secsGem,
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
                    if (connector.State != ConnectionState.Selected)
                    {
                        consecutiveFailures = 0;
                        continue;
                    }

                    var heartbeatSessionId = connector.SessionId;
                    try
                    {
                        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(Math.Min(intervalSeconds, 10)));

                        var reply = await secsGem.SendAsync(new SecsMessage(1, 1), timeoutTokenSource.Token);
                        if (connector.SessionId != heartbeatSessionId)
                        {
                            consecutiveFailures = 0;
                            continue;
                        }

                        if (reply != null)
                        {
                            consecutiveFailures = 0;
                            continue;
                        }

                        consecutiveFailures++;
                        _logger.Warning($"Heartbeat failed for {eqpid}. Attempt: {consecutiveFailures}.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (connector.SessionId != heartbeatSessionId)
                        {
                            consecutiveFailures = 0;
                            continue;
                        }
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
    }
}
