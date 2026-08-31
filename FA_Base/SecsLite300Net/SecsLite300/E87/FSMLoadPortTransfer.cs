using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E87
{
	public class FSMLoadPortTransfer : FSMBase
	{

		public virtual string ClassName => GetType().Name;
		public enum Event : ushort
		{
			SwitchToOOS, //(Out Of Service)
			SwitchToReadyToLoad,
			SwitchToReadyToUnload,
			SwitchToTransferBlocked
		}

		public FSMLoadPortTransfer(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(LoadPortTransferStatus.OutOfService, Event.SwitchToReadyToLoad, LoadPortTransferStatus.ReadyToLoad, onReadyToLoad);
			DefineTransition(LoadPortTransferStatus.OutOfService, Event.SwitchToReadyToUnload, LoadPortTransferStatus.ReadyToUnload, onReadyToUnload);
			DefineTransition(LoadPortTransferStatus.OutOfService, Event.SwitchToTransferBlocked, LoadPortTransferStatus.TransferBlocked, onReadyToUnload);
			DefineTransition(LoadPortTransferStatus.ReadyToLoad, Event.SwitchToOOS, LoadPortTransferStatus.OutOfService, onOOS);
			DefineTransition(LoadPortTransferStatus.ReadyToUnload, Event.SwitchToOOS, LoadPortTransferStatus.OutOfService, onOOS);
			DefineTransition(LoadPortTransferStatus.TransferBlocked, Event.SwitchToOOS, LoadPortTransferStatus.OutOfService, onOOS);
			DefineTransition(LoadPortTransferStatus.ReadyToLoad, Event.SwitchToTransferBlocked, LoadPortTransferStatus.TransferBlocked, onTransferBlocked);
			DefineTransition(LoadPortTransferStatus.ReadyToUnload, Event.SwitchToTransferBlocked, LoadPortTransferStatus.TransferBlocked, onTransferBlocked);
			DefineTransition(LoadPortTransferStatus.TransferBlocked, Event.SwitchToReadyToLoad, LoadPortTransferStatus.ReadyToLoad, onReadyToLoad);
			DefineTransition(LoadPortTransferStatus.TransferBlocked, Event.SwitchToReadyToUnload, LoadPortTransferStatus.ReadyToUnload, onReadyToUnload);
			base.CurrentState = loadStatus();
		}

		private void onReadyToLoad(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.LoadPortSwitchToReadyToLoad, null, base.ActionHost);
		}

		private void onOOS(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.LoadPortSwitchToOOS, null, base.ActionHost);
		}

		private void onTransferBlocked(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.LoadPortSwitchToTransferBlocked, null, base.ActionHost);
		}

		private void onReadyToUnload(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.LoadPortSwitchToReadyToUnload, null, base.ActionHost);
		}

		private LoadPortTransferStatus loadStatus()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			return LoadPortTransferStatus.ReadyToLoad;
		}
	}
}
