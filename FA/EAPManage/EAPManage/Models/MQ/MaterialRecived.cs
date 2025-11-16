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
    public class MaterialRecived : MQBody
    {
        public int PORT { get; set; }

        public string CARRIERID { get; set; }

        public string LotId { get; set; }
    }
}
