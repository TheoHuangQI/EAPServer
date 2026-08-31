using System;

namespace SecsLite300.E90
{
	public class SubstHistoryInfo : ISubstrateHistoryInfo
	{
		public DateTime TimeIn { get; set; }

		public DateTime TimeOut { get; set; }

		public string SubstLocID { get; set; }
	}
}
