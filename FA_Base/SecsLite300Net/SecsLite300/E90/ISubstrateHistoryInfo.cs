using System;

namespace SecsLite300.E90
{
	public interface ISubstrateHistoryInfo
	{
		string SubstLocID { get; }

		DateTime TimeIn { get; }

		DateTime TimeOut { get; }
	}
}
