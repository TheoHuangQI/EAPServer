
using EAPManage.Models.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAPManage.Utils
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
                EqpDataRule = new Dictionary<string, Dictionary<string, DataRule>>();
                EventDataRule = new Dictionary<string, Dictionary<string, DataRule>>();
            }
            catch (Exception)
            {

            }
        }



        public Dictionary<string, Dictionary<string,DataRule>> EqpDataRule;

        public Dictionary<string, Dictionary<string,DataRule>> EventDataRule;
    }
}
