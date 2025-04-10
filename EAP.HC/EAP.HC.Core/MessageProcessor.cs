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
            if (message.Contains("<Request>"))
            {
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
                            List<string> recipeList = SecsBase.GetPPIDList(context);
                            eqpPPIDList.PPLIST = recipeList;
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            var xmlBody = XmlSerialization.SerializeOnlyBody(eqpPPIDList);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "EQPHeartbeat":
                        {
                            MQBody mQBody = XmlSerialization.DeserializeNodeObj<MQBody>(message, "Request", "Body");
                            var eqpId = mQBody.EQUIPMENTID;
                            if (AppCache.Current.EqpInfos.ContainsKey(eqpId))
                            {

                                var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>()
                                };
                                args.Header[CItems.EqpId] = eqpId;
                                var heartRes = SecsBase.ReciveSecsMessage<bool>(context, args, "Heart");
                                if (heartRes)
                                {
                                    res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                                }
                                else
                                {
                                    res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                }
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "EAPHeartbeat":
                        {
                            MQBody mQBody = XmlSerialization.DeserializeNodeObj<MQBody>(message, "Request", "Body");
                            var eqpId = mQBody.EQUIPMENTID;
                            if (AppCache.Current.EqpInfos.ContainsKey(eqpId))
                            {

                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "ControlStateCheck":
                        {
                            State mQBody = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var eqpId = mQBody.EQUIPMENTID;
                            if (AppCache.Current.EqpInfos.ContainsKey(eqpId))
                            {
                                var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>()
                                };
                                args.Header[CItems.EqpId] = eqpId;
                                args.Header[CItems.Check] = true;
                                var controlState = SecsBase.ReciveSecsMessage<ControlState>(context, args, "Send_S1F3_Get_ControlState");
                                mQBody.ControlState = controlState;
                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                mQBody.ControlState = ControlState.UNKNOWN;
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "AccessModeCheck":
                        {
                            State mQBody = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var eqpId = mQBody.EQUIPMENTID;
                            if (AppCache.Current.EqpInfos.ContainsKey(eqpId))
                            {
                                var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>()
                                };
                                args.Header[CItems.EqpId] = eqpId;
                                args.Header[CItems.Check] = true;
                                var accessModes = SecsBase.ReciveSecsMessage<List<AccessMode>>(context, args, "Send_S1F3_Get_AccessMode");
                                mQBody.AccessModes = accessModes;
                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                mQBody.AccessModes = null;
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "TransferStatusCheck":
                        {
                            State mQBody = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var eqpId = mQBody.EQUIPMENTID;
                            if (AppCache.Current.EqpInfos.ContainsKey(eqpId))
                            {
                                var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>()
                                };
                                args.Header[CItems.EqpId] = eqpId;
                                args.Header[CItems.Check] = true;
                                var transferStatuses = SecsBase.ReciveSecsMessage<List<TransferStatus>>(context, args, "Send_S1F3_Get_TransferStatus");
                                mQBody.TransferStatuses = transferStatuses;
                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                mQBody.ControlState = ControlState.UNKNOWN;
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                }
            }
        }
    }
}
