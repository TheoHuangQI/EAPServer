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
   
    }
}
