using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E87
{
	internal class FSMCarrierID : FSMBase
	{
		public virtual string ClassName => GetType().Name;
		public enum Event : ushort
		{
			IDVerificationOK,
			IDVerificationFailed,
			WaitForHost
		}

		public FSMCarrierID(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(CarrierIDStatus.IDNotRead, Event.IDVerificationOK, CarrierIDStatus.VerificationOK, onVerifyOK);
			DefineTransition(CarrierIDStatus.IDNotRead, Event.WaitForHost, CarrierIDStatus.WaitingForHost, onWaitForHost);
			DefineTransition(CarrierIDStatus.WaitingForHost, Event.IDVerificationOK, CarrierIDStatus.VerificationOK, onVerifyOK);
			DefineTransition(CarrierIDStatus.WaitingForHost, Event.IDVerificationFailed, CarrierIDStatus.VerificationFailed, onVerifyFailed);
			base.CurrentState = CarrierIDStatus.IDNotRead;
		}

		private void onWaitForHost(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierIDWaitForHost, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onVerifyOK(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierIDVerifyOK, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onVerifyFailed(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierIDVerifyFailed, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}
	}
}
