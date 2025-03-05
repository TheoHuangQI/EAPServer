using EAP.HC.Core;
using EAP.HC.Core.Entities.MES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace EAP.RC.MES
{
    public class MESService
    {
        #region Send_MES_LotInfoDownloadRequest Lot信息请求
        /// <summary>
        /// Lot信息请求
        /// </summary>
        public static MesBatchInfo MES_LotInfoDownloadRequest(string carrierId)
        {
            try
            {
                var lotinfoStr = "<MESSAGE>\r\n <MESSAGE_ID>MES_LOTINFODOWNLOAD</MESSAGE_ID>\r\n <TID>MES_fast_track_in_2025.06.01_20:49:50</TID>\r\n <EQP_ID>AHEG07</EQP_ID>\r\n <FOUP_ID>FP0001</FOUP_ID>\r\n <OPERATOR>1004617</OPERATOR>\r\n <BATCH_ID>04617_BK1135.00_2025.06.01_20:49:50</BATCH_ID>\r\n <BATCH_SIZE>2</BATCH_SIZE>\r\n <LOTDATALIST>\r\n    <LOTDATA>\r\n\t  <LOT_ID>BK1135</LOT_ID>\r\n\t  <QTY>3</QTY>\r\n\t  <PPID>23001 BKM\\PROCESS\\20250305</PPID>\r\n\t  <WAFERS>\r\n    \t  <WAFER>\r\n\t\t  <WAFER_ID>BK1135.01</WAFER_ID>\r\n\t\t  <SLOT>1</SLOT>\r\n\t\t  </WAFER>\r\n\t\t  <WAFER> \r\n\t\t  <WAFER_ID>BK1135.02</WAFER_ID>\r\n\t\t  <SLOT>2</SLOT>\r\n\t\t  </WAFER>\r\n\t\t  <WAFER>\r\n\t\t  <WAFER_ID>BK1135.03</WAFER_ID>\r\n\t\t  <SLOT>3</SLOT>\r\n\t\t  </WAFER>\r\n\t  </WAFERS>\r\n\t</LOTDATA>\r\n\t<LOTDATA>\r\n\t  <LOT_ID>BK1136</LOT_ID>\r\n\t  <QTY>4</QTY>\r\n\t  <PPID>23001 BKM\\PROCESS\\20250305</PPID>\r\n\t  <WAFERS>\r\n    \t  <WAFER>\r\n\t\t  <WAFER_ID>BK1136.04</WAFER_ID>\r\n\t\t  <SLOT>4</SLOT>\r\n\t\t  </WAFER>\r\n\t\t  <WAFER> \r\n\t\t  <WAFER_ID>BK1136.05</WAFER_ID>\r\n\t\t  <SLOT>5</SLOT>\r\n\t\t  </WAFER>\r\n\t\t  <WAFER>\r\n\t\t  <WAFER_ID>BK1136.06</WAFER_ID>\r\n\t\t  <SLOT>6</SLOT>\r\n\t\t  </WAFER>\r\n\t  </WAFERS>\r\n\t</LOTDATA>\r\n  </LOTDATALIST>\r\n </MESSAGE>";
                MesBatchInfo mesBatchInfo = JsonUtils.DESerializer<MesBatchInfo>(lotinfoStr);
                return mesBatchInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        #endregion Send_MES_LotInfoDownloadRequest Lot信息请求
    }
}
