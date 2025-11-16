using System.Reflection.PortableExecutable;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [XmlRoot("Response")]
    public class MQReplyData
    {
        public MQReplyData()
        {
            mQBody = new MQBody();
        }
        [XmlElement("Header")]
        public MQHeader mQHeader { get; set; }
        [XmlElement("Body")]
        public MQBody mQBody { get; set; }
    }
}
