using Apache.NMS;
using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using System.Reflection.PortableExecutable;
using System.Xml;

namespace EAPManage.Utils
{
    public class MessageProcessor
    {
        public readonly LogService _logService;

        public MessageProcessor(IServiceScopeFactory scopeFactory)
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

        public void HandleMessage(IMessage Imessage, MessageReceivedEventArgs mrea)
        {
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
                    case "LOTINFODOWNLOAD":
                        {
                            MESLotInfoDownloadRequest request = XmlSerialization.DeserializeNodeObj<MESLotInfoDownloadRequest>(message, "Request", "Body");
                            var eqpid = request.EQUIPMENTID;
                            var carrierId = request.CARRIERID;
                            var portId = request.PORTID;
                            var batch = DataFactory.Current.Batch.GetList(u => u.EqpId == eqpid && u.CarrierId == carrierId);
                            if(batch == null || batch.Count == 0)
                            {
                                batch = DataFactory.Current.Batch.GetList(u => u.EqpId == eqpid && u.PortId == portId);
                                if (batch.Count > 1)
                                {
                                    batch.RemoveRange(1, batch.Count - 1);
                                }
                            }
                            if (batch != null && batch.Count == 1)
                            {
                                MESLotInfoDownloadReply reply = new MESLotInfoDownloadReply();
                                reply.EQUIPMENTID = eqpid;
                                reply.CARRIERID = carrierId;
                                reply.BATCHCHID = batch.First().BatchId;
                                reply.TID = batch.First().TID;
                                reply.OPERATOR = batch.First().OPERATOR;
                                reply.BATCHSIZE = batch.First().BatchSize;
                                reply.LOTLIST = new List<Lot>();

                                var LotList = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.First().BatchId);
                                foreach (var Lot in LotList)
                                {
                                    Lot lotInfo = new Lot();
                                    lotInfo.LOT_ID = Lot.LotId;
                                    lotInfo.PPID = Lot.PPID;
                                    lotInfo.QTY = Lot.QTY;
                                    lotInfo.WAFERS = new List<WaferInfo>();
                                    var wafers = DataFactory.Current.Wafer.GetList(u => u.lotId == Lot.LotId);
                                    foreach (var wafer in wafers)
                                    {
                                        WaferInfo waferInfo = new WaferInfo();
                                        waferInfo.WAFER_ID = wafer.WaferId;
                                        waferInfo.SLOT = wafer.Slot;
                                        lotInfo.WAFERS.Add(waferInfo);
                                    }
                                    reply.LOTLIST.Add(lotInfo);
                                }

                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                                var xmlBody = XmlSerialization.SerializeOnlyBody(reply);
                                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                                mrea.Reply(xmlMessage);
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                var xmlMessage = XmlSerialization.SerializeOnlyBody(res);
                                mrea.Reply(xmlMessage);
                            }
                            break;
                        }
                    case "SENDMESSAGE":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MessageBody request = XmlSerialization.DeserializeNodeObj<MessageBody>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);

