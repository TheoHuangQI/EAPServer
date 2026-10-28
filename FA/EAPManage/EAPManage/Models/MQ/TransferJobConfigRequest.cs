using System.Collections.Generic;

namespace EAPManage.Models.MQ
{
    public class TransferJobConfigRequest
    {
        public string eqpId { get; set; }
        public List<TransferPortConfigRequest> ports { get; set; }
    }

    public class TransferPortConfigRequest
    {
        public int portId { get; set; }
        public int jobMode { get; set; }
        public int role { get; set; }
        public int partnerPortId { get; set; }
    }
}
