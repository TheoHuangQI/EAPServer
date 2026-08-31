using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E90
{
	internal class FSMSubstrateProcessing : FSMBase
	{
		public virtual string ClassName => GetType().Name;
		public enum Event : ushort
		{
			StartProcessing,
			NeedsProcessing,
			SubstProcessed,
			SubstAborted,
			SubstRejected,
			SubstLost,
			SubstStopped,
			SubstSkipped
		}

		public FSMSubstrateProcessing(object host)
			: base(host, FSMRunMode.Basic)
		{
			DefineTransition(SubstProcessingState.NeedsProcessing, Event.NeedsProcessing, SubstProcessingState.NeedsProcessing, onInitNeedProcess);
			DefineTransition(SubstProcessingState.NeedsProcessing, Event.StartProcessing, SubstProcessingState.InProcess, onInProcess);
			DefineTransition(SubstProcessingState.InProcess, Event.SubstProcessed, SubstProcessingState.Processed, onInProcessToProcessed);
			DefineTransition(SubstProcessingState.InProcess, Event.SubstAborted, SubstProcessingState.Aborted, onAborted);
			DefineTransition(SubstProcessingState.InProcess, Event.SubstRejected, SubstProcessingState.Rejected, onRejected);
			DefineTransition(SubstProcessingState.InProcess, Event.SubstLost, SubstProcessingState.Lost, onLost);
			DefineTransition(SubstProcessingState.InProcess, Event.SubstStopped, SubstProcessingState.Stopped, onStopped);
			DefineTransition(SubstProcessingState.InProcess, Event.SubstSkipped, SubstProcessingState.Skipped, onSkipped);
			DefineTransition(SubstProcessingState.InProcess, Event.NeedsProcessing, SubstProcessingState.NeedsProcessing, onNeedProcess);
			DefineTransition(SubstProcessingState.NeedsProcessing, Event.SubstLost, SubstProcessingState.Lost, onNeedProceeToStopped);
			DefineTransition(SubstProcessingState.NeedsProcessing, Event.SubstSkipped, SubstProcessingState.Skipped, onNeedProceeToSkipped);
			base.CurrentState = SubstProcessingState.NeedsProcessing;
		}
		public override void Start()
		{
			base.Start();
			TriggerEvent(Event.NeedsProcessing);
		}

		private void onInitNeedProcess(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans10_InitToNeedProcess, null, base.ActionHost);
		}

		private void onInProcess(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans11_NeedProcessToInProcess, null, base.ActionHost);
		}

		private void onInProcessToProcessed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans12_InProcessToComplete, null, base.ActionHost);
		}

		private void onAborted(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans12_InProcessToComplete, null, base.ActionHost);
		}

		private void onRejected(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans12_InProcessToComplete, null, base.ActionHost);
		}

		private void onLost(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans12_InProcessToComplete, null, base.ActionHost);
		}

		private void onStopped(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans12_InProcessToComplete, null, base.ActionHost);
		}

		private void onSkipped(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans12_InProcessToComplete, null, base.ActionHost);
		}

		private void onNeedProcess(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans13_InProcessToNeedProcess, null, base.ActionHost);
		}

		private void onNeedProceeToStopped(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans14_NeedProcessToComplete, null, base.ActionHost);
		}

		private void onNeedProceeToSkipped(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans14_NeedProcessToComplete, null, base.ActionHost);
		}

	}
}
