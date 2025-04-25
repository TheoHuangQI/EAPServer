using System.Xml.Serialization;

namespace EAP.HC.Core.Entities.MES
{
    public class Transaction
    {
        public string MessageName { get; set; }

        public string Description { get; set; }

        public int Stream { get; set; }

        public int Function { get; set; }

        public string Direction { get; set; }

        public bool Wait { get; set; }

        public bool AutoReply { get; set; }

        public string DataItemContent { get; set; }
    }
}
