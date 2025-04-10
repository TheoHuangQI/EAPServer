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
    public class LPAccessMode : MQBody
    {
        public string portId { get; set; }

        public AccessMode accessMode { get; set; }
    }
}
