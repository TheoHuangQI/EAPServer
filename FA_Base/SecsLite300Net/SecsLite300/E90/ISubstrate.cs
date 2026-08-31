using System.Collections.Generic;
using SecsLite300.E87;

namespace SecsLite300.E90
{
	public interface ISubstrate
	{
		string LotID { get; }

		string SubstDestination { get; }

		SubstIDReadingState SubstIDState { get; }

		string SubstLocID { get; }

		SubstProcessingState SubstProcState { get; }

		string SubstSource { get; }

		SubstTransportState SubstState { get; }

		SubstrateType SubstType { get; }

		SubstUsage Usage { get; }

		ICarrier CurrentCarrier { get; }

		List<ISubstrateHistoryInfo> SubstrateHistory { get; }

		string AcquiredID { get; }

		string CurrentSlot { get; }

		string ProcessJobID { get; set; }

		int SubstMoveDone(ISubstrateLocation destLoc, SubstTransportState newState);

		void Start();

		int SubstProcessStateChanged(SubstProcessingState state);
	}
}
