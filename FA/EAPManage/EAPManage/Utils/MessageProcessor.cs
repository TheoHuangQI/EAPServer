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
        private static readonly object EqpStateVersionLock = new();
        private static readonly Dictionary<string, long> EqpStateVersions = new();
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

        private int ParsePortId(string portId)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                return 0;
            }

            var text = portId.Trim().ToUpperInvariant();
            if (text.StartsWith("LP"))
            {
                text = text.Substring(2);
            }

            if (int.TryParse(text, out var parsed))
            {
                return parsed;
            }

            return 0;
        }

        private string ExtractMessageToken(string message, string tokenName)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(tokenName))
            {
                return string.Empty;
            }

            var token = tokenName + "=";
            var index = message.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return string.Empty;
            }

            var start = index + token.Length;
            var end = message.IndexOf(",", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                end = message.Length;
            }

            return message.Substring(start, end - start).Trim();
        }

        private bool IsCarrierClearEvent(int ceid)
        {
            return ceid == 15 || ceid == 72 || ceid == 10005;
        }

        private bool IsCarrierPresentEvent(int ceid)
        {
            return ceid == 14 || ceid == 37 || ceid == 59 || ceid == 10009;
        }

        private void ClearLoadPort(string eqpid, int portId)
        {
            if (portId <= 0)
            {
                return;
            }

            DataFactory.Current.PortInfo.Update(
                u => u.eqpId == eqpid && u.portId == portId,
                u => new PortInfo
                {
                    carrierId = string.Empty,
                    lotId = string.Empty,
                    slotMap = string.Empty,
                    pjSlot = string.Empty,
                    proComSlot = string.Empty
                });

            var port = portId.ToString();
            _logService.SendMessage(eqpid, port, "Carrier", string.Empty, "info");
            _logService.SendMessage(eqpid, port, "lot", string.Empty, "info");
            _logService.SendMessage(eqpid, port, "Map", string.Empty, "info");
        }

        private void UpdateCarrierIfPresent(string eqpid, int portId, string carrierId)
        {
            if (portId <= 0 || string.IsNullOrWhiteSpace(carrierId))
            {
                return;
            }

            DataFactory.Current.PortInfo.Update(
                u => u.eqpId == eqpid && u.portId == portId,
                u => new PortInfo { carrierId = carrierId });
            _logService.SendMessage(eqpid, portId.ToString(), "Carrier", carrierId, "info");
        }

        private void InsertOrRefreshAlarm(string eqpid, int portId, string alarmContent)
        {
            var eqpInfo = DataFactory.Current.Equipment.GetList(u => u.EqpId == eqpid).FirstOrDefault();
            var portInfo = portId > 0
                ? DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqpid && u.portId == portId).FirstOrDefault()
                : null;
            var existing = DataFactory.Current.ExceptionRecord
                .GetList(u => u.eqpId == eqpid && u.state == 0)
                .FirstOrDefault(u => (u.content ?? string.Empty).Contains(alarmContent.Split(';')[0]));

            if (existing != null)
            {
                return;
            }

            DataFactory.Current.ExceptionRecord.Insert(new ExceptionRecord()
            {
                eqpId = eqpid,
                eqpType = eqpInfo?.EqpType ?? string.Empty,
                portId = portId,
                portName = portInfo?.portName ?? string.Empty,
                lotId = portInfo?.lotId ?? string.Empty,
                content = alarmContent,
                errorTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff"),
                state = 0
            });
        }

        private void ClearAlarm(string eqpid, string alid)
        {
            if (string.IsNullOrWhiteSpace(alid))
            {
                return;
            }

            var token = "ALID=" + alid.Trim();
            var activeAlarms = DataFactory.Current.ExceptionRecord.GetList(u => u.eqpId == eqpid && u.state == 0);
            foreach (var alarm in activeAlarms.Where(u => (u.content ?? string.Empty).Contains(token)))
            {
                DataFactory.Current.ExceptionRecord.Update(
                    u => u.Id == alarm.Id,
                    u => new ExceptionRecord { state = 1 });
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

                                                        // 检查循环跑片状态：如果已停止(1)或暂停(2)，则拒绝下载新批次
                            // 按设备ID+PortID分别检查
                            if (AppCache.Current.LoopProcessState != null &&
                                AppCache.Current.LoopProcessState.ContainsKey(eqpid))
                            {
                                var portStates = AppCache.Current.LoopProcessState[eqpid];
                                var parsedPortId = int.TryParse(portId, out var pId) ? pId : 0;
                                if (portStates != null && portStates.ContainsKey(parsedPortId) && portStates[parsedPortId] != 0)
                                {
                                    var lpState = portStates[parsedPortId];
                                    var stateText = lpState == 2 ? "PAUSED" : "STOPPED";
                                    Console.WriteLine($"[LOTINFODOWNLOAD] eqp={eqpid} port={portId} LoopProcessState={lpState}, rejecting");
                                    res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                    var xmlMessage = XmlSerialization.SerializeOnlyBody(res);
                                    mrea.Reply(xmlMessage);
                                    break;
                                }
                            }

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
                                reply.PORTID = string.IsNullOrWhiteSpace(batch.First().PortId)
                                    ? portId?.Trim()
                                    : batch.First().PortId.Trim();
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
                    case "SENDEVENT":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MessageBody request = XmlSerialization.DeserializeNodeObj<MessageBody>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);

                            var eqpid = request.EQUIPMENTID;
                            var portId = string.IsNullOrWhiteSpace(request.PORT) ? "0" : request.PORT;
                            var parsedPortId = ParsePortId(portId);
                            var msg = request.MESSAGE;
                            var msgType = string.IsNullOrWhiteSpace(request.TYPE) ? "info" : request.TYPE.ToLower();
                            var carrierId = ExtractMessageToken(msg, "Carrier");

                            if (IsCarrierClearEvent(request.CEID))
                            {
                                ClearLoadPort(eqpid, parsedPortId);
                            }
                            else if (IsCarrierPresentEvent(request.CEID))
                            {
                                UpdateCarrierIfPresent(eqpid, parsedPortId, carrierId);
                            }

                            _logService.SendMessage(eqpid, portId, "Event", msg, msgType);
                            break;
                        }
                    case "SENDALARM":
                        {
                            res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            MessageBody request = XmlSerialization.DeserializeNodeObj<MessageBody>(message, "Request", "Body");
                            var xmlBody = XmlSerialization.SerializeOnlyBody(request);
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);

                            var eqpid = request.EQUIPMENTID;
                            var portId = string.IsNullOrWhiteSpace(request.PORT) ? "0" : request.PORT;
                            var parsedPortId = ParsePortId(portId);
                            var alid = string.IsNullOrWhiteSpace(request.ALID) ? "0" : request.ALID;
                            var alcd = string.IsNullOrWhiteSpace(request.ALCD) ? "0" : request.ALCD;
                            var altx = request.ALTX ?? string.Empty;
                            var alarmContent = $"ALID={alid}; ALCD={alcd}; {altx}".Trim();
                            var isAlarmSet = alcd != "0";

                            if (isAlarmSet)
                            {
                                InsertOrRefreshAlarm(eqpid, parsedPortId, alarmContent);
                                _logService.SendMessage(eqpid, portId, "Alarm", alarmContent, "error");
                            }
                            else
                            {
                                ClearAlarm(eqpid, alid);
                                _logService.SendMessage(eqpid, portId, "AlarmClear", alarmContent, "info");
                            }
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
                                        // LOTSTART is the transition from queued to execution;
                                        // new records must be visible as in progress immediately.
                                        state = (int)LotRunState.Running,
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

                                    var trackOutPortId = ParsePortId(mESLotTrackIn.PORTID);
                                    var resCode = DataFactory.Current.RunHistory.Update(u => u.eqpId == mESLotTrackIn.EQUIPMENTID
                                        && u.lotId == lot && u.endTime == null
                                        && (trackOutPortId <= 0 || u.portId == trackOutPortId), u => new RunHistory()
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
                            MapResult request = XmlSerialization.DeserializeNodeObj<MapResult>(message, "Request", "Body");
                            try
                            {
                                if (request == null
                                    || string.IsNullOrWhiteSpace(request.EQUIPMENTID)
                                    || !int.TryParse(request.PORT, out var portNo)
                                    || portNo <= 0
                                    || string.IsNullOrWhiteSpace(request.MapStr))
                                {
                                    throw new InvalidDataException(
                                        "SENDMAPRESULT requires equipment ID, a positive numeric port, and map data.");
                                }

                                var eqpid = request.EQUIPMENTID;
                                var portId = request.PORT;
                                if (request.MapStr.EndsWith(';'))
                                {
                                    var slotMap = request.MapStr.Substring(0, request.MapStr.Length - 1);
                                    DataFactory.Current.PortInfo.Update(
                                        u => u.eqpId == eqpid && u.portId == portNo,
                                        u => new PortInfo { slotMap = slotMap, proComSlot = string.Empty }
                                    );
                                    _logService.SendMessage(eqpid, portId, "Map", $"{slotMap}", "info");

                                }
                                else
                                {
                                    var portInfo = DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqpid && u.portId == portNo).FirstOrDefault();
                                    var completedSlots = (portInfo?.proComSlot ?? string.Empty)
                                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                                        .ToList();
                                    if (!completedSlots.Contains(request.MapStr))
                                    {
                                        completedSlots.Add(request.MapStr);
                                    }
                                    DataFactory.Current.PortInfo.Update(
                                        u => u.eqpId == eqpid && u.portId == portNo,
                                        u => new PortInfo { proComSlot = string.Join(';', completedSlots) }
                                    );
                                    _logService.SendMessage(eqpid, portId, "SubStrate", $"{request.MapStr}", "info");
                                }

                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            catch (Exception ex)
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                Console.WriteLine($"SENDMAPRESULT failed: {ex}");
                            }

                            var xmlBody = XmlSerialization.SerializeOnlyBody(request ?? new MapResult());
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);
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
                                // 删除前先读取旧的 loopProcessState
                                var oldPorts = DataFactory.Current.PortInfo.GetAll().Where(u => u.eqpId == eqpid).ToList();
                                var oldStates = new Dictionary<int, int>();
                                 var oldPortsById = oldPorts.ToDictionary(p => p.portId);
                                foreach (var op in oldPorts)
                                {
                                    oldStates[op.portId] = op.loopProcessState;
                                }
                                DataFactory.Current.PortInfo.Delete(u => u.eqpId == eqpid);
                                for (int i = 1; i <= loadPort; i++)
                                {
                                    var savedState = oldStates.ContainsKey(i) ? oldStates[i] : 0;
                                     oldPortsById.TryGetValue(i, out var oldPort);
                                     var partnerIsValid = oldPort != null
                                         && oldPort.jobMode == 1
                                         && oldPort.transferRole is 1 or 2
                                         && oldPort.transferPartnerPortId > 0
                                         && oldPort.transferPartnerPortId <= loadPort
                                         && oldPortsById.TryGetValue(oldPort.transferPartnerPortId, out var oldPartner)
                                         && oldPartner.jobMode == 1
                                         && oldPartner.transferRole is 1 or 2
                                         && oldPartner.transferRole != oldPort.transferRole
                                         && oldPartner.transferPartnerPortId == i;
                                    DataFactory.Current.PortInfo.Insert(new PortInfo()
                                    {
                                        eqpId = eqpid,
                                        portId = i,
                                        portName = "LP" + i.ToString(),
                                        accessMode = (int)AccessMode.Manual,
                                        transferState = (int)TransferStatus.OutOfService,
                                        loopProcessState = savedState,
                                        jobMode = partnerIsValid ? oldPort.jobMode : 0,
                                        transferRole = partnerIsValid ? oldPort.transferRole : 0,
                                        transferPartnerPortId = partnerIsValid ? oldPort.transferPartnerPortId : 0
                                    });
                                }
                            });
                            var ports = DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqpid);
                            TransferJobConfigPublisher.Publish(eqpid, ports);
                            foreach (var port in ports)
                            {
                                SendLoopProcessStateToEap(eqpid, port.portId, port.loopProcessState);
                            }
                            break;
                        }
                    case "EQPSTATE":
                        {
                            State request = XmlSerialization.DeserializeNodeObj<State>(message, "Request", "Body");
                            var accepted = false;
                            string failure = null;
                            if (request == null || string.IsNullOrWhiteSpace(request.EQUIPMENTID))
                            {
                                failure = "EQPSTATE requires an equipment ID.";
                            }
                            else
                            {
                                lock (EqpStateVersionLock)
                                {
                                    if (EqpStateVersions.TryGetValue(request.EQUIPMENTID, out var latestVersion)
                                        && request.ConnectionVersion < latestVersion)
                                    {
                                        failure = $"Ignored stale EQPSTATE. Version={request.ConnectionVersion}, Latest={latestVersion}.";
                                    }
                                    else
                                    {
                                        var eqpId = request.EQUIPMENTID;
                                        var updated = DataFactory.Current.Equipment.Update(
                                            u => u.EqpId == eqpId,
                                            u => new Equipment { EqpStatus = (int)request.EQPState });
                                        if (updated > 0)
                                        {
                                            EqpStateVersions[eqpId] = request.ConnectionVersion;
                                            _logService.SendMessage(eqpId, "0", "EqpState", ((int)request.EQPState).ToString(), "info");
                                            accepted = true;
                                        }
                                        else
                                        {
                                            failure = $"Equipment {eqpId} was not found while applying EQPSTATE.";
                                        }
                                    }
                                }
                            }

                            if (accepted)
                            {
                                res.mQHeader.RESULT = MQResult.SUCCESS.ToString();
                            }
                            else
                            {
                                res.mQHeader.RESULT = MQResult.FAILURE.ToString();
                                Console.WriteLine(failure);
                            }

                            var xmlBody = XmlSerialization.SerializeOnlyBody(request ?? new State());
                            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQReplyData>(res, xmlBody);
                            mrea.Reply(xmlMessage);

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
                            _logService.SendMessage(eqpid, portId, "Log", msg,
                                string.IsNullOrEmpty(msgType) ? "error" : msgType.ToLower());
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

        private static void SendLoopProcessStateToEap(string eqpId, int portId, int state)
        {
            try
            {
                var xmlMessage = $@"<Request>
                    <Header><MESSAGENAME>LOOPPROCESSSTATE</MESSAGENAME></Header>
                    <Body>
                        <EQUIPMENTID>{eqpId}</EQUIPMENTID>
                        <PORTID>{portId}</PORTID>
                        <STATE>{state}</STATE>
                    </Body>
                </Request>";
                ActiveMQClient.Current.SendAsync(xmlMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to sync loop process state. EqpId={eqpId}, PortId={portId}, Error={ex.Message}");
            }
        }
    }
}
