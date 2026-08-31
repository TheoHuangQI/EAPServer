using SecsLite300.E90;

namespace SecsLite300.E87
{
	public interface ICarrier
	{
		string CarrierID { get; }

		CarrierIDStatus IDStatus { get; }

		CarrierAccessingStatus AccessingStatus { get; }

		CarrierSlotMapStatus SlotMapStatus { get; }

		int Capacity { get; }

		string LocationID { get; }

		uint SubstrateCount { get; }

		string Usage { get; }

		CarrierSlotInfo[] ContentMapArray { get; }

		SlotState[] SlotMapArray { get; }

		ISubstrateLocation[] CarrierSubstrateLocations { get; }

		ILoadPort CurrentLoadPort { get; }

		int AccessCompleted();

		int AccessStarted();

		int AccessStopped();

		int CarrierApproachingComplete();

		int CarrierEject();

		int CarrierIdVerify(DataStatus status, string id);

		int CarrierMoveCompleted();

		int CarrierMoveStarted(int destination);

		int CarrierMoveStopped();

		int ClampStateChanged(OnOffState clamped);

		int RemoveCarrier();

		int DoorClosed();

		int DoorOpened();

		int SlotMapVerify(DataStatus status, ref SlotState[] slotmap, int capacity);
	}
}
