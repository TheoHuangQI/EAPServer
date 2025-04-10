using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class LPTransferStatus : MQBody
    {
        public string portId { get; set; }

        public TransferStatus transferStatus { get; set; }
    }
}
