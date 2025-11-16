using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using FluentScheduler;

namespace EAPManage.Monitor
{
    public class ControlStateMonitor : IJob
    {
        public async void Execute()
        {
            var eqpInfos = DataFactory.Current.Equipment.GetAll();
            foreach (var eqpInfo in eqpInfos)
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "ControlStateCheck";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                State msg = new State();
                msg.EQUIPMENTID = eqpInfo.EqpId;

                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData = await ActiveMQClient.Current.SendRequestAsync<State>(xmlMessage);
                if (replyData != null)
                {
                    if ((int)replyData.ControlState != eqpInfo.ControlState)
                    {
                        DataFactory.Current.ExceptionRecord.Insert(new ExceptionRecord()
                        {
                            eqpId = eqpInfo.EqpId,
                            content = $"DB EQP:{eqpInfo.EqpId} ControlState={eqpInfo.ControlState},ControlStateCheck Result={(int)replyData.ControlState}",
                            errorTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff")
                        });
                    }
                }
                else
                {
                    DataFactory.Current.Equipment.Update(u => u.EqpId == eqpInfo.EqpId, u => new Equipment()
                    {
                        ControlState = (int)ControlState.UNKNOWN
                    });
                }
            }
        }
    }
}
