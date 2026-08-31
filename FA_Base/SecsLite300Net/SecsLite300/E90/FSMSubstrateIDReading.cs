using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E90
{
	internal class FSMSubstrateIDReading : FSMBase
	{
		public virtual string ClassName => GetType().Name;

		public enum Event : ushort
		{
			IDNotConfirmed,
			IDConfirmed,
			IDReadingFailed,
			IDMismatch,
			IDConfirmedFailed
		}

		public FSMSubstrateIDReading(object host)
			: base(host, FSMRunMode.Basic)
		{
			DefineTransition(SubstIDReadingState.NotConfirmed, Event.IDNotConfirmed, SubstIDReadingState.NotConfirmed, onNotConfirmed);
			DefineTransition(SubstIDReadingState.NotConfirmed, Event.IDConfirmed, SubstIDReadingState.Confirmed, onConfirmed);
			DefineTransition(SubstIDReadingState.NotConfirmed, Event.IDReadingFailed, SubstIDReadingState.WaitingForHost, onIDReadingFailed);
			DefineTransition(SubstIDReadingState.NotConfirmed, Event.IDMismatch, SubstIDReadingState.WaitingForHost, onIDMismatch);
			DefineTransition(SubstIDReadingState.WaitingForHost, Event.IDConfirmed, SubstIDReadingState.Confirmed, onWaitForHostToConfirmed);
			DefineTransition(SubstIDReadingState.WaitingForHost, Event.IDConfirmedFailed, SubstIDReadingState.ConfirmationFailed, onIDConfirmedFailed);
			base.CurrentState = SubstIDReadingState.NotConfirmed;
		}

		public override void Start()
		{
			base.Start();
			//TriggerEvent(Event.IDNotConfirmed);
		}

		private void onNotConfirmed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans16_InitToNotConfirmed, null, base.ActionHost);
		}

		private void onConfirmed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans17_NotConfirmedToConfirmed, null, base.ActionHost);
		}

		private void onIDReadingFailed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans18_IDReadFailNotConfirmedToWaitingForHost, null, base.ActionHost);
		}

		private void onIDMismatch(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans19_IDMismatchNotConfirmedToWaitingForHost, null, base.ActionHost);
		}

		private void onWaitForHostToConfirmed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans20_WaitingForHostToConfirmed, null, base.ActionHost);
		}

		private void onIDConfirmedFailed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans21_WaitingForHostToConfirmationFailed, null, base.ActionHost);

		}


	}
}
