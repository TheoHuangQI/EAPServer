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
    public class VID : MQBody
    {
        public int vID { get; set; }
        public string type { get; set; }
        public string value { get; set; }
        public string name { get; set; }
        public int abnormal { get; set; }
    }

    [Flags]
    public enum VIDAbnormalType
    {
        None = 0,

        SameAsPrevious = 1 << 0, // 与上一次值相同（1）
        ZeroValue = 1 << 1, // 值为 0（2）
        FirstValue = 1 << 2, // 首次采集（4）
    }
}
