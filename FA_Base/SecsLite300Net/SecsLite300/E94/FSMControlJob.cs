using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E94
{
	internal class FSMControlJob : FSMBase
	{
		public virtual string ClassName => GetType().Name;
		public enum Event : ushort
		{
			Accepted,
			Select,
			Start,
			WaitStart,
			Pause,
			Resume,
			Cancel,
			Deselect,
			Stop,
			Abort,
			HOQ,
			AllPJCompleted,
			CJRemove
		}

		public FSMControlJob(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(ControlJobState.NoState, Event.Accepted, ControlJobState.Queued, onQueued);
			DefineTransition(ControlJobState.Queued, Event.Select, ControlJobState.Selected, onSelected);
			DefineTransition(ControlJobState.Selected, Event.Deselect, ControlJobState.Queued, onDeselected);
			DefineTransition(ControlJobState.Selected, Event.Start, ControlJobState.Executing, onExecuting);
			DefineTransition(ControlJobState.Selected, Event.WaitStart, ControlJobState.WaitingForStart, onWaitingForStart);
			DefineTransition(ControlJobState.WaitingForStart, Event.Start, ControlJobState.Executing, onWaitingfotStartToExecuting);
			DefineTransition(ControlJobState.Executing, Event.Pause, ControlJobState.Paused, onPause);
			DefineTransition(ControlJobState.Paused, Event.Resume, ControlJobState.Executing, onResume);

			DefineTransition(ControlJobState.Executing, Event.AllPJCompleted, ControlJobState.Completed, onComplete);

			DefineTransition(ControlJobState.Selected, Event.Stop, ControlJobState.Completed, onCompleteStop);
			DefineTransition(ControlJobState.WaitingForStart, Event.Stop, ControlJobState.Completed, onCompleteStop);
			DefineTransition(ControlJobState.Executing, Event.Stop, ControlJobState.Completed, onCompleteStop);
			DefineTransition(ControlJobState.Paused, Event.Stop, ControlJobState.Completed, onCompleteStop);

			DefineTransition(ControlJobState.Selected, Event.Abort, ControlJobState.Completed, onCompleteAbort);
			DefineTransition(ControlJobState.WaitingForStart, Event.Abort, ControlJobState.Completed, onCompleteAbort);
			DefineTransition(ControlJobState.Executing, Event.Abort, ControlJobState.Completed, onCompleteAbort);
			DefineTransition(ControlJobState.Paused, Event.Abort, ControlJobState.Completed, onCompleteAbort);

			DefineTransition(ControlJobState.Queued, Event.Cancel, ControlJobState.NoState, onJobDone);
			DefineTransition(ControlJobState.Queued, Event.Stop, ControlJobState.NoState, onJobDone);
			DefineTransition(ControlJobState.Queued, Event.Abort, ControlJobState.NoState, onJobDone);

			DefineTransition(ControlJobState.Completed, Event.CJRemove, ControlJobState.NoState, onRemove);
			base.CurrentState = ControlJobState.NoState;
		}

		#region CJAction1
		public override void Start()
		{
			base.Start();
		}

		private void onQueued(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_01_NoStateToQueued, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onJobDone(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			IControlJob cj = arg as IControlJob;
			string res = "";
			Gem300Manager.EquipmentObject.OnControlJobCancel(cj, ref  res);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_02_QueuedToNoState, null, base.ActionHost);
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onSelected(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobSelected(cj);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_03_QueuedToSelected, null, base.ActionHost);
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onDeselected(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_04_SelectedToQueued, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onExecuting(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_05_SelectedToExecuting, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onWaitingForStart(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_06_SelectedToWaitingforstart, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}
		private void onWaitingfotStartToExecuting(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_07_WaitingforstartToExecuting, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onPause(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_08_ExecutingToPaused, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onResume(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_09_PausedToExecuting, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}
		

		private void onComplete(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_10_ExecutingToCompleted, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onCompleteStop(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobDone(cj, CJCompleteType.Stopped);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_11_ActiveToCompletedStop, null, base.ActionHost);
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onCompleteAbort(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobDone(cj, CJCompleteType.Aborted);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_12_ActiveToCompletedAbort, null, base.ActionHost);
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}

		private void onRemove(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E94_CJ_13_CompletedToNoState, null, base.ActionHost);
			IControlJob cj = arg as IControlJob;
			Gem300Manager.EquipmentObject.OnControlJobStatusChange(cj);
		}
		#endregion
	}
}
