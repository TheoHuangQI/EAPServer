using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [XmlRoot("MESSAGE")]
    public class MesBatchInfo
    {
        public string MESSAGE_ID { get; set; } //

        public string TID { get; set; }//

        public string EQP_ID { get; set; } //设备号

        public string OPERATOR { get; set; }

        public string BATCH_ID { get; set; }

        public string BATCH_SIZE { get; set; }
        [XmlArray("LOTDATALIST")]
        [XmlArrayItem("LOTDATA")]
        public List<LotInfo> LOTDATALIST { get; set; }
    }
}
