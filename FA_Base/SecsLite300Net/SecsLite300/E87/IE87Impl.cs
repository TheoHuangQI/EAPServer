using System.Collections.Generic;
using SecsLite;
using SecsLite300.E90;

namespace SecsLite300.E87
{
	internal interface IE87Impl
	{
		int OnCancelBind(string carrierId, ILoadPort loadPort);

		CAACK OnCancelCarrier(ICarrier carrier);

		int OnCancelCarrierNotification(string carrierId);

		int OnCancelTransfer(ILoadPort loadPort);

		void OnCarrierActive(ICarrier carrier, OnOffState onOffState);

		int OnCarrierBindOrNotify(ILoadPort loadPort, int capacity, string carrierId);

		int OnCarrierCreate(ICarrier carrier);

		int OnCarrierDelete(ICarrier carrier);

		int OnCarrierIn(ICarrier carrier);

		int OnCarrierOut(ICarrier carrier, ILoadPort loadPort);

		int OnCarrierOutEntry(ICarrier carrier, ILoadPort loadPort);

		void OnCarrierReCreate(ILoadPort loadPort);

		int OnCarrierTagRead(ICarrier carrier, int location, string carrierSpec, ref string data);

		CAACK OnCarrierTagReleased(ICarrier carrier, ILocation location);

		int OnCarrierTagWrite(ICarrier carrier, int location, string carrierSpec, string dataSeg, string data);

		void OnCarrierUnlocked(ICarrier carrier);

		void OnIdVerifyFailed(ICarrier carrier);

		void OnIdVerifyOK(ICarrier carrier, string carrierId);

		void OnLoadFromAMHS(ILoadPort loadPort);

		void OnLoadFromOperator(ILoadPort loadPort);

		void OnLoadPortActive(ILoadPort loadPort, OnOffState onOffState);

		int OnLocationCreate(ILocation location);

		int OnLocationDelete(ILocation location);

		int OnPortCreate(ILoadPort loadPort);

		int OnPortDelete(ILoadPort loadPort);

		void OnSetReservationVisibleSignal(ILoadPort loadPort, OnOffState state);

		void OnSlotMapVerifyFailed(ICarrier carrier);

		void OnSlotMapVerifyOK(ICarrier carrier, ref SlotState[] slotMap, int capacity, ref List<ISubstrate> substrateMapList, ref ISubstrateLocation[] substLocMap);

		int OnUnloadToAMHS(ILoadPort loadPort, UnclampMethod unclampMethod);

		void OnUnloadToOperator(ILoadPort loadPort);
	}
}
