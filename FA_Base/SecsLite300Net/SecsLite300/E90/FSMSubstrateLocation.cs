using FALogger;
using FSMLite;
using System.Reflection;

namespace SecsLite300.E90
{
	internal class FSMSubstrateLocation : FSMBase
	{
		public virtual string ClassName => GetType().Name;

		public enum Event : ushort
		{
			MoveIn,
			MoveOut
		}

		public FSMSubstrateLocation(object host)
			: base(host, FSMRunMode.Thread)
		{
			DefineTransition(SubstLocState.Unoccupied, Event.MoveIn, SubstLocState.Occupied, onOccupied);
			DefineTransition(SubstLocState.Occupied, Event.MoveOut, SubstLocState.Unoccupied, onUnoccupied);
			base.CurrentState = SubstLocState.Unoccupied;
		}

		private void onOccupied(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.UnOccupiedTransOccupied);
		}

		private void onUnoccupied(object arg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.OccupiedTransUnOccupied);
		}
	}
}
