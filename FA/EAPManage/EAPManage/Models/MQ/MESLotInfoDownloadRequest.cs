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
    public class MESLotInfoDownloadRequest : MQBody
    {
        public string CARRIERID { get; set; }
        public string PORTID { get; set; }
    }
}
