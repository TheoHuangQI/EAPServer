using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    public class LotInfo
    {
        public LotState LOT_STATE { get; set; }

        public string LOT_ID { get; set; }

        public string QTY { get; set; }

        public string PPID { get; set; }
        [XmlArray("WAFERS")]
        [XmlArrayItem("WAFER")]
        public List<Wafer> WAFERS { get; set; }
    }
}
