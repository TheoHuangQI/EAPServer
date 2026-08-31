using System.Reflection;
using System.Threading;
using FALogger;
using FSMLite;

namespace SecsLite300.E90
{
	internal class FSMSubstrateTransport : FSMBase
	{
		public virtual string ClassName => GetType().Name;
		public enum Event : ushort
		{
			SubstEnterEquipment,
			SubstToSource,
			SubstToWork,
			SubstToDestination,
			SubstRemoved
		}

		public FSMSubstrateTransport(object host)
			: base(host, FSMRunMode.Basic)
		{
			DefineTransition(SubstTransportState.AtSource, Event.SubstToSource, SubstTransportState.AtSource, onInitToSource);
			DefineTransition(SubstTransportState.AtSource, Event.SubstToWork, SubstTransportState.AtWork, onSourceToWork);
			DefineTransition(SubstTransportState.AtWork, Event.SubstToSource, SubstTransportState.AtSource, onWorkToSource);
			DefineTransition(SubstTransportState.AtWork, Event.SubstToWork, SubstTransportState.AtWork, onWorkToWork);
			DefineTransition(SubstTransportState.AtWork, Event.SubstToDestination, SubstTransportState.AtDestination, onWorkToDestination);
			DefineTransition(SubstTransportState.AtDestination, Event.SubstToWork, SubstTransportState.AtWork, onDestinationToWork);
			DefineTransition(SubstTransportState.AtDestination, Event.SubstRemoved, null, null);
			DefineTransition(SubstTransportState.AtSource, Event.SubstRemoved, null, null);
			DefineTransition(SubstTransportState.AtWork, Event.SubstRemoved, null, null);
			DefineTransition(SubstTransportState.AtDestination, Event.SubstToSource, SubstTransportState.AtSource, onDestinationToSource);
			base.CurrentState = SubstTransportState.AtSource;
		}

		public override void Start()
		{
			base.Start();
			TriggerEvent(Event.SubstToSource);
		}

		private void onInitToSource(object arg)
		{
			
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans01_InitToSource, null, base.ActionHost);
		}

		private void onSourceToWork(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Substrate substrate = arg as Substrate;
			substrate.SubstLocID = substrate.SubstrateHistory?.Count > 0 ? substrate.SubstrateHistory.FindLast(a => a.SubstLocID != null).SubstLocID : substrate.SubstLocID;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans02_SourceToWork, null, base.ActionHost);
		}

		private void onWorkToSource(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Substrate substrate = arg as Substrate;
			substrate.SubstLocID = substrate.SubstrateHistory?.Count > 0 ? substrate.SubstrateHistory.FindLast(a => a.SubstLocID != null).SubstLocID : substrate.SubstLocID;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans03_WorkToSource, null, base.ActionHost);
		}

		private void onWorkToWork(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Substrate substrate = arg as Substrate;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.OccupiedTrans02UnOccupied, null, base.ActionHost);
			substrate.SubstLocID = substrate.SubstrateHistory?.Count > 0 ? substrate.SubstrateHistory.FindLast(a => a.SubstLocID != null).SubstLocID : substrate.SubstLocID;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.UnOccupiedTrans01Occupied, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans04_WorkToWork, null, base.ActionHost);
		}

		private void onWorkToDestination(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Substrate substrate = arg as Substrate;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.OccupiedTrans02UnOccupied, null, base.ActionHost);
			substrate.SubstLocID = substrate.SubstrateHistory?.Count > 0 ? substrate.SubstrateHistory.FindLast(a => a.SubstLocID != null).SubstLocID : substrate.SubstLocID;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.UnOccupiedTrans01Occupied, null, base.ActionHost);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans05_WorkToDestination, null, base.ActionHost);
		}

		private void onDestinationToWork(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Substrate substrate = arg as Substrate;
			substrate.SubstLocID = substrate.SubstrateHistory?.Count > 0 ? substrate.SubstrateHistory.FindLast(a => a.SubstLocID != null).SubstLocID : substrate.SubstLocID;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans06_DestinationToWork, null, base.ActionHost);
		}

		private void onDestinationToSource(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Substrate substrate = arg as Substrate;
			substrate.SubstLocID = substrate.SubstrateHistory?.Count > 0 ? substrate.SubstrateHistory.FindLast(a => a.SubstLocID != null).SubstLocID : substrate.SubstLocID;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SubstSMTrans08_DestinationToSource, null, base.ActionHost);
		}
	}
}
