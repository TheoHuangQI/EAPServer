using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    public class Lot
    {

        public string LOT_ID { get; set; }

        public int QTY { get; set; }

        public string PPID { get; set; }
        [XmlArray("WAFERS")]
        [XmlArrayItem("WAFER")]
        public List<WaferInfo> WAFERS { get; set; }
    }
}
