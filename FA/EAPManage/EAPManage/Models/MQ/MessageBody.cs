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
    public class MessageBody : MQBody
    {
        public string PORT { get; set; }
        public string MESSAGE { get; set; }
        public string TYPE { get; set; }


    }
}
