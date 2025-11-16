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
    public class MESLotTrackIn : MQBody
    {
        public string CARRIERID { get; set; }
        [XmlArray("LOTLIST")]
        [XmlArrayItem("LOT")]
        public List<string> LOTLIST { get; set; }
    }
}
