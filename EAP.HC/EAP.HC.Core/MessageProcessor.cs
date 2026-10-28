using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using EAP.HC.Core.Entities.MES;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Transaction = EAP.HC.Core.Entities.MES.Transaction;

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
                    case "EQUIPMENTCONNECTIONCONTROL":
                        {
                            var request = XmlSerialization.DeserializeNodeObj<EquipmentConnectionControl>(message, "Request", "Body");
                            var response = EAPContext.DeviceConnectionControlHandler == null
                                ? new EquipmentConnectionControl
                                {
                                    EQUIPMENTID = request?.EQUIPMENTID ?? string.Empty,
                                    ENABLE = request?.ENABLE ?? 0,
                                    MESSAGE = "Equipment connection control is unavailable."
                                }
                                : EAPContext.DeviceConnectionControlHandler(request).GetAwaiter().GetResult();
                            var reply = new EquipmentConnectionControlReply
                            {
                                Header = new MQHeader
                                {
                                    MESSAGENAME = replyHeader.MESSAGENAME,
                                    RESULT = MQResult.SUCCESS.ToString()
                                },
                                Body = response
                            };
                            mrea.Reply(XmlSerialization.XmlSerialize(reply));
                            break;
                        }
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
                            if (AppCache.Current.EqpInfos != null)
                            {
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
                            }
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
                                res.mQHeader.RESULT = args.GetResultCode() == "OK"
                                    ? MQResult.SUCCESS.ToString()
                                    : MQResult.FAILURE.ToString();
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
                    case "FDCSVIDCOLLECT":
                        {
                            FdcSvidCollection mQBody = XmlSerialization.DeserializeNodeObj<FdcSvidCollection>(message, "Request", "Body");
                            mQBody ??= new FdcSvidCollection();
                            var eqpId = mQBody.EQUIPMENTID;
                            if (string.IsNullOrWhiteSpace(eqpId))
                            {
                                mQBody.Return = "NG";
                                mQBody.Message = "Equipment id is empty.";
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                            }
                            else if (AppCache.Current.EqpInfos.ContainsKey(eqpId))
                            {
                                var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                                Arguments args = new Arguments()
                                {
                                    Header = new Dictionary<string, object>()
                                };
                                args.Header[CItems.EqpId] = eqpId;
                                args.Header[CItems.FdcSvidCollection] = mQBody;

                                var collectResult = SecsBase.ReciveSecsMessage<FdcSvidCollection>(context, args, "CollectFdcSvidValues");
                                if (collectResult != null)
                                {
                                    mQBody = collectResult;
                                    res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                                }
                                else
                                {
                                    mQBody.Return = "NG";
                                    mQBody.Message = $"FDC S1F3 collect failed for {eqpId}.";
                                    res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                }
                            }
                            else
                            {
                                mQBody.Return = "NG";
                                mQBody.Message = $"Equipment {eqpId} is not loaded in EAPServer.";
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mQBody);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "TestItemStart":
                        {
                            EAPTestItemStart eapTestItem = XmlSerialization.DeserializeNodeObj<EAPTestItemStart>(message, "Request", "Body");
                            var eqpId = eapTestItem.EQUIPMENTID;
                            var testItem = eapTestItem.EAPTestItem;
                            var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                            var resCode = SecsBase.StartTest(context, testItem);
                            if (resCode)
                            {
                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                            }
                            var xmlBody = XmlSerialization.SerializeOnlyBody(eapTestItem);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "TestBatchStart":
                        {
                            EAPTestBatchStart eAPTestBatch = XmlSerialization.DeserializeNodeObj<EAPTestBatchStart>(message, "Request", "Body");
                            var eqpId = eAPTestBatch.EQUIPMENTID;
                            var context = EAPContext.contexts.FirstOrDefault(u => u.EquipmentID == eqpId);
                            var testItems = eAPTestBatch.EAPTestBatch.TestItems;



                            //var resCode = SecsBase.StartTest(context, testItem);
                            //if (resCode)
                            //{
                            //    res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            //}
                            //else
                            //{
                            //    res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                            //}
                            //var xmlBody = XmlSerialization.SerializeOnlyBody(eapTestItem);
                            //var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            //mrea.Reply(xmlMessage);
                            break;

                        }
                    case "LOOPPROCESSSTATE":
                        {
                            LoopProcessStateMsg stateMsg = XmlSerialization.DeserializeNodeObj<LoopProcessStateMsg>(message, "Request", "Body");
                            var eqpId = stateMsg.EQUIPMENTID;
                            var portId = stateMsg.PORTID;
                            var state = stateMsg.STATE;

                            var loopProcessStates = AppCache.Current.LoopProcessState
                                ??= new Dictionary<string, Dictionary<int, int>>();
                            lock (loopProcessStates)
                            {
                                if (eqpId == "*")
                                {
                                    foreach (var eqp in AppCache.Current.EqpInfos.Keys.ToList())
                                    {
                                        if (!loopProcessStates.ContainsKey(eqp))
                                            loopProcessStates[eqp] = new Dictionary<int, int>();

                                        for (int i = 1; i <= AppCache.Current.EqpInfos[eqp].LoadPort; i++)
                                        {
                                            loopProcessStates[eqp][i] = state;
                                        }
                                    }
                                }
                                else
                                {
                                    if (!loopProcessStates.ContainsKey(eqpId))
                                        loopProcessStates[eqpId] = new Dictionary<int, int>();

                                    loopProcessStates[eqpId][portId] = state;
                                }
                            }

                            var stateText = state == 0 ? "RUNNING" : state == 1 ? "STOPPED" : "PAUSED";
                            Console.WriteLine($"[LoopProcess MQ] eqp={eqpId} port={portId} state={stateText}");
                            break;
                        }
                    case "TRANSFERJOBCONFIG":
                        {
                            var config = XmlSerialization.DeserializeNodeObj<TransferJobConfig>(message, "Request", "Body");
                            if (config?.Ports == null || string.IsNullOrWhiteSpace(config.EQUIPMENTID)
                                || !AppCache.Current.EqpInfos.TryGetValue(config.EQUIPMENTID, out var eqpInfo))
                            {
                                break;
                            }

                            foreach (var portConfig in config.Ports)
                            {
                                if (eqpInfo.PortInfo.TryGetValue(portConfig.PORTID, out var port))
                                {
                                    port.JobMode = portConfig.JOBMODE;
                                    port.TransferRole = portConfig.ROLE;
                                    port.TransferPartnerPortNo = portConfig.PARTNERPORTID;
                                }
                            }
                            Console.WriteLine($"[TransferJobConfig MQ] eqp={config.EQUIPMENTID} ports={config.Ports.Count}");
                            break;
                        }
                }
            }
        }
    }
}
