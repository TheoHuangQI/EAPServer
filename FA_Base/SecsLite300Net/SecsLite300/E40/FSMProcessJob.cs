using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E40
{
	public class FSMProcessJob : FSMBase
	{
		public enum Event : ushort
		{
			Accepted,
			SettingUp,
			WaitForStart,
			Start,
			ProcessComplete,
			Pause,
			PauseDone,
			Stop,
			StopDone,
			Abort,
			AbortDone,
			Resume,
			Cancel,
			JobDone
		}

		private ProcessJobState m_lastState = ProcessJobState.NoState;
		public virtual string ClassName => GetType().Name;

		public FSMProcessJob(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(ProcessJobState.NoState, Event.Accepted, ProcessJobState.Queued, onQueued);
			DefineTransition(ProcessJobState.Queued, Event.SettingUp, ProcessJobState.SettingUp, onSettingUp);
			DefineTransition(ProcessJobState.SettingUp, Event.WaitForStart, ProcessJobState.WaitingForStart, onWaitForStart);
			DefineTransition(ProcessJobState.SettingUp, Event.Start, ProcessJobState.Processing, onSettingUpToStart);
			DefineTransition(ProcessJobState.WaitingForStart, Event.Start, ProcessJobState.Processing, onWaitingForStartToStart);
			DefineTransition(ProcessJobState.Processing, Event.ProcessComplete, ProcessJobState.ProcessComplete, onProcessDone);
			DefineTransition(ProcessJobState.ProcessComplete, Event.JobDone, ProcessJobState.JobDone, onJobDone);
			DefineTransition(ProcessJobState.Aborted, Event.JobDone, ProcessJobState.JobDone, onJobDone);
			DefineTransition(ProcessJobState.Stopped, Event.JobDone, ProcessJobState.JobDone, onJobDone);
			DefineTransition(ProcessJobState.SettingUp, Event.Pause, ProcessJobState.Pausing, onPausing);
			DefineTransition(ProcessJobState.WaitingForStart, Event.Pause, ProcessJobState.Pausing, onPausing);
			DefineTransition(ProcessJobState.Processing, Event.Pause, ProcessJobState.Pausing, onPausing);
			DefineTransition(ProcessJobState.Pausing, Event.PauseDone, ProcessJobState.Paused, onPaused);
			DefineTransition(ProcessJobState.Pausing, Event.Resume, ProcessJobState.SettingUp, onResume);
			DefineTransition(ProcessJobState.Pausing, Event.Resume, ProcessJobState.WaitingForStart, onResume);
			DefineTransition(ProcessJobState.Pausing, Event.Resume, ProcessJobState.Processing, onResume);
			DefineTransition(ProcessJobState.Paused, Event.Resume, ProcessJobState.SettingUp, onResume);
			DefineTransition(ProcessJobState.Paused, Event.Resume, ProcessJobState.WaitingForStart, onResume);
			DefineTransition(ProcessJobState.Paused, Event.Resume, ProcessJobState.Processing, onResume);
			DefineTransition(ProcessJobState.SettingUp, Event.Stop, ProcessJobState.Stopping, onExecutingToStopping);
			DefineTransition(ProcessJobState.WaitingForStart, Event.Stop, ProcessJobState.Stopping, onExecutingToStopping);
			DefineTransition(ProcessJobState.Processing, Event.Stop, ProcessJobState.Stopping, onExecutingToStopping);
			DefineTransition(ProcessJobState.Pausing, Event.Stop, ProcessJobState.Stopping, onPauseToStopping);
			DefineTransition(ProcessJobState.Paused, Event.Stop, ProcessJobState.Stopping, onPauseToStopping);
			DefineTransition(ProcessJobState.SettingUp, Event.Abort, ProcessJobState.Aborting, onExecutingToAborting);
			DefineTransition(ProcessJobState.WaitingForStart, Event.Abort, ProcessJobState.Aborting, onExecutingToAborting);
			DefineTransition(ProcessJobState.Processing, Event.Abort, ProcessJobState.Aborting, onExecutingToAborting);
			DefineTransition(ProcessJobState.Stopping, Event.Abort, ProcessJobState.Aborting, onStoppingToAborting);
			DefineTransition(ProcessJobState.Pausing, Event.Abort, ProcessJobState.Aborting, onPauseToAborting);
			DefineTransition(ProcessJobState.Paused, Event.Abort, ProcessJobState.Aborting, onPauseToAborting);
			DefineTransition(ProcessJobState.Aborting, Event.AbortDone, ProcessJobState.Aborted, onAborted);
			DefineTransition(ProcessJobState.Stopping, Event.StopDone, ProcessJobState.Stopped, onStopped);
			DefineTransition(ProcessJobState.Queued, Event.Abort, ProcessJobState.JobDone, onDequeue);
			DefineTransition(ProcessJobState.Queued, Event.Stop, ProcessJobState.JobDone, onDequeue);
			DefineTransition(ProcessJobState.Queued, Event.Cancel, ProcessJobState.JobDone, onDequeue);
			base.CurrentState = ProcessJobState.NoState;
		}
		#region PJAction1

		public override void Start()
		{
			base.Start();
			TriggerEvent(Event.Accepted);
		}

		public void StoreCurrentState()
		{
			m_lastState = (ProcessJobState)base.CurrentState;
		}

		private void onQueued(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_01_NostateToQueued, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onSettingUp(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_02_QueuedToSettingUP, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onWaitForStart(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_03_SettingUPToWaittingForStart, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onSettingUpToStart(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_04_SettingUPToProcessing, null, base.ActionHost);
			//Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.ProcessJobToStartState, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}
		private void onWaitingForStartToStart(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_05_WaittingForStartToProcessing, null, base.ActionHost);
			//Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.ProcessJobToStartState, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onProcessDone(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_06_ProcessingToProcessComplete, null, base.ActionHost);
			//Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobProcessingComplete, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onJobDone(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_07_ProcessCompleteToNostate, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}



		private void onPausing(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_08_ExecutingToPausing, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onPaused(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_09_PausingToPaused, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onResume(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_10_PauseToExecuting, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onExecutingToStopping(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_11_ExecutingToStopping, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onPauseToStopping(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_12_PauseToStoping, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onExecutingToAborting(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_13_ExecutingToAborting, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			//TriggerEvent(Event.AbortDone);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onStoppingToAborting(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_14_StopingToAborting, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			//TriggerEvent(Event.AbortDone);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}
		private void onPauseToAborting(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_15_PauseToAborting, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			//TriggerEvent(Event.AbortDone);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}
		

		private void onAborted(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_16_AbortingToAborted, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onStopped(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_17_StoppingToStopped, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		private void onDequeue(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobStateChange, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E40_18_QueuedToNostate, null, base.ActionHost);
			IProcessJob pj = arg as IProcessJob;
			Gem300Manager.EquipmentObject.OnProcessJobStatusChange(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}
		#endregion
	}
}
