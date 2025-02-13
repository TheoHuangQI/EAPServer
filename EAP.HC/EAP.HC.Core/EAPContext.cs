using EAP.HC.Core.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
                return true;
            }
            else
            {
                return false;
            }
        }

       
    }
}
