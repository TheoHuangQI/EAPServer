namespace SecsLite300.E87
{
	public interface ILoadPort
	{
		LoadPortTransferStatus TransferStatus { get; }

		string PortName { get; }

		byte PortID { get; }

		LoadPortAccessMode AccessMode { get; }

		PortAssociationStatus PortAssociationState { get; set; }

		ICarrier CurrentCarrier { get; }

		bool BypassReadID { get; set; }

		bool ReservationState { get; }
		OnOffState ClampState { get; set; }
		int AlarmAccessMode(OnOffState onOffState);

		int AlarmCarrierPlacement(OnOffState onOffState);

		int AlarmLoadPortOutOfService(OnOffState onOffState);

		int AlarmPIO(OnOffState onOffState);

		int AMHSArrived();

		int ClearPort();

		ICarrier LoadTransferCompleted();

		int TransferCommitted();

		int TransferStopped();

		int UnloadTransferCompleted();

		void Release();
	}
}
