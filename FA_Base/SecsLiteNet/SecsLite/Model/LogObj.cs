using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsLite.Model
{
   public class LogObj
    {
        public  string Time { get; set; }
        public byte stream { get; set; }
        public byte func { get; set; }
        public SecsItem item { get; set; }
        public uint msgId { get; set; }
        public bool fromHost { get; set; }
    }
}
