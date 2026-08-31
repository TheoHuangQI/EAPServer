using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsLite300
{
	public enum FA300ErrorCode : uint
	{
		Base = 1000u,
		PJobRejected,
		PJobDuplicated,
		CJIDDuplicated
	}


    #region E94
    public enum CJCompleteType
	{
		Completed,
		Aborted,
		Stopped
	}

	public enum CJProcessOrderMgmt : byte
	{
		Arrival = 1,
		Optimize,
		List
	}

	public enum CJSelectionMode
	{
		Accepted,
		Deferred,
		Rejected
	}

	public enum CJSelectResult
	{
		Accepted,
		Deferred,
		Rejected
	}

	public enum ControlJobCommand : byte
	{
		CJStart = 1,
		CJPause,
		CJResume,
		CJCancel,
		CJDeselect,
		CJStop,
		CJAbort,
		CJHOQ
	}

	public enum ControlJobState : byte
	{
		NoState = byte.MaxValue,
		Queued=0,
		Selected,
		WaitingForStart,
		Executing,
		Paused,
		Completed
	}
	#endregion

	#region E90
	public enum SubstIDReadingState : byte
	{
		NotConfirmed,
		WaitingForHost,
		Confirmed,
		ConfirmationFailed
	}

	public enum SubstLocState : byte
	{
		Unoccupied,
		Occupied
	}

	public enum SubstProcessingState : byte
	{
		NeedsProcessing,
		InProcess,
		Processed,
		Rejected,
		Stopped,
		Aborted,
		Lost,
		Skipped
	}

	public enum SubstrateType : byte
	{
		Wafer,
		FlatPanel,
		CD,
		Mask
	}

	public enum SubstTransportState : byte
	{
		AtSource,
		AtWork,
		AtDestination
	}

	public enum SubstUsage : byte
	{
		Product,
		Test,
		Filler
	}
	#endregion

	#region E87
	public enum CarrierAccessingStatus : byte
	{
		NotAccessed,
		InAccess,
		CarrierComplete,
		CarrierStopped
	}

	public enum CarrierIDStatus : byte
	{
		IDNotRead,
		WaitingForHost,
		VerificationOK,
		VerificationFailed
	}

	public enum CarrierSlotMapStatus : byte
	{
		NoState,
		NotRead,
		WaitingForHost,
		VerificationOK,
		VerificationFailed
	}

	public enum DataStatus
	{
		NotAvailable,
		ReadOK,
		ReadFailed
	}

	public enum LoadPortAccessMode : byte
	{
		Manual,
		Auto
	}

	public enum LoadPortLocationType
	{
		LoadPort,
		FIMS
	}

	public enum LoadPortTransferStatus : byte
	{
		OutOfService,
		TransferBlocked,
		ReadyToLoad,
		ReadyToUnload,
		InService,
		TransferReady
	}


	public enum OnOffState
	{
		OffState,
		OnState,
		Pulse
	}

	public enum PortAssociationStatus : byte
	{
		NOTASSOCIATED,
		ASSOCIATED
	}

	public enum SlotState : byte
	{
		Undefined,
		Empty,
		NotEmpty,
		CorrectlyOccupied,
		DoubleSlotted,
		CrossSlotted
	}

	public enum UnclampMethod
	{
		Release,
		HoldUntilArrival
	}
	#endregion

	#region E40
	public enum PJResultCode
	{
		Success,
		Error
	}

	public enum PJSetupResult
	{
		Success,
		Delay,
		Rejected
	}

	public enum ProcessJobState : byte
	{
		NoState = byte.MaxValue,
		Queued = 0,
		SettingUp = 1,
		WaitingForStart = 2,
		Processing = 3,
		ProcessComplete = 4,
		Pausing = 6,
		Paused = 7,
		Stopping = 8,
		Aborting = 9,
		Stopped = 10,
		Aborted = 11,
		JobDone = 100
	}

	public enum ProcessRecipeMethod : byte
	{
		RecipeOnly = 1,
		RecipeWithVariableTunning
	}
	#endregion

	#region E39
	public enum CarrierHoldControlMode : byte
	{
		WaitHostRelease = 1,
		AutoRelease = 0
	}

	public enum ObjectCommand : byte
	{
		Reserved,
		AttachToRequestor,
		DetachFromRequestor,
		ReattachToRequestor,
		SetAttributes
	}
	#endregion

	#region E116
	public enum BlockType : byte
	{
		NotBlocked,
		Unknown,
		Safety,
		Error,
		ParameterException,
		Aborting,
		Pausing,
		Reserve1,
		Reserve2,
		Reserve3
	}

	public enum EPTTaskType : byte
	{
		NoTask,
		Unspecified,
		Process,
		Support,
		EquipmentMaintenance,
		EquipmentDiagnostics,
		Waiting
	}

	public enum PerformanceTrackingElementType : byte
	{
		Equipment,
		Production,
		EFEMLoadPort
	}

	public enum PerformanceTrackingState : byte
	{
		NoState,
		Idle,
		Busy,
		Blocked
	}

	public enum TransitionType : byte
	{
		Transition1 = 1,
		Transition2,
		Transition3,
		Transition4,
		Transition5,
		Transition6,
		Transition7,
		Transition8,
		Transition9
	}
	#endregion
}
