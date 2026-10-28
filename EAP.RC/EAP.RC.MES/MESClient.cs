using EAP.HC.Core;
using EAP.HC.Core.Entities.MES;
using EAPManage.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace EAP.RC.MES
{
    public class MESClient
    {
        private static string NormalizePortId(string portId)
        {
            var value = portId?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.StartsWith("LP", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(2).Trim();

            return int.TryParse(value, out var number)
                ? number.ToString()
                : value.ToUpperInvariant();
        }

        private static MESLotInfoDownloadReply FindBatchInfo(string carrierId, string portID)
        {
            var batchInfos = AppCache.Current?.MesBatchInfos?.Values
                ?? Enumerable.Empty<MESLotInfoDownloadReply>();
            var transferBatchInfos = AppCache.Current?.TransferBatchInfos?.Values
                ?? Enumerable.Empty<MESLotInfoDownloadReply>();
            var allBatchInfos = batchInfos.Concat(transferBatchInfos).Where(batch => batch != null).ToList();
            if (allBatchInfos.Count == 0)
            {
                return null;
            }

            var normalizedCarrierId = carrierId?.Trim();
            var normalizedPortId = NormalizePortId(portID);
            MESLotInfoDownloadReply batchInfo = null;
            if (!string.IsNullOrWhiteSpace(normalizedCarrierId) && !string.IsNullOrWhiteSpace(normalizedPortId))
            {
                batchInfo = allBatchInfos.FirstOrDefault(batch =>
                    batch != null &&
                    string.Equals(batch.CARRIERID?.Trim(), normalizedCarrierId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizePortId(batch.PORTID), normalizedPortId, StringComparison.OrdinalIgnoreCase));
            }

            if (batchInfo == null && !string.IsNullOrWhiteSpace(normalizedCarrierId))
            {
                batchInfo = allBatchInfos.FirstOrDefault(batch =>
                    batch != null &&
                    string.Equals(batch.CARRIERID?.Trim(), normalizedCarrierId, StringComparison.OrdinalIgnoreCase));
            }

            if (batchInfo == null && !string.IsNullOrWhiteSpace(normalizedPortId))
            {
                batchInfo = allBatchInfos.FirstOrDefault(batch =>
                batch != null &&
                string.Equals(NormalizePortId(batch.PORTID), normalizedPortId, StringComparison.OrdinalIgnoreCase));
            }

            if (batchInfo != null && string.IsNullOrWhiteSpace(batchInfo.PORTID)
                && !string.IsNullOrWhiteSpace(normalizedPortId))
            {
                // Repair in-memory entries created before PORTID was included in the reply.
                batchInfo.PORTID = normalizedPortId;
            }
            return batchInfo;
        }

        private static bool TryGetLotIds(string carrierId, string portID, out List<string> lotIds)
        {
            lotIds = new List<string>();
            var batchInfo = FindBatchInfo(carrierId, portID);
            if (batchInfo?.LOTLIST == null)
            {
                return false;
            }

            lotIds.AddRange(batchInfo.LOTLIST
                .Where(lot => lot != null && !string.IsNullOrWhiteSpace(lot.LOT_ID))
                .Select(lot => lot.LOT_ID)
                .ToList());

            return lotIds.Count > 0;
        }

        public static Task<bool> MES_TrackInLots(string eqpId, string portID, string carrierId, IEnumerable<string> lotIds) =>
            SendLotTrackingRequestAsync("TRACKIN", eqpId, portID, carrierId, lotIds);

        public static Task<bool> MES_LotStartLots(string eqpId, string portID, string carrierId, IEnumerable<string> lotIds) =>
            SendLotTrackingRequestAsync("LOTSTART", eqpId, portID, carrierId, lotIds);

        public static Task<bool> MES_LotEndLots(string eqpId, string portID, string carrierId, IEnumerable<string> lotIds) =>
            SendLotTrackingRequestAsync("LOTEND", eqpId, portID, carrierId, lotIds);

        public static Task<bool> MES_TrackOutLots(string eqpId, string portID, string carrierId, IEnumerable<string> lotIds) =>
            SendLotTrackingRequestAsync("LOTTRACKOUT", eqpId, portID, carrierId, lotIds);

        private static async Task<bool> SendLotTrackingRequestAsync(
            string messageName, string eqpId, string portID, string carrierId, IEnumerable<string> lotIds)
        {
            try
            {
                var normalizedLotIds = (lotIds ?? Enumerable.Empty<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
                if (normalizedLotIds.Count == 0)
                    return false;

                var request = new MQData
                {
                    mQHeader = new MQHeader { MESSAGENAME = messageName }
                };
                var body = new MESLotTrackIn
                {
                    EQUIPMENTID = eqpId,
                    PORTID = portID,
                    CARRIERID = carrierId,
                    LOTLIST = normalizedLotIds
                };
                var xml = XmlSerialization.XmlSerializeMessage<MQData>(request, XmlSerialization.SerializeOnlyBody(body));
                return await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xml) != null;
            }
            catch
            {
                return false;
            }
        }

        #region Send_SVIDValue 推送SVIDValue
        public static void Send_SVIDValue(string eqpId, VID vID)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "VIDVALUE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                vID.EQUIPMENTID = eqpId;

                var xmlBody = XmlSerialization.SerializeOnlyBody(vID);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<LPAccessMode>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_SVIDValue 推送SVIDValue

        #region MES_LotInfoDownloadRequest Lot信息请求
        /// <summary>
        /// Lot信息请求
        /// </summary>
        public async static Task<MESLotInfoDownloadReply> MES_LotInfoDownloadRequest(string eqpId, string carrierId, string portID)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "LOTINFODOWNLOAD";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MESLotInfoDownloadRequest lotRequest = new MESLotInfoDownloadRequest();
                lotRequest.EQUIPMENTID = eqpId;
                lotRequest.CARRIERID = carrierId;
                lotRequest.PORTID = portID;
                var xmlBody = XmlSerialization.SerializeOnlyBody(lotRequest);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData = await ActiveMQClient.Current.SendRequestAsync<MESLotInfoDownloadReply>(xmlMessage);
                return NormalizeLotInfoDownloadReply(replyData, portID);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static MESLotInfoDownloadReply NormalizeLotInfoDownloadReply(
            MESLotInfoDownloadReply replyData, string requestedPortId)
        {
            if (replyData == null)
                return null;

            // Older MES responders may not emit PORTID. The request's port identifies
            // the batch lookup in that case and keeps the local cache searchable.
            replyData.PORTID = string.IsNullOrWhiteSpace(replyData.PORTID)
                ? requestedPortId?.Trim()
                : replyData.PORTID.Trim();
            return replyData;
        }

        #endregion Send_MES_LotInfoDownloadRequest Lot信息请求

        #region MES_TrackIn 批次进站
        /// <summary>
        /// 批次进站
        /// </summary>
        public async static Task<bool> MES_TrackIn(string eqpId, string portID, string carrierId)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "TRACKIN";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MESLotTrackIn mESLotTrackIn = new MESLotTrackIn();
                mESLotTrackIn.EQUIPMENTID = eqpId;
                mESLotTrackIn.CARRIERID = carrierId;
                mESLotTrackIn.PORTID = portID;
                mESLotTrackIn.LOTLIST = new List<string>();
                if (!TryGetLotIds(carrierId, portID, out var lotIds))
                {
                    return false;
                }
                mESLotTrackIn.LOTLIST.AddRange(lotIds);

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res = await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
                return res == null ? false : true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion MES_LotStart 批次开始

        #region MES_LotStart 批次开始
        /// <summary>
        /// 批次开始
        /// </summary>
        public async static Task<bool> MES_LotStart(string eqpId, string portID, string carrierId)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "LOTSTART";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MESLotTrackIn mESLotTrackIn = new MESLotTrackIn();
                mESLotTrackIn.EQUIPMENTID = eqpId;
                mESLotTrackIn.CARRIERID = carrierId;
                mESLotTrackIn.PORTID = portID;
                mESLotTrackIn.LOTLIST = new List<string>();
                if (!TryGetLotIds(carrierId, portID, out var lotIds))
                {
                    return false;
                }
                mESLotTrackIn.LOTLIST.AddRange(lotIds);

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res = await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
                return res == null ? false : true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion MES_LotStart 批次结束

        #region MES_LotEnd 批次结束
        /// <summary>
        /// 批次开始
        /// </summary>
        public static async Task<bool> MES_LotEnd(string eqpId, string portID, string carrierId)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "LOTEND";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MESLotTrackIn mESLotTrackIn = new MESLotTrackIn();
                mESLotTrackIn.EQUIPMENTID = eqpId;
                mESLotTrackIn.CARRIERID = carrierId;
                mESLotTrackIn.PORTID = portID;
                mESLotTrackIn.LOTLIST = new List<string>();
                if (!TryGetLotIds(carrierId, portID, out var lotIds))
                {
                    return false;
                }
                mESLotTrackIn.LOTLIST.AddRange(lotIds);

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res = await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
                return res == null ? false : true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion MES_TrackOut 批次出站

        #region MES_TrackOut 批次出站
        /// <summary>
        /// 批次开始
        /// </summary>
        public static async Task<bool> MES_TrackOut(string eqpId, string portID, string carrierId)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "LOTTRACKOUT";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MESLotTrackIn mESLotTrackIn = new MESLotTrackIn();
                mESLotTrackIn.EQUIPMENTID = eqpId;
                mESLotTrackIn.CARRIERID = carrierId;
                mESLotTrackIn.PORTID = portID;
                mESLotTrackIn.LOTLIST = new List<string>();
                if (!TryGetLotIds(carrierId, portID, out var lotIds))
                {
                    return false;
                }
                mESLotTrackIn.LOTLIST.AddRange(lotIds);

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res = await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
                return res == null ? false : true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion MES_TrackOut 批次结束

        #region Send_Message 消息推送
        /// <summary>
        /// 批次开始
        /// </summary>
        public static void Send_Message(string eqpId, string portId, string message, LogType logType)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDMESSAGE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MessageBody msg = new MessageBody();
                msg.EQUIPMENTID = eqpId;
                msg.MESSAGE = message;
                msg.TYPE = logType.ToString();
                msg.PORT = portId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<MessageBody>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_Message 消息推送

        #region Send_Event 推送设备事件
        public static void Send_Event(string eqpId, string portId, int ceid, string eventName, string message, LogType logType)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDEVENT";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MessageBody msg = new MessageBody();
                msg.EQUIPMENTID = eqpId;
                msg.PORT = portId;
                msg.CEID = ceid;
                msg.EVENTNAME = eventName;
                msg.MESSAGE = message;
                msg.TYPE = logType.ToString();
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<MessageBody>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_Event 推送设备事件

        #region Send_Alarm 推送设备报警
        public static void Send_Alarm(string eqpId, string portId, byte alcd, uint alid, string altx, bool isSet)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDALARM";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MessageBody msg = new MessageBody();
                msg.EQUIPMENTID = eqpId;
                msg.PORT = portId;
                msg.MESSAGE = isSet ? "Alarm Set" : "Alarm Clear";
                msg.TYPE = isSet ? LogType.ERROR.ToString() : LogType.INFO.ToString();
                msg.ALCD = alcd.ToString();
                msg.ALID = alid.ToString();
                msg.ALTX = altx;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<MessageBody>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_Alarm 推送设备报警

        #region Send_MapResult 推送Map结果
        /// <summary>
        /// 批次开始
        /// </summary>
        public static async Task Send_MapResultAsync(string eqpId, string portId, string map)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eqpId) || string.IsNullOrWhiteSpace(portId)
                    || string.IsNullOrWhiteSpace(map))
                {
                    Logger.Inst().Warn($"Skip SENDMAPRESULT because required values are empty. "
                        + $"EqpId={eqpId ?? "<null>"} PortId={portId ?? "<null>"} Map={map ?? "<null>"}");
                    return;
                }

                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDMAPRESULT";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MapResult msg = new MapResult();
                msg.EQUIPMENTID = eqpId;
                msg.MapStr = map;
                msg.PORT = portId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var reply = await ActiveMQClient.Current.SendRequestAsync<MapResult>(xmlMessage).ConfigureAwait(false);
                if (reply == null)
                {
                    Logger.Inst().Warn($"SENDMAPRESULT did not receive a successful reply. EqpId={eqpId} "
                        + $"PortId={portId} Map={map}");
                }
            }
            catch (Exception ex)
            {
                Logger.Inst().Error($"SENDMAPRESULT failed. EqpId={eqpId ?? "<null>"} "
                    + $"PortId={portId ?? "<null>"} Map={map ?? "<null>"} Exception={ex}");
            }
        }
        #endregion Send_Message 消息推送

        #region Send_EapState 推送EAP 程序状态
        public static void Send_EapState(string eqpId, int LoadPort, EAPState state)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "EAPSTATE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                State msg = new State();
                msg.EQUIPMENTID = eqpId;
                msg.EAPState = state;
                msg.LOADPORT = LoadPort.ToString();
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<State>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_EapState 推送EAP 程序状态

        #region Send_EqpState 推送设备连接状态
        public static async Task<bool> Send_EqpStateAsync(string eqpId, EQPState state, long connectionVersion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eqpId))
                    return false;

                MQHeader header = new MQHeader();
                header.MESSAGENAME = "EQPSTATE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                State msg = new State();
                msg.EQUIPMENTID = eqpId;
                msg.EQPState = state;
                msg.ConnectionVersion = connectionVersion;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var reply = await ActiveMQClient.Current.SendRequestAsync<State>(xmlMessage).ConfigureAwait(false);
                if (reply == null)
                {
                    Logger.Inst().Warn($"EQPSTATE was not acknowledged. EqpId={eqpId} State={state} Version={connectionVersion}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Inst().Error($"EQPSTATE send failed. EqpId={eqpId} State={state} "
                    + $"Version={connectionVersion} Exception={ex}");
                return false;
            }
        }
        #endregion Send_EqpState 推送设备连接状态

        #region Send_ControlState 推送设备控制状态
        public static void Send_ControlState(string eqpId, ControlState state)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDCONTROLSTATE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                State msg = new State();
                msg.EQUIPMENTID = eqpId;
                msg.ControlState = state;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<State>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_ControlState 

        #region Send_LPTransferStatus 推送LP Transfer 状态
        public static void Send_LPTransferStatus(string eqpId, string portId, TransferStatus status)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "TRANSFERSTATUS";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                LPTransferStatus msg = new LPTransferStatus();
                msg.EQUIPMENTID = eqpId;
                msg.transferStatus = status;
                msg.portId = portId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<LPAccessMode>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_LPTransferStatus 推送LP Transfer 状态

        #region Send_LPAcessMode 推送LP Access状态
        public static void Send_LPAcessMode(string eqpId, string portId, AccessMode mode)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "ACCESSMODE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                LPAccessMode msg = new LPAccessMode();
                msg.EQUIPMENTID = eqpId;
                msg.accessMode = mode;
                msg.portId = portId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<LPAccessMode>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_LPAcessMode 推送LP Access状态

        #region Send_Exception 推送异常记录
        public static void Send_Exception(string eqpId, string portId, string message)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDEXCEPTION";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MessageBody msg = new MessageBody();
                msg.EQUIPMENTID = eqpId;
                msg.MESSAGE = message;
                msg.TYPE = LogType.ERROR.ToString();
                msg.PORT = portId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<MessageBody>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_Exception 推送异常记录

        #region Send_MaterialRecived 推送物料到达信息
        public static void Send_MaterialRecived(string eqpId, int portId, string CarrierId)
        {
            MQHeader header = new MQHeader();
            header.MESSAGENAME = "MaterialRecived";

            var requestMessage = new MQData();
            requestMessage.mQHeader = header;

            MaterialRecived msg = new MaterialRecived();
            msg.EQUIPMENTID = eqpId;
            msg.CARRIERID = CarrierId;
            msg.PORT = portId;
            var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
            ActiveMQClient.Current.SendRequestAsync<MaterialRecived>(xmlMessage);
        }
        #endregion Send_MaterialRecived 推送物料到达信息

        #region Send_MaterialRecived 推送物料到达信息
        public static void Send_LPLotId(string eqpId, int portId, string lotId)
        {
            MQHeader header = new MQHeader();
            header.MESSAGENAME = "LPLotId";

            var requestMessage = new MQData();
            requestMessage.mQHeader = header;

            MaterialRecived msg = new MaterialRecived();
            msg.EQUIPMENTID = eqpId;
            msg.LotId = lotId;
            msg.PORT = portId;
            var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
            var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
            ActiveMQClient.Current.SendRequestAsync<MaterialRecived>(xmlMessage);
        }
        #endregion Send_MaterialRecived 推送物料到达信息
    }
}
