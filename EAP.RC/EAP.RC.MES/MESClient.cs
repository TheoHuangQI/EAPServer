using EAP.HC.Core;
using EAP.HC.Core.Entities.MES;
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
        public static MESLotInfoDownloadReply MES_LotInfoDownloadRequest(string eqpId,string carrierId)
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
                var replyHeader = new MQHeader();
                var replyData =ActiveMQClient.Current.SendRequest<MESLotInfoDownloadReply>(xmlMessage,out replyHeader);
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
        public static bool MES_TrackIn(string eqpId,string carrierId)
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
                var replyHeader = new MQHeader();
                var res = ActiveMQClient.Current.SendRequest<MESLotTrackIn>(xmlMessage, out replyHeader);
                return replyHeader.RESULT==MQResult.SUCCESS.ToString()?true:false;
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
        public static bool MES_LotStart(string eqpId, string carrierId)
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
                var replyHeader = new MQHeader();
                var res = ActiveMQClient.Current.SendRequest<MESLotTrackIn>(xmlMessage, out replyHeader);
                return replyHeader.RESULT == MQResult.SUCCESS.ToString() ? true : false;
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
        public static bool MES_LotEnd(string eqpId, string carrierId)
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
                var replyHeader = new MQHeader();
                var res = ActiveMQClient.Current.SendRequest<MESLotTrackIn>(xmlMessage, out replyHeader);
                return replyHeader.RESULT == MQResult.SUCCESS.ToString() ? true : false;
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
        public static bool MES_TrackOut(string eqpId, string carrierId)
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
                var replyHeader = new MQHeader();
                var res = ActiveMQClient.Current.SendRequest<MESLotTrackIn>(xmlMessage, out replyHeader);
                return replyHeader.RESULT == MQResult.SUCCESS.ToString() ? true : false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion MES_TrackOut 批次结束

    }
}
