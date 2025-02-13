using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core.Entities
{
    public class ConnectionConfigure
    {
        private string name;
        private int deviceId;
        private ConnectionType connectionType;
        private ConnectionMode connectionMode;
        private string localIp;
        private int localPort;
        private string remoteIp;
        private int remotePort;
        private string eqpType;
        private int t3;
        private int t5;
        private int t6;
        private int t7;
        private int t8;


        public string Name { get { return name;  } set{ name = value;} }

        public int DeviceID { get { return deviceId; } set { deviceId = value; } }
        public ConnectionType ConnectionType { get { return connectionType; } set { connectionType = value; } }
        public ConnectionMode ConnectionMode { get { return connectionMode; } set { connectionMode = value; } }
        public string LocalIP { get { return localIp; } set { localIp = value; } }
        public int LocalPort { get { return localPort; } set { localPort = value; } }
        public string RemoteIP { get { return remoteIp; } set { remoteIp = value; } }
        public int RemotePort { get { return remotePort; } set { remotePort = value; } }
        public string EQPType { get { return eqpType; } set { eqpType = value; } }
        public int T3 { get { return t3; } set { t3 = value; } }
        public int T5 { get { return t5; } set { t5 = value; } }
        public int T6 { get { return t6; } set { t6 = value; } }
        public int T7 { get { return t7; } set { t7 = value; } }
        public int T8 { get { return t8; } set { t8 = value; } }
    }
}
