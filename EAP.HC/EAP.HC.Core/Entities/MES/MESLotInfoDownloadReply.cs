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
    public class MESLotInfoDownloadReply
    {
        public string TID { get; set; }

        public string EQUIPMENTID { get; set; }

        public string CARRIERID { get; set; }

        public string OPERATOR { get; set; }

        public string BATCHCHID { get; set; }

        public int BATCHSIZE { get; set; }

        [XmlArray("LOTLIST")]
        [XmlArrayItem("LOTDATA")]
        public List<LotInfo> LOTLIST { get; set; }
    }
}
