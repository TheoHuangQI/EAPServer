using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class State : MQBody
    {
        public EAPState EAPState { get; set; }

        public EQPState EQPState { get; set; }

        public long ConnectionVersion { get; set; }

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
}
