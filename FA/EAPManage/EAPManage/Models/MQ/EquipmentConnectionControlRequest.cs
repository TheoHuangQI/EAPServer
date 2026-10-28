using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [XmlRoot("Request")]
    public class EquipmentConnectionControlRequest
    {
        [XmlElement("Header")]
        public MQHeader Header { get; set; }

        [XmlElement("Body")]
        public EquipmentConnectionControl Body { get; set; }
    }
}
