using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class EqpPPIDList : MQBody
    {
        public List<string> PPLIST { get; set; }
    }
}
