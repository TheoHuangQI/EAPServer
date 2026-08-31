namespace SecsLite300.E116
{
	public interface IEPTModule
	{
		EPTTaskType TaskType { get; }

		TransitionType Transition { get; }

		string TransitionTimestamp { get; }

		string BlockedReasonText { get; }

		BlockType BlockedReason { get; }

		string PreviousTaskName { get; }

		string TaskName { get; }

		EPTTaskType PreviousTaskType { get; }

		uint TrackerEventID { get; }

		TransitionType[] DisableEventOnTransition { get; }

		PerformanceTrackingState EPTState { get; }

		uint EPTStateTime { get; }

		PerformanceTrackingElementType EPTElementType { get; }

		string EPTElementName { get; }

		uint EPTStateTimeStart { get; }

		PerformanceTrackingState PreviousEPTState { get; }

		int SetBlocked(BlockType type, string desc);

		int SetBusy(EPTTaskType taskType, string taskName);

		int SetIdle();
	}
}
