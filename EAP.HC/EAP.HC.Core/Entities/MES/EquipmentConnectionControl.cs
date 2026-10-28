using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class EquipmentConnectionControl : MQBody
    {
        public int ENABLE { get; set; }

        public bool SUCCESS { get; set; }

        public string MESSAGE { get; set; } = string.Empty;
    }

    [XmlRoot("Response")]
    public class EquipmentConnectionControlReply
    {
        [XmlElement("Header")]
        public MQHeader Header { get; set; }

        [XmlElement("Body")]
        public EquipmentConnectionControl Body { get; set; }
    }
}
