using FALogger;
using FSMLite;
using SecsLite300.E39;
using System.Reflection;

namespace SecsLite300.E87
{
	public class FSMCarrierAccessing : FSMBase
	{
		public virtual string ClassName => GetType().Name;

		public enum Event : ushort
		{
			EquipmentStartAccessing,
			EquipmentAccessFinished,
			EquipmentAccessFailed
		}

		public FSMCarrierAccessing(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(CarrierAccessingStatus.NotAccessed, Event.EquipmentStartAccessing, CarrierAccessingStatus.InAccess, onInAccess);
			DefineTransition(CarrierAccessingStatus.InAccess, Event.EquipmentAccessFinished, CarrierAccessingStatus.CarrierComplete, onComplete);
			DefineTransition(CarrierAccessingStatus.InAccess, Event.EquipmentAccessFailed, CarrierAccessingStatus.CarrierStopped, onStopped);
			base.CurrentState = CarrierAccessingStatus.NotAccessed;
		}

		private void onInAccess(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierAccessInAccess, null, base.ActionHost);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onComplete(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierAccessComplete, null, base.ActionHost);
			if (Gem300Manager.EquipmentObject.CarrierHoldControl == CarrierHoldControlMode.AutoRelease)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Gem300Manager.EquipmentObject.CarrierHoldControl={Gem300Manager.EquipmentObject.CarrierHoldControl} ");
				Gem300Manager.EquipmentObject.OnCarrierTagReleased(base.ActionHost as ICarrier, null);
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		private void onStopped(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierAccessStopped, null, base.ActionHost);
			if (Gem300Manager.EquipmentObject.CarrierHoldControl == CarrierHoldControlMode.AutoRelease)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Gem300Manager.EquipmentObject.CarrierHoldControl={Gem300Manager.EquipmentObject.CarrierHoldControl}");
				Gem300Manager.EquipmentObject.OnCarrierTagReleased(base.ActionHost as ICarrier, null);
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}
	}
}
