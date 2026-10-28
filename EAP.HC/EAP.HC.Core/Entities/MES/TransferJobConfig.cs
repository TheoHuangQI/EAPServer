using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class TransferJobConfig : MQBody
    {
        [XmlArray("PORTS")]
        [XmlArrayItem("PORT")]
        public List<TransferPortConfig> Ports { get; set; } = new();
    }

    [Serializable]
    public class TransferPortConfig
    {
        public int PORTID { get; set; }
        public int JOBMODE { get; set; }
        public int ROLE { get; set; }
        public int PARTNERPORTID { get; set; }
    }
}
