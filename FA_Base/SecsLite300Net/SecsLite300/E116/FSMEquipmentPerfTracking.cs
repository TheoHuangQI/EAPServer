using FSMLite;

namespace SecsLite300.E116
{
	internal class FSMEquipmentPerfTracking : FSMBase
	{
		public enum Event : ushort
		{
			ToIdle,
			ToBusy,
			ToBlocked
		}

		public FSMEquipmentPerfTracking(object host)
			: base(host, FSMRunMode.Basic)
		{
			DefineTransition(PerformanceTrackingState.NoState, Event.ToIdle, PerformanceTrackingState.Idle, null);
			DefineTransition(PerformanceTrackingState.NoState, Event.ToBusy, PerformanceTrackingState.Busy, null);
			DefineTransition(PerformanceTrackingState.NoState, Event.ToBlocked, PerformanceTrackingState.Blocked, null);
			DefineTransition(PerformanceTrackingState.Idle, Event.ToBusy, PerformanceTrackingState.Busy, null);
			DefineTransition(PerformanceTrackingState.Busy, Event.ToIdle, PerformanceTrackingState.Idle, null);
			base.CurrentState = PerformanceTrackingState.NoState;
		}
	}
}
