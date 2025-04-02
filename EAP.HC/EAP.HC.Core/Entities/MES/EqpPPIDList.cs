using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    [Serializable]
    [XmlRoot("Body")]
    public class EqpPPIDList
    {
        public string EQUIPMENTID { get; set; }

        public List<string> PPLIST { get; set; }
    }
}
