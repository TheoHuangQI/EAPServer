using EAPManage.Models.MQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class MESLotInfoDownloadReply : MQBody
    {
        public string TID { get; set; }

        [XmlElement("PORTID")]
        public string PORTID { get; set; }

        public string CARRIERID { get; set; }

        public string OPERATOR { get; set; }

        public string BATCHCHID { get; set; }

        public int BATCHSIZE { get; set; }

        [XmlArray("LOTLIST")]
        [XmlArrayItem("LOTDATA")]
        public List<Lot> LOTLIST { get; set; }

    }
}
