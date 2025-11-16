using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class LPTransferStatus : MQBody
    {
        public string portId { get; set; }

        public TransferStatus transferStatus { get; set; }
    }

    public enum TransferStatus : byte
    {
        OutOfService,
        TransferBlocked,
        ReadyToLoad,
        ReadyToUnload,
        InService,
        TransferReady
    }
}
