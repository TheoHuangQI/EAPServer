using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAPManage.Utils
{
    public class NotifyPropertyEntity : INotifyPropertyChanged
    {
        protected PropertyChangedEventHandler PropertyChangedHandler => this.PropertyChanged;

        public event PropertyChangedEventHandler PropertyChanged;
        public static object fileLock = new object();
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            try
            {
                lock (fileLock)
                {
                    /* if (AppCache.Current.Dicts != null && AppCache.Current.Dicts.Count > 0)
                     {
                         File.WriteAllText("systemCache.cache", JsonConvert.SerializeObject(AppCache.Current.Dicts));
                     }*/
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }
    }
}
