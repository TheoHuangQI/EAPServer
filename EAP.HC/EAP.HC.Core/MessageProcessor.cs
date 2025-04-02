using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using EAP.HC.Core.Entities.MES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class MessageProcessor
    {
        public static void HandleMessage(IMessage Imessage, MessageReceivedEventArgs mrea)
        {
            // 处理消息
            var replyHeader = new MQHeader();
            var message = (Imessage as ITextMessage).Text;
            replyHeader = XmlSerialization.DeserializeNodeObj<MQHeader>(message, "Request", "Header");
            var res = new MQReplyData()
            {
                mQHeader = new MQHeader()
                {
                    MESSAGENAME = replyHeader.MESSAGENAME,
                }
            };
            switch (replyHeader.MESSAGENAME)
            {
                case "GETPPIDLIST":
                    {
                        EqpPPIDList eqpPPIDList = XmlSerialization.DeserializeNodeObj<EqpPPIDList>(message, "Request", "Body");
                        var eqpId = eqpPPIDList.EQUIPMENTID;
                        var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                        List<string>  recipeList=SecsBase.GetPPIDList(context);
                        eqpPPIDList.PPLIST = recipeList;
                        res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                        var xmlBody = XmlSerialization.SerializeOnlyBody(eqpPPIDList);
                        var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                        mrea.Reply(xmlMessage);
                        break;
                    }
                   
            }
        }
    }
}
