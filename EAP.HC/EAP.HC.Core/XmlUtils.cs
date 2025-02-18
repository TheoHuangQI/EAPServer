using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EAP.HC.Core
{
    public class XmlUtils
    {
        public static string XmlGetNodeAttrVal(XmlNode node, string attrName, string defaultVal = "")
        {
            XmlAttribute xmlAttribute = node.Attributes[attrName];
            return (xmlAttribute == null) ? defaultVal : xmlAttribute.InnerText;
        }
    }
}
