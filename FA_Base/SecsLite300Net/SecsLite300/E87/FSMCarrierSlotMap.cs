using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E87
{
	public class FSMCarrierSlotMap : FSMBase
	{

		public virtual string ClassName => GetType().Name;
		public enum Event : ushort
		{
			NotRead,
			VerificationOK,
			VerificationFailed,
			WaitForHost
		}

		public FSMCarrierSlotMap(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(CarrierSlotMapStatus.NotRead, Event.VerificationOK, CarrierSlotMapStatus.VerificationOK, onVerifyOK);
			DefineTransition(CarrierSlotMapStatus.NotRead, Event.WaitForHost, CarrierSlotMapStatus.WaitingForHost, onWaitForHost);
			DefineTransition(CarrierSlotMapStatus.WaitingForHost, Event.VerificationOK, CarrierSlotMapStatus.VerificationOK, onVerifyOK);
			DefineTransition(CarrierSlotMapStatus.WaitingForHost, Event.VerificationFailed, CarrierSlotMapStatus.VerificationFailed, onVerifyFailed);
			DefineTransition(CarrierSlotMapStatus.NoState, Event.NotRead, CarrierSlotMapStatus.NotRead, onNotRead);
			base.CurrentState = CarrierSlotMapStatus.NoState;
		}

		private void onNotRead(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E87NoStateTrans01Carrier, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierSlotMapNotRead, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E87NoStateTrans17NotAccessed, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onVerifyOK(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierSlotMapVerifyOK, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onWaitForHost(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierSlotMapWaitForHost, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onVerifyFailed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierSlotMapVerifyFailed, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}
	}
}
