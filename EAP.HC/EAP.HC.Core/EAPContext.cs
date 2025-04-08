using EAP.HC.Core.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class EAPContext
    {
        public static string RootPath= AppDomain.CurrentDomain.BaseDirectory;
        public static object Configuration { get; set; }
        public static string ServerName { get; set; }
        public static List<ConnectionConfigure> EqpConfigures { get; set; }

        public static List<ContextObject> contexts { get; set; }
        public static bool Initialization()
        {
            string path = Path.Combine(EAPContext.RootPath, $"Configuration\\{EAPContext.ServerName}.json");
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                EqpConfigures = JsonConvert.DeserializeObject<List<ConnectionConfigure>>(text);
                contexts = new List<ContextObject>();
                foreach(var eqpconfig in EqpConfigures)
                {
                    if (!AppCache.Current.EqpInfos.ContainsKey(eqpconfig.Name))
                    {
                        AppCache.Current.EqpInfos.Add(eqpconfig.Name, new EqpInfo()
                        {
                            IsInitial = false,
                            EQPID = eqpconfig.Name,
                            EQPType = eqpconfig.EQPType,
                            EqpStatus = EQPStatus.IDLE,
                            ControlStatus = ControlState.EQPOFFLINE,
                            ConnectStatus = ConnectStatus.DISCONNECTED,
                            MESConnectStatus = MESConnectStatus.DISCONNECTED,
                            Chamber = eqpconfig.Chamber,
                            LoadPort = eqpconfig.LoadPort,
                            PortInfo = new Dictionary<int, PortInfo>()
                        });
                        for(int i=1;i<= AppCache.Current.EqpInfos[eqpconfig.Name].LoadPort; i++)
                        {
                            AppCache.Current.EqpInfos[eqpconfig.Name].PortInfo.Add(i, new PortInfo()
                            {
                                PortNo = i,
                                CarrierId = string.Empty,
                                PortStatus = PortStatus.Idle,
                                BatchId = string.Empty,
                                LotIds = new List<string>()
                            });
                        }
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

       
    }
}
