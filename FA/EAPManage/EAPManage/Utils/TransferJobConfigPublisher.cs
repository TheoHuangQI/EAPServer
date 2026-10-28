using EAPManage.Models.DB;
using EAPManage.Models.MQ;

namespace EAPManage.Utils
{
    public static class TransferJobConfigPublisher
    {
        public static void Publish(string eqpId, IEnumerable<PortInfo> ports)
        {
            var message = new MQData
            {
                mQHeader = new MQHeader { MESSAGENAME = "TRANSFERJOBCONFIG" }
            };
            var body = new TransferJobConfig
            {
                EQUIPMENTID = eqpId,
                Ports = ports.OrderBy(p => p.portId).Select(p => new TransferPortConfig
                {
                    PORTID = p.portId,
                    JOBMODE = p.jobMode,
                    ROLE = p.transferRole,
                    PARTNERPORTID = p.transferPartnerPortId
                }).ToList()
            };

            var xml = XmlSerialization.XmlSerializeMessage(message, XmlSerialization.SerializeOnlyBody(body));
            ActiveMQClient.Current.SendAsync(xml);
        }
    }
}
