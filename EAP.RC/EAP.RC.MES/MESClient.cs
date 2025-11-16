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
        #region MES_LotInfoDownloadRequest Lot信息请求
        /// <summary>
        /// Lot信息请求
        /// </summary>
        public async static Task<MESLotInfoDownloadReply> MES_LotInfoDownloadRequest(string eqpId,string carrierId)
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
                var xmlBody = XmlSerialization.SerializeOnlyBody(lotRequest);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData =await ActiveMQClient.Current.SendRequestAsync<MESLotInfoDownloadReply>(xmlMessage);
                if (replyData != null)
                {
                    return replyData;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        #endregion Send_MES_LotInfoDownloadRequest Lot信息请求

        #region MES_TrackIn 批次进站
        /// <summary>
        /// 批次进站
        /// </summary>
        public async static Task<bool> MES_TrackIn(string eqpId,string carrierId)
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
                mESLotTrackIn.LOTLIST = new List<string>();
                var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
                mESLotTrackIn.LOTLIST.AddRange(batchInfo.LOTLIST.Select(P => P.LOT_ID).ToList());

               var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res =await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
                return res==null? false :true;
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
        public async static Task<bool> MES_LotStart(string eqpId, string carrierId)
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
                mESLotTrackIn.LOTLIST = new List<string>();
                var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
                mESLotTrackIn.LOTLIST.AddRange(batchInfo.LOTLIST.Select(P => P.LOT_ID).ToList());

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res =await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
                return res==null ? false : true;
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
        public static async Task<bool> MES_LotEnd(string eqpId, string carrierId)
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
                mESLotTrackIn.LOTLIST = new List<string>();
                var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
                mESLotTrackIn.LOTLIST.AddRange(batchInfo.LOTLIST.Select(P => P.LOT_ID).ToList());

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res =await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
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
        public static async Task<bool> MES_TrackOut(string eqpId, string carrierId)
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
                mESLotTrackIn.LOTLIST = new List<string>();
                var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
                mESLotTrackIn.LOTLIST.AddRange(batchInfo.LOTLIST.Select(P => P.LOT_ID).ToList());

                var xmlBody = XmlSerialization.SerializeOnlyBody(mESLotTrackIn);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var res =await ActiveMQClient.Current.SendRequestAsync<MESLotTrackIn>(xmlMessage);
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
        public static void  Send_Message(string eqpId,string portId, string message, LogType logType)
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

        #region Send_MapResult 推送Map结果
        /// <summary>
        /// 批次开始
        /// </summary>
        public static void Send_MapResult(string eqpId, string portId, string Map)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "SENDMAPRESULT";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                MapResult msg = new MapResult();
                msg.EQUIPMENTID = eqpId;
                msg.MapStr = Map;
                msg.PORT = portId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<MapResult>(xmlMessage);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion Send_Message 消息推送

        #region Send_EapState 推送EAP 程序状态
        public static void Send_EapState(string eqpId,int LoadPort,EAPState state)
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
        public static void Send_EqpState(string eqpId, EQPState state)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "EQPSTATE";

                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                State msg = new State();
                msg.EQUIPMENTID = eqpId;
                msg.EQPState = state;
                var xmlBody = XmlSerialization.SerializeOnlyBody(msg);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                ActiveMQClient.Current.SendRequestAsync<State>(xmlMessage);
            }
            catch (Exception ex)
            {
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
        public static void Send_LPTransferStatus(string eqpId,string portId, TransferStatus status)
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
        public static void Send_MaterialRecived(string eqpId,int portId,string CarrierId)
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
