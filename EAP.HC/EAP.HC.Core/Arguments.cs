using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class Arguments
    {

        private Dictionary<string, object> header;
        private string resultCode;
        private string resultMsg;
        private PrimaryMessageWrapper secsdata;



        public PrimaryMessageWrapper SecsData
        {
            get
            {
                return secsdata;
            }
            set
            {
                secsdata = value;
            }
        }
        public Dictionary<string, object> Header
        {
            get
            {
                return header;
            }
            set
            {
                header = value;
            }
        }

        public void SetResultOK()
        {
            resultCode = "OK";
            resultMsg = "OK";
        }

        public void SetResultNG(string msg="NG")
        {
            resultCode = "NG";
            resultMsg = msg;
        }

        public string GetResultCode()
        {
            return resultCode;
        }

        public string GetResultMsg()
        {
            return resultMsg;
        }

     
    }
}
