using SecsLite.Gem;
using SecsLite.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecsLite.Model
{
    internal class DataTraceInfo
	{
        public string TRID = "";

        public SecsItem RawTRID = null;

        public int DSPER = 0;

        public uint TOTSMP = 0u;

        public uint REPGSZ = 0u;

        public uint[] SVIDArray = null;

        public ManualResetEvent StopEvent = null;

        public Thread SamplingThread = null;

        public GemEquipment EQ = null;
    }
}
