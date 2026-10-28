using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core.Entities
{
    public enum CarrierIdState : byte
    {
        NotRead,
        WaitingForHost,
        Verified,
        Failed
    }

    public enum CarrierSlotMapState : byte
    {
        NoState,
        NotRead,
        WaitingForHost,
        Verified,
        Failed
    }

    public enum CarrierAccessState : byte
    {
        NotAccessed,
        InAccess,
        Complete,
        Stopped
    }

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

        /// <summary>
        /// E87 load port state maintained from collection events.
        /// </summary>
        public bool AssociationState { get; set; }

        public bool ReservationState { get; set; }

        public bool ClampState { get; set; }

        public CarrierIdState CarrierIdState { get; set; }

        public CarrierSlotMapState SlotMapState { get; set; }

        public CarrierAccessState CarrierAccessState { get; set; }

        public bool CarrierDoorOpen { get; set; }

        public bool CarrierTagReleased { get; set; }

        public bool CarrierApproachComplete { get; set; }

        public string CarrierLocationId { get; set; }

        public bool AccessModeAlarm { get; set; }

        public bool OutOfServiceAlarm { get; set; }

        /// <summary>0 = 原进原出，1 = Transfer。</summary>
        public int JobMode { get; set; }

        /// <summary>0 = None，1 = 出片 LP，2 = 进片 LP。</summary>
        public int TransferRole { get; set; }

        public int TransferPartnerPortNo { get; set; }

        public List<int> CurrentSlotMap { get; set; }
    }
}
