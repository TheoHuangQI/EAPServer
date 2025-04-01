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
                InitializeEAP();
                InitialMQ();
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
                        Task.Run(async () => {

                            var options = Options.Create(new SecsGemOptions
                            {
                                IsActive = true,
                                IpAddress = eqpconfig.RemoteIP,
                                Port = (int)eqpconfig.RemotePort,
                                SocketReceiveBufferSize = (int)65535,
                                DeviceId = (ushort)eqpconfig.DeviceID,
                            });
                            CancellationTokenSource _cancellationTokenSource = new();
                            HsmsConnection _connector = new HsmsConnection(options, _logger);
                            SecsGem _secsGem = new SecsGem(options, _connector, _logger);

                            var eqpid = eqpconfig.Name;
                            var eqpType = eqpconfig.EQPType;
                            ContextObject context = new ContextObject()
                            {
                                EquipmentID = eqpconfig.Name,
                                EqpType = eqpconfig.EQPType,
                                SecsGem = _secsGem
                            };
                            EAPContext.contexts.Add(context);

                            _connector.ConnectionChanged += delegate
                            {
                                SecsBase.ConnectionChange(context,_connector.State); 
                            };
                            _connector.Start(_cancellationTokenSource.Token);


                            while (true)
                            {
                                if (_connector.State == ConnectionState.Selected)
                                    break;
                            }
                            
                            try
                            {
                                await foreach (var primaryMessage in _secsGem.GetPrimaryMessageAsync(_cancellationTokenSource.Token))
                                {
                                    SecsBase.ReciveSecsMessage(context, primaryMessage);
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

        private static void InitialMQ()
        {
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            Task.Run(async () =>
            {
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
                await activeMQClient.InitializeAsync();
            });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        }

    }
}
