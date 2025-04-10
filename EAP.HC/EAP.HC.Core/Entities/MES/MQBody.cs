using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class MQBody
    {
        public string EQUIPMENTID { get; set; }

        public string LOADPORT { get; set; }
    }
}
