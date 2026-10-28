using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EAPManage.Utils;

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
            if (msg.S == 1 && msg.F == 4)
            {
                Logger.Inst().Info($"SECS In S1F4 MessageId=0x{unchecked((uint)id):X8}");
            }
        }

        public void MessageOut(SecsMessage msg, int id)
        {
            if (msg.Name?.StartsWith("SVID29:", StringComparison.Ordinal) == true)
            {
                Logger.Inst().Info($"S1F3/SVID29 Sent QueryId={msg.Name.Substring(7)} MessageId=0x{unchecked((uint)id):X8}");
            }
        }

        public void Info(string msg)
        {

        }

        public void Warning(string msg)
        {
            Logger.Inst().Warn(msg);
        }

        public void Error(string msg, SecsMessage? message, Exception? ex)
        {
            Logger.Inst().Error($"{msg} Message={message} Exception={ex}");
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
