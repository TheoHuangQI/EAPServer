using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class EqpPPIDList: MQBody
    {

        public List<string> PPLIST { get; set; }
    }
}
