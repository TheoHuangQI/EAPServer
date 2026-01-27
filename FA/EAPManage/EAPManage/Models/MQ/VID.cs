using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class VID : MQBody
    {
        public int vID { get; set; }
        public string type { get; set; }
        public string value { get; set; }
        public string name { get; set; }
        public int abnormal { get; set; }
    }
}
