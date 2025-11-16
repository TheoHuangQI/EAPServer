using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using FluentScheduler;
using System.Net.NetworkInformation;

namespace EAPManage.Monitor
{
    public class EQPMonitor : IJob
    {
        public readonly LogService _logService;

        public EQPMonitor(IServiceScopeFactory scopeFactory)
        {
            try
            {
                using (var scope = scopeFactory.CreateScope())
                {
                    // 从作用域中解析 LogService
                    _logService = scope.ServiceProvider.GetRequiredService<LogService>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async void Execute()
        {
            var eqpInfos = DataFactory.Current.Equipment.GetAll();
            foreach (var eqpInfo in eqpInfos)
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "EQPHeartbeat";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MQBody mQBody = new MQBody();
                mQBody.EQUIPMENTID = eqpInfo.EqpId;

                var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData =await ActiveMQClient.Current.SendRequestAsync<MQBody>(xmlMessage);
                if (replyData != null)
                {
                    if (eqpInfo.EqpStatus != (int)EQPState.Connected)
                    {
                        DataFactory.Current.Equipment.Update(u => u.EqpId == eqpInfo.EqpId, u => new Equipment
                        {
                            EqpStatus = (int)EQPState.Connected
                        });
                        _logService.SendMessage(eqpInfo.EqpId,"0", "EqpState", ((int)EQPState.Connected).ToString(), "info");
                    }
                }
                else
                {
                    if (eqpInfo.EqpStatus != (int)EQPState.DisConnected)
                    {
                        DataFactory.Current.Equipment.Update(u => u.EqpId == eqpInfo.EqpId, u => new Equipment
                        {
                            EqpStatus = (int)EQPState.DisConnected
                        });
                        _logService.SendMessage(eqpInfo.EqpId, "0", "EqpState", ((int)EQPState.DisConnected).ToString(), "info");
                    }
                }
            }
        }
    }
}
