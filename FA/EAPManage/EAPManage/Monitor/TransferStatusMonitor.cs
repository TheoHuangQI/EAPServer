using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using FluentScheduler;

namespace EAPManage.Monitor
{
    public class TransferStatusMonitor : IJob
    {
        public async void Execute()
        {
            try
            {
                var eqpInfos = DataFactory.Current.Equipment.GetAll();
                foreach (var eqpInfo in eqpInfos)
                {
                    MQHeader header = new MQHeader();
                    header.MESSAGENAME = "TransferStatusCheck";
                    var requestMessage = new MQData();
                    requestMessage.mQHeader = header;

                    State msg = new State();
                    msg.EQUIPMENTID = eqpInfo.EqpId;

                    var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                    var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                    var replyData = await ActiveMQClient.Current.SendRequestAsync<State>(xmlMessage);
                    if (replyData != null)
                    {
                        var portInfos = DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqpInfo.EqpId);
                        if (portInfos?.Count > 0)
                        {
                            foreach (var portInfo in portInfos)
                            {
                                if (portInfo.transferState != (int)replyData.TransferStatuses[portInfo.portId - 1])
                                {
                                    DataFactory.Current.ExceptionRecord.Insert(new ExceptionRecord()
                                    {
                                        eqpId = eqpInfo.EqpId,
                                        content = $"DB EQP:{eqpInfo.EqpId} {portInfo.portName} transferState={portInfo.transferState},TransferStatusCheck Result={(int)replyData.TransferStatuses[portInfo.portId - 1]}",
                                        errorTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff")
                                    });
                                }
                            }
                        }

                    }
                }
            }
            catch
            {
            }
        }
    }
}
