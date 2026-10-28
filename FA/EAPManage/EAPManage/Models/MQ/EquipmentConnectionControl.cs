using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class EquipmentConnectionControl
    {
        public string EQUIPMENTID { get; set; } = string.Empty;

        public int ENABLE { get; set; }

        public bool SUCCESS { get; set; }

        public string MESSAGE { get; set; } = string.Empty;
    }
}
