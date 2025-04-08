using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
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
                Current.PjQuendList = new List<string>();
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

        public List<string> PjQuendList { get; set; }
    }
}
