using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [XmlRoot("Header")]
    public class MQHeader
    {
        public MQHeader()
        {
            MESSAGENAME = "";
        }
        [XmlElement("MESSAGENAME")]
        public string MESSAGENAME;
        [XmlElement("RESULT")]
        public string RESULT;
    }
}
