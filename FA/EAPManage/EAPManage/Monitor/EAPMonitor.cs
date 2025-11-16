using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using FluentScheduler;

namespace EAPManage.Monitor
{
    public class EAPMonitor : IJob
    {
        public readonly LogService _logService;

        public EAPMonitor(IServiceScopeFactory scopeFactory)
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
            foreach(var eqpInfo in eqpInfos)
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "EAPHeartbeat";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MQBody mQBody = new MQBody();
                mQBody.EQUIPMENTID = eqpInfo.EqpId;

                var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData =await ActiveMQClient.Current.SendRequestAsync<MQBody>(xmlMessage);
                if (replyData != null)
                {
                    if (eqpInfo.EapStatus != (int)EAPState.Run)
                    {
                        DataFactory.Current.Equipment.Update(u => u.EqpId == eqpInfo.EqpId, u => new Equipment
                        {
                            EapStatus = (int)EAPState.Run
                        });
                        _logService.SendMessage(eqpInfo.EqpId, "0", "EapState", ((int)EAPState.Run).ToString(), "info");
                    }
                }
                else
                {
                    if (eqpInfo.EapStatus != (int)EAPState.Stop)
                    {
                        DataFactory.Current.Equipment.Update(u => u.EqpId == eqpInfo.EqpId, u => new Equipment
                        {
                            EapStatus = (int)EAPState.Stop
                        });
                        _logService.SendMessage(eqpInfo.EqpId, "0", "EapState", ((int)EAPState.Stop).ToString(), "info");
                    }
                }
            }
        }
    }
}
