using System.Reflection.PortableExecutable;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
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