                            var eqpid = request.EQUIPMENTID;
                            var portId = request.PORT;
                            var msg = request.MESSAGE;
                            var msgType = request.TYPE;  //info / warning / error
                            _logService.SendMessage(eqpid, portId, "Log", $"[{portId}]: {msg}", msgType.ToLower());
                            break;
                        }
                    case "TRACKIN":
                        {
                            MESLotTrackIn mESLotTrackIn = XmlSerialization.DeserializeNodeObj<MESLotTrackIn>(message, "Request", "Body");
                            var flag = true;
                            DataFactory.Current.Lot.ExecuteTransaction(db =>
                            {
                                foreach (var lot in mESLotTrackIn.LOTLIST)
                                {
                                    var dbres = DataFactory.Current.Lot.Update(u => u.LotId == lot, u => new LotInfo
                                    {
                                        LotState = LotState.TrackIn
                                    });
                                    if (dbres <= 0)
                                    {
                                        flag = false;
                                    }
                                }
                            });
                            res.mQHeader.RESULT = flag ? MQResult.SUCCESS.ToString() : MQResult.FAILURE.ToString();
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "LOTSTART":
                        {
                            MESLotTrackIn mESLotTrackIn = XmlSerialization.DeserializeNodeObj<MESLotTrackIn>(message, "Request", "Body");
                            var flag = true;
                            DataFactory.Current.Lot.ExecuteTransaction(db =>
                            {
                                foreach (var lot in mESLotTrackIn.LOTLIST)
                                {
                                    var dbres = DataFactory.Current.Lot.Update(u => u.LotId == lot, u => new LotInfo
                                    {
                                        LotState = LotState.LotStart
                                    });


                                    var portInfo = DataFactory.Current.PortInfo.GetList(u => u.eqpId == mESLotTrackIn.EQUIPMENTID && u.carrierId == mESLotTrackIn.CARRIERID).FirstOrDefault();
                                    var eqpInfo = DataFactory.Current.Equipment.GetList(u => u.EqpId == mESLotTrackIn.EQUIPMENTID).FirstOrDefault();
                                    var resCode = DataFactory.Current.RunHistory.Insert(new RunHistory()
                                    {
                                        eqpId = mESLotTrackIn.EQUIPMENTID,
                                        eqpType = eqpInfo == null ? "" : eqpInfo.EqpType,
                                        lotId = lot,
                                        state = (int)LotRunState.Start,
                                        startTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff"),
                                        carrierId = mESLotTrackIn.CARRIERID,
                                        portId = portInfo.portId,
                                        portName = portInfo.portName
                                    });

                                    if (dbres <= 0 || !resCode)
                                    {
                                        flag = false;
                                    }
                                }
                            });
                            res.mQHeader.RESULT = flag ? MQResult.SUCCESS.ToString() : MQResult.FAILURE.ToString();
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "LOTEND":
                        {
                            MESLotTrackIn mESLotTrackIn = XmlSerialization.DeserializeNodeObj<MESLotTrackIn>(message, "Request", "Body");
                            var flag = true;
                            DataFactory.Current.Lot.ExecuteTransaction(db =>
                            {
                                foreach (var lot in mESLotTrackIn.LOTLIST)
                                {
                                    var dbres = DataFactory.Current.Lot.Update(u => u.LotId == lot, u => new LotInfo
                                    {
                                        LotState = LotState.LotEnd
                                    });
                                    if (dbres <= 0)
                                    {
                                        flag = false;
                                    }
                                }
                            });
                            res.mQHeader.RESULT = flag ? MQResult.SUCCESS.ToString() : MQResult.FAILURE.ToString();
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "LOTTRACKOUT":
                        {
                            MESLotTrackIn mESLotTrackIn = XmlSerialization.DeserializeNodeObj<MESLotTrackIn>(message, "Request", "Body");
                            var flag = true;
                            DataFactory.Current.Lot.ExecuteTransaction(db =>
                            {
                                foreach (var lot in mESLotTrackIn.LOTLIST)
                                {
                                    var dbres = DataFactory.Current.Lot.Update(u => u.LotId == lot, u => new LotInfo
                                    {
                                        LotState = LotState.TrackOut
                                    });

                                    var resCode = DataFactory.Current.RunHistory.Update(u => u.eqpId == mESLotTrackIn.EQUIPMENTID && u.lotId == lot && u.endTime == null, u => new RunHistory()
                                    {
                                        state = (int)LotRunState.End,
                                        endTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff"),
                                    });

                                    if (dbres <= 0 || resCode <= 0)
                                    {
                                        flag = false;
                                    }
                                }
                            });
                            res.mQHeader.RESULT = flag ? MQResult.SUCCESS.ToString() : MQResult.FAILURE.ToString();
                            var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            break;
                        }
                    case "SENDMAPRESULT":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MapResult request = XmlSerialization.DeserializeNodeObj<MapResult>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var eqpid = request.EQUIPMENTID;
                            var portId = request.PORT;
                            if (request.MapStr.EndsWith(';'))
                            {
                                _logService.SendMessage(eqpid, portId, "Map", $"{request.MapStr.Substring(0, request.MapStr.Length - 1)}", "info");

                            }
                            else
                            {
                                _logService.SendMessage(eqpid, portId, "SubStrate", $"{request.MapStr}", "info");
                            }
                            break;
                        }
                    case "EAPSTATE":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            State request = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var eapState = request.EAPState;
                            var eqpid = request.EQUIPMENTID;
                            var loadPort = int.Parse(request.LOADPORT);
                            DataFactory.Current.Equipment.ExecuteTransaction(db =>
                            {
                                DataFactory.Current.Equipment.Update(u => u.EqpId == eqpid, u => new Equipment
                                {
                                    EapStatus = (int)eapState
                                });
                                _logService.SendMessage(eqpid, "0", "EapState", ((int)eapState).ToString(), "info");
                                DataFactory.Current.PortInfo.Delete(u => u.eqpId == eqpid);
                                for (int i = 1; i <= loadPort; i++)
                                {
                                    DataFactory.Current.PortInfo.Insert(new PortInfo()
                                    {
                                        eqpId = eqpid,
                                        portId = i,
                                        portName = "LP" + i.ToString(),
                                        accessMode = (int)AccessMode.Manual,
                                        transferState = (int)TransferStatus.OutOfService
                                    });
                                }
                            });
                            break;
                        }
                    case "EQPSTATE":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            State request = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var eqpState = request.EQPState;
                            var eqpid = request.EQUIPMENTID;
                            DataFactory.Current.Equipment.Update(u => u.EqpId == eqpid, u => new Equipment
                            {
                                EqpStatus = (int)eqpState
                            });
                            _logService.SendMessage(eqpid, "0", "EqpState", ((int)eqpState).ToString(), "info");

                            ////始终采集的VID
                            //var dataRules1= DataFactory.Current.DataRule.GetList(u => u.eqpId == eqpid && u.triggerType==0);
                            //if (dataRules1?.Count > 0)
                            //{

                            //    if (AppCache.Current.EqpDataRule.ContainsKey(eqpid))
                            //        AppCache.Current.EqpDataRule.Remove(eqpid);
                            //    AppCache.Current.EqpDataRule.Add(eqpid, new Dictionary<string, DataRule>());

                            //    foreach (var rule in dataRules1)
                            //    {
                            //        AppCache.Current.EqpDataRule[eqpid].Add(rule.vid, rule);
                            //    }
                            //    //发送给EAP 开始采集


                            //}

                            ////根据事件采集的VID
                            //var dataRules2 = DataFactory.Current.DataRule.GetList(u => u.eqpId == eqpid && u.triggerType != 0);
                            //if (dataRules2?.Count > 0)
                            //{
                            //    if (AppCache.Current.EventDataRule.ContainsKey(eqpid))
                            //        AppCache.Current.EventDataRule.Remove(eqpid);
                            //    AppCache.Current.EventDataRule.Add(eqpid, new Dictionary<string, DataRule>());

                            //    foreach (var rule in dataRules2)
                            //    {
                            //        AppCache.Current.EqpDataRule[eqpid].Add(rule.vid, rule);
                            //    }
                            //}
                            break;
                        }
                    case "SENDCONTROLSTATE":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            State request = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var controlState = request.ControlState;
                            var eqpid = request.EQUIPMENTID;
                            DataFactory.Current.Equipment.Update(u => u.EqpId == eqpid, u => new Equipment
                            {
                                ControlState = (int)controlState
                            });
                            _logService.SendMessage(eqpid, "0", "ControlState", ((int)controlState).ToString(), "info");
                            break;
                        }
                    case "SENDEXCEPTION":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MessageBody request = XmlSerialization.DeserializeNodeObj<MessageBody>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);

                            var eqpid = request.EQUIPMENTID;
                            var portId = request.PORT;
                            var msg = request.MESSAGE;
                            var msgType = request.TYPE;
                            var parsedPortId = int.TryParse(portId, out var port) ? port : 0;
                            var eqpInfo = DataFactory.Current.Equipment.GetList(u => u.EqpId == eqpid).FirstOrDefault();
                            var portInfo = parsedPortId > 0
                                ? DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqpid && u.portId == parsedPortId).FirstOrDefault()
                                : null;
                            DataFactory.Current.ExceptionRecord.Insert(new ExceptionRecord()
                            {
                                eqpId = eqpid,
                                eqpType = eqpInfo?.EqpType ?? string.Empty,
                                portId = parsedPortId,
                                portName = portInfo?.portName ?? string.Empty,
                                lotId = portInfo?.lotId ?? string.Empty,
                                content = msg,
                                errorTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff"),
                                state = 0
                            });
                            break;
                        }
                    case "ACCESSMODE":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            LPAccessMode request = XmlSerialization.DeserializeNodeObj<LPAccessMode>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var accessMode = request.accessMode;
                            var eqpid = request.EQUIPMENTID;
                            var portId = request.portId;
                            DataFactory.Current.PortInfo.Update(u => u.eqpId == eqpid && u.portId == int.Parse(portId), u => new PortInfo
                            {
                                accessMode = (int)accessMode
                            });
                            _logService.SendMessage(eqpid, portId, "AccessMode", ((int)accessMode).ToString(), "info");
                            break;
                        }
                    case "TRANSFERSTATUS":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            LPTransferStatus request = XmlSerialization.DeserializeNodeObj<LPTransferStatus>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var transferStatus = request.transferStatus;
                            var eqpid = request.EQUIPMENTID;
                            var portId = request.portId;
                            DataFactory.Current.PortInfo.Update(u => u.eqpId == eqpid && u.portId == int.Parse(portId), u => new PortInfo
                            {
                                transferState = (int)transferStatus
                            });
                            _logService.SendMessage(eqpid, portId, "TransferState", ((int)transferStatus).ToString(), "info");

                            break;
                        }
                    case "MaterialRecived":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MaterialRecived request = XmlSerialization.DeserializeNodeObj<MaterialRecived>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var port = request.PORT;
                            var eqpid = request.EQUIPMENTID;
                            var carrierId = request.CARRIERID;
                            DataFactory.Current.PortInfo.Update(u => u.eqpId == eqpid && u.portId == port, u => new PortInfo
                            {
                                carrierId = carrierId
                            });
                            _logService.SendMessage(eqpid, port.ToString(), "Carrier", carrierId, "info");
                            break;
                        }
                    case "LPLotId":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MaterialRecived request = XmlSerialization.DeserializeNodeObj<MaterialRecived>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
                            var port = request.PORT;
                            var eqpid = request.EQUIPMENTID;
                            var lotIds = request.LotId;
                            DataFactory.Current.PortInfo.Update(u => u.eqpId == eqpid && u.portId == port, u => new PortInfo
                            {
                                lotId = lotIds
                            });
                            _logService.SendMessage(eqpid, port.ToString(), "lot", lotIds, "info");
                            break;
                        }
                    case "VIDVALUE":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            VID request = XmlSerialization.DeserializeNodeObj<VID>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);

                            var eqpid = request.EQUIPMENTID;
                            var vid = request.vID;
                            var type = request.type;
                            var value = request.value;
                            var name = request.name;
                            var abnormal = request.abnormal;
                            DataFactory.Current.VIDValue.Insert(new VIDValue()
                            {
                                eqpId = eqpid,
                                VId = vid,
                                type = type,
                                value = value,
                                name = name,
                                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                AbnormalFlag = (int)abnormal
                            });
                            break;
                        }
                }
            }
        }
    }
}
