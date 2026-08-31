namespace SecsLite300
{
	public static class FA300Event
	{
		#region E94
		public static readonly string E94_CJ_01_NoStateToQueued = "E94_CJ_01_NoStateToQueued";
		public static readonly string E94_CJ_02_QueuedToNoState = "E94_CJ_02_QueuedToNoState";
		public static readonly string E94_CJ_03_QueuedToSelected = "E94_CJ_03_QueuedToSelected";
		public static readonly string E94_CJ_04_SelectedToQueued = "E94_CJ_04_SelectedToQueued";
		public static readonly string E94_CJ_05_SelectedToExecuting = "E94_CJ_05_SelectedToExecuting";
		public static readonly string E94_CJ_06_SelectedToWaitingforstart = "E94_CJ_06_SelectedToWaitingforstart";
		public static readonly string E94_CJ_07_WaitingforstartToExecuting = "E94_CJ_07_WaitingforstartToExecuting";
		public static readonly string E94_CJ_08_ExecutingToPaused = "E94_CJ_08_ExecutingToPaused";
		public static readonly string E94_CJ_09_PausedToExecuting = "E94_CJ_09_PausedToExecuting";
		public static readonly string E94_CJ_10_ExecutingToCompleted = "E94_CJ_10_ExecutingToCompleted";
		public static readonly string E94_CJ_11_ActiveToCompletedStop = "E94_CJ_11_ActiveToCompletedStop";
		public static readonly string E94_CJ_12_ActiveToCompletedAbort = "E94_CJ_12_ActiveToCompletedAbort";
		public static readonly string E94_CJ_13_CompletedToNoState = "E94_CJ_13_CompletedToNoState";
		public static readonly string SelectedCjSetEmptied = "SelectedCjSetEmptied";
		#endregion

		#region E40

		public static readonly string PRJobSetup = "PRJobSetup";

		public static readonly string PRJobProcessing = "PRJobProcessing";

		public static readonly string PRJobProcessingComplete = "PRJobProcessingComplete";

		public static readonly string PRJobComplete = "PRJobComplete";

		public static readonly string PRJobWaitingForStart = "PRJobWaitingForStart";

		public static readonly string PRJobWaitingForMaterial = "PRJobWaitingForMaterial";

		public static readonly string PRJobStateChange = "PRJobStateChange";


		public static readonly string E40_01_NostateToQueued = "E40_01_NostateToQueued";
		public static readonly string E40_02_QueuedToSettingUP = "E40_02_QueuedToSettingUP";
		public static readonly string E40_03_SettingUPToWaittingForStart = "E40_03_SettingUPToWaittingForStart";
		public static readonly string E40_04_SettingUPToProcessing = "E40_04_SettingUPToProcessing";
		public static readonly string E40_05_WaittingForStartToProcessing = "E40_05_WaittingForStartToProcessing";
		public static readonly string E40_06_ProcessingToProcessComplete = "E40_06_ProcessingToProcessComplete";
		public static readonly string E40_07_ProcessCompleteToNostate = "E40_07_ProcessCompleteToNostate";
		public static readonly string E40_08_ExecutingToPausing = "E40_08_ExcutingToPausing";
		public static readonly string E40_09_PausingToPaused = "E40_09_PausingToPaused";
		public static readonly string E40_10_PauseToExecuting = "E40_10_PauseToExecuting";
		public static readonly string E40_11_ExecutingToStopping = "E40_11_ExecutingToStopping";
		public static readonly string E40_12_PauseToStoping = "E40_12_PauseToStoping";
		public static readonly string E40_13_ExecutingToAborting = "E40_13_ExecutingToAborting";
		public static readonly string E40_14_StopingToAborting = "E40_14_StopingToAborting";
		public static readonly string E40_15_PauseToAborting = "E40_15_PauseToAborting";
		public static readonly string E40_16_AbortingToAborted = "E40_16_AbortingToAborted";
		public static readonly string E40_17_StoppingToStopped = "E40_17_StoppingToStopped";
		public static readonly string E40_18_QueuedToNostate = "E40_18_QueuedToNostate";

		#endregion

		#region E90
		public static readonly string SubstSMTrans01_InitToSource = "SubstSMTrans01_InitToSource";
		public static readonly string SubstSMTrans02_SourceToWork = "SubstSMTrans02_SourceToWork";
		public static readonly string SubstSMTrans03_WorkToSource = "SubstSMTrans03_WorkToSource";
		public static readonly string SubstSMTrans04_WorkToWork = "SubstSMTrans04_WorkToWork";
		public static readonly string SubstSMTrans05_WorkToDestination = "SubstSMTrans05_WorkToDestination";
		public static readonly string SubstSMTrans06_DestinationToWork = "SubstSMTrans06_DestinationToWork";
		public static readonly string SubstSMTrans07_DestinationToExtinction = "SubstSMTrans07_DestinationToExtinction";
		public static readonly string SubstSMTrans08_DestinationToSource = "SubstSMTrans08_DestinationToSource";
		public static readonly string SubstSMTrans09_ProcessToEnd = "SubstSMTrans09_ProcessToEnd";

		public static readonly string SubstSMTrans10_InitToNeedProcess = "SubstSMTrans10_InitToNeedProcess";
		public static readonly string SubstSMTrans11_NeedProcessToInProcess = "SubstSMTrans11_NeedProcessToInProcess";
		public static readonly string SubstSMTrans12_InProcessToComplete = "SubstSMTrans12_InProcessToComplete";
		public static readonly string SubstSMTrans13_InProcessToNeedProcess = "SubstSMTrans13_InProcessToNeedProcess";
		public static readonly string SubstSMTrans14_NeedProcessToComplete = "SubstSMTrans14_NeedProcessToComplete";

		public static readonly string SubstSMTrans16_InitToNotConfirmed = "SubstSMTrans16_InitToNotConfirmed";
		public static readonly string SubstSMTrans17_NotConfirmedToConfirmed = "SubstSMTrans17_NotConfirmedToConfirmed";
		public static readonly string SubstSMTrans18_IDReadFailNotConfirmedToWaitingForHost = "SubstSMTrans18_IDReadFailNotConfirmedToWaitingForHost";
		public static readonly string SubstSMTrans19_IDMismatchNotConfirmedToWaitingForHost = "SubstSMTrans19_IDMismatchNotConfirmedToWaitingForHost";
		public static readonly string SubstSMTrans20_WaitingForHostToConfirmed = "SubstSMTrans20_WaitingForHostToConfirmed";
		public static readonly string SubstSMTrans21_WaitingForHostToConfirmationFailed = "SubstSMTrans21_WaitingForHostToConfirmationFailed";

		public static readonly string OccupiedTrans02UnOccupied = "OccupiedTrans02UnOccupied";
		public static readonly string UnOccupiedTrans01Occupied = "UnOccupiedTrans01Occupied";
		#endregion

		public static readonly string CarrierIDWaitForHost = "EV_CARRIER_ID_WAIT_FOR_HOST";

		public static readonly string CarrierIDVerifyOK = "EV_CARRIER_ID_VERIFY_OK";

		public static readonly string CarrierIDVerifyFailed = "EV_CARRIER_ID_VERIFY_FAILED";

		public static readonly string CarrierIDReadFail = "EV_CARRIER_ID_READ_FAILED";

		public static readonly string E87NoStateTrans01Carrier = "E87NoStateTrans01Carrier";

		public static readonly string E87NoStateTrans17NotAccessed = "E87NoStateTrans17NotAccessed";

		public static readonly string CarrierSlotMapWaitForHost = "EV_CARRIER_SLOTMAP_WAIT_FOR_HOST";

		public static readonly string CarrierSlotMapVerifyOK = "EV_CARRIER_SLOTMAP_VERIFY_OK";

		public static readonly string CarrierSlotMapVerifyFailed = "EV_CARRIER_SLOTMAP_VERIFY_FAILED";

		public static readonly string E87ReadyToUnLoadTrans07TransferBlocked = "E87ReadyToUnLoadTrans07TransferBlocked";

		public static readonly string CarrierAccessInAccess = "EV_CARRIER_IN_ACCESS";

		public static readonly string CarrierAccessComplete = "EV_CARRIER_COMPLETE";

		public static readonly string CarrierAccessStopped = "EV_CARRIER_STOPPED";

		public static readonly string CarrierLocationChanged = "EV_CARRIER_LOCATION_CHANGED";

		public static readonly string CarrierApproachingComplete = "EV_CARRIER_APPROACH_COMPLETE";

		public static readonly string CarrierClamped = "EV_CARRIER_CLAMPED";

		public static readonly string CarrierUnclamped = "EV_CARRIER_UNCLAMPED";

		public static readonly string CarrierTagReleased = "EV_CARRIER_TAG_RELEASED";

		public static readonly string CarrierDoorOpened = "EV_CARRIER_DOOR_OPENED";

		public static readonly string CarrierDoorClosed = "EV_CARRIER_DOOR_CLOSED";

		public static readonly string LoadPortSwitchToOOS = "EV_LOADPORT_TRANSFER_OOS";

		public static readonly string LoadPortSwitchToReadyToLoad = "EV_LOADPORT_TRANSFER_READY_TO_LOAD";

		public static readonly string LoadPortSwitchToReadyToUnload = "EV_LOADPORT_TRANSFER_READY_TO_UNLOAD";

		public static readonly string LoadPortSwitchToTransferBlocked = "EV_LOADPORT_TRANSFER_BLOCKED";

		public static readonly string LoadPortOOSAlarmOn = "EV_LOADPORT_ALARM_OOS_ON";

		public static readonly string LoadPortOOSAlarmOff = "EV_LOADPORT_ALARM_OOS_OFF";

		public static readonly string LoadPortReservationOn = "EV_LOADPORT_RESERVATION_ON";

		public static readonly string LoadPortReservationOff = "EV_LOADPORT_RESERVATION_OFF";

		public static readonly string LoadPortAccessModeChangedAuto = "EV_LOADPORT_ACCESS_MODE_CHANGED_AUTO";

		public static readonly string LoadPortAccessModeChangedManual = "EV_LOADPORT_ACCESS_MODE_CHANGED_Manual";

		public static readonly string CarrierMoveFailedOn = "EV_ALARM_CARRIER_MOVE_FAILED_ON";

		public static readonly string CarrierMoveFailedOff = "EV_ALARM_CARRIER_MOVE_FAILED_OFF";

		public static readonly string CarrierPlacementErrorOn = "EV_ALARM_CARRIER_PLACE_ERR_ON";

		public static readonly string CarrierPlacementErrorOff = "EV_ALARM_CARRIER_PLACE_ERR_OFF";

		public static readonly string PIOAlarmOn = "EV_ALARM_PIO_ON";

		public static readonly string PIOAlarmOff = "EV_ALARM_PIO_OFF";

		public static readonly string MaterialReceivedEvent = "MaterialReceivedEvent";

		public static readonly string MaterialRemovedEvent = "MaterialRemovedEvent";

		public static readonly string CarrierSlotMapNotRead = "NoStateTransSotMapNotRead";

		public static readonly string AssocSMGoAssoc = "AssocSMGoAssoc";

		public static readonly string SubStrateNeedProcessing = "SubStrateNeedProcessing";

		public static readonly string UnOccupiedTransOccupied = "UnOccupiedTransOccupied";

		public static readonly string OccupiedTransUnOccupied = "OccupiedTransUnOccupied";

		public static readonly string AssocToNotAssoc = "AssocToNotAssoc";
	}
}
