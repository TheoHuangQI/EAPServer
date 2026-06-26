using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core.Entities
{
    public enum AutoRunMode
    {
        Running = 0,
        Paused = 1,
        Stopped = 2
    }

    public class PortInfo
    {
        public PortInfo()
        {
            AutoRunMode = AutoRunMode.Running; // 默认启动循环跑片
        }
        /// <summary>
        /// PORT编号
        /// </summary>
        public int PortNo { get; set; }

        public string CarrierId { get; set; }
        /// <summary>
        /// PORT 状态
        /// </summary>
        public PortStatus PortStatus { get; set; }

        /// <summary>
        /// 循环跑片状态
        /// </summary>
        public AutoRunMode AutoRunMode { get; set; }

        /// <summary>
        /// 当前Port作业的BatchId(同时只能有一个)
        /// </summary>
        public string BatchId { get; set; }

        /// <summary>
        /// 当前Port 正在Run的LotId（同时可能存在多个）
        /// </summary>
        public List<string> LotIds { get; set; }

        public AccessMode AccessState { get; set; }

        public TransferStatus TransferStatus { get; set; }
    }
}
