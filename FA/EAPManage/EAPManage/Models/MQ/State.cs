using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{

    [Serializable]
    [XmlRoot("Body")]
    public class State : MQBody
    {
        public EAPState EAPState { get; set; }

        public EQPState EQPState { get; set; }

        public ControlState ControlState { get; set; }

        public List<AccessMode> AccessModes { get; set; }

        public List<TransferStatus> TransferStatuses { get; set; }
    }

    public enum EQPState
    {
        DisConnected,
        Connected
    }

    public enum EAPState
    {
        Stop,
        Run
    }

    public enum ControlState
    {
        UNKNOWN = 0,
        EQPOFFLINE = 1,
        OFFLINEATTEMPTINGTOONLINE = 2,
        HOSTOFFLINE = 3,
        LOCAL = 4,
        REMOTE = 5,
    }
}
