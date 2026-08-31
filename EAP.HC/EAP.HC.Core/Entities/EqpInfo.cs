using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core.Entities
{
    public class EqpInfo
    {
        public EqpInfo()
        {
        }

        public bool IsInitial { get; set; }
        /// <summary>
        /// 设备编号
        /// </summary>
        public string EQPID { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        public string EQPType { get; set; }

        /// <summary>
        /// 设备状态
        /// </summary>
        public EQPStatus EqpStatus { get; set; }


        /// <summary>
        /// 设备控制状态
        /// </summary>
        public ControlState ControlStatus { get; set; }


        /// <summary>
        /// 设备连接状态
        /// </summary>
        public ConnectStatus ConnectStatus { get; set; }


        /// <summary>
        /// Mes 连接状态
        /// </summary>
        public MESConnectStatus MESConnectStatus { get; set; }

        public string HsmsConnectionState { get; set; } = string.Empty;

        public string HsmsRemoteAddress { get; set; } = string.Empty;

        public int HsmsRetryCount { get; set; }

        public string HsmsLastError { get; set; } = string.Empty;

        public int HsmsLastErrorCode { get; set; }

        public DateTime? HsmsLastErrorTime { get; set; }

        public DateTime? HsmsLastAttemptTime { get; set; }

        public DateTime? HsmsNextRetryTime { get; set; }

        public string HsmsDisconnectReason { get; set; } = string.Empty;

        public bool HsmsAutoReconnect { get; set; } = true;


        public string Chamber { get; set; }

        public int LoadPort { get; set; }

        /// <summary>
        /// 当前设备Port信息
        /// </summary>
        public Dictionary<int, PortInfo> PortInfo { get; set; }
    }
}
