using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class AppCache : NotifyPropertyEntity
    {
        #region 单例对象定义
        private static readonly Lazy<AppCache> Lazy = new Lazy<AppCache>(() => new AppCache());
        public static AppCache Current => Lazy.Value;
        private AppCache()
        {

        }
        #endregion

        public void InitializeObjectCache()
        {
            try
            {
                Current.EqpInfos = new Dictionary<string, EqpInfo>();
                Current.MesBatchInfos = new Dictionary<string, MESLotInfoDownloadReply>();
                Current.TransferBatchInfos = new ConcurrentDictionary<string, MESLotInfoDownloadReply>();
                Current.CJQuendList = new Dictionary<string, Dictionary<string,int>>();
                Current.LoopProcessState = new Dictionary<string, Dictionary<int, int>>();
                Current.TransferJobStates = new ConcurrentDictionary<string, TransferJobState>();
                //读配置文件
            }
            catch (Exception)
            {

            }
        }


        /// <summary>
        /// 当前设备列表信息
        /// </summary>
        public Dictionary<string,EqpInfo> EqpInfos { get; set; }

        /// <summary>
        /// MES lot 信息
        /// </summary>
        public Dictionary<string, MESLotInfoDownloadReply> MesBatchInfos { get; set; }
        public ConcurrentDictionary<string, MESLotInfoDownloadReply> TransferBatchInfos { get; set; }
        public Dictionary<string,Dictionary<string,int>> CJQuendList { get; set; }

                /// <summary>
        /// 循环跑片控制状态（按设备ID+PortID）
        /// 外层key=eqpId, 内层key=portId
        /// value: 0 = 正常运行, 1 = 停止, 2 = 暂停
        /// </summary>
        public Dictionary<string, Dictionary<int, int>> LoopProcessState { get; set; }

        /// <summary>Transfer runtime state keyed by equipment and source LP.</summary>
        public ConcurrentDictionary<string, TransferJobState> TransferJobStates { get; set; }
    }
}
