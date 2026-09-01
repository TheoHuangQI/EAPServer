using System;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class LoopProcessStateMsg : MQBody
    {
        public int PORTID { get; set; }

        public int STATE { get; set; }
    }
}
