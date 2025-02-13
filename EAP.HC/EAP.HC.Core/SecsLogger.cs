using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public sealed class SecsLogger : ISecsGemLogger
    {
        private readonly Object _obj;
        public SecsLogger(Object obj)
        {
            _obj = obj;
        }

        public void MessageIn(SecsMessage msg, int id)
        {

        }

        public void MessageOut(SecsMessage msg, int id)
        {

        }

        public void Info(string msg)
        {

        }

        public void Warning(string msg)
        {

        }

        public void Error(string msg, SecsMessage? message, Exception? ex)
        {

        }

        public void Debug(string msg)
        {

        }

#if NET472
        public void Error(string msg)
        {
            Error(msg, null, null);
        }

        public void Error(string msg, Exception ex)
        {
            Error(msg, null, ex);
        }
#endif
    }
}
