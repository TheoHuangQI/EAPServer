using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [XmlRoot("Request")]
    public class MQData
    {
        public MQData()
        {
            mQBody = new MQBody();
        }
        [XmlElement("Header")]
        public MQHeader mQHeader { get; set; }
        [XmlElement("Body")]
        public MQBody mQBody { get; set; }
    }
}
