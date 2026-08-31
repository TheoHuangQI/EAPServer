using System;
using System.Reflection;
using System.Threading;
using FALogger;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;
using SecsLite300.E39;

namespace SecsLite300.E87
{
	public class LoadPort : OSSObject, ILoadPort
	{
		private static readonly string attrPortID = "PortID";

		private static readonly string attrPortAccessMode = "AccessMode";

		private static readonly string attrPortTransferState = "PortTransferState";

		private static readonly string attrPortReservationState = "PortReservationState";

		private static readonly string attrPortAssociateState = "PortAssociationState";

		private static readonly string attrCarrier = "CarrierID";

		private static readonly string attrClampState = "PortClampState";


		private Location m_locationLP = null;

        private Location m_locationFIMS = null;

		private FSMLoadPortTransfer m_fsm = null;


		private LoadPortTransferStatus m_prevStatus = LoadPortTransferStatus.OutOfService;

		public LoadPortTransferStatus TransferStatus => (LoadPortTransferStatus)m_fsm.CurrentState;

		public byte PortID { get; }

		public LoadPortAccessMode AccessMode { get; set; }

		public ICarrier CurrentCarrier { get; set; }

		public string PortName => base.ObjID;

		public bool BypassReadID { get; set; }

		public bool ReservationState { get; set; }
		public virtual string ClassName => GetType().Name;
		public PortAssociationStatus PortAssociationState { get; set; }
		public OnOffState ClampState { get; set; }
		public LoadPort(byte ptn)
			: base($"LP{ptn}", "LoadPort")
		{
			PortID = ptn;
			BypassReadID = false;
            m_locationLP = new Location(ptn, LoadPortLocationType.LoadPort);
            m_locationFIMS = new Location(ptn, LoadPortLocationType.FIMS);
            m_fsm = new FSMLoadPortTransfer(this);
			m_fsm.Name = base.ObjID;
			m_prevStatus = TransferStatus;
			m_fsm.Start();
		}

		public override SecsItem GetAttribute(string attrName)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName={attrName} ");
			SecsItem attribute = base.GetAttribute(attrName);
			if (attribute != null)
			{
				return attribute;
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortID))
			{
				return new SecsItem(PortID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortAccessMode))
			{
				return new SecsItem((byte)AccessMode);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortTransferState))
			{
				return new SecsItem((byte)TransferStatus);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortReservationState))
			{
				return new SecsItem(ReservationState);
			}
			if (OSSObject.AttrNameEqual(attrName, attrCarrier))
			{
				return new SecsItem(CurrentCarrier?.CarrierID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortAssociateState))
			{
				return new SecsItem((byte)PortAssociationState);
			}
			if(OSSObject.AttrNameEqual(attrName, attrClampState))
            {
				return new SecsItem((byte)ClampState);
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Not find attrName={attrName} ");
			return null;
		}

		private void storeCurrentStatus()
		{
			m_prevStatus = TransferStatus;
		}

		public void SwitchToBlockedStatus()
		{

			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			storeCurrentStatus();
			m_fsm.TriggerEvent(FSMLoadPortTransfer.Event.SwitchToTransferBlocked);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		public void SwitchToReadyToLoad()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			m_fsm.TriggerEvent(FSMLoadPortTransfer.Event.SwitchToReadyToLoad);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		public void SwitchToReadyToUnload()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			m_fsm.TriggerEvent(FSMLoadPortTransfer.Event.SwitchToReadyToUnload);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		public void SwitchReservationMode(bool newMode)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			ReservationState = newMode;
			Gem300Manager.EquipmentObject.PostNamedEvent(newMode ? FA300Event.LoadPortReservationOn : FA300Event.LoadPortReservationOff, null, this);
			if (newMode)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {OnOffState.OnState.ToString()} ");
				Gem300Manager.EquipmentObject.OnSetReservationVisibleSignal(this, OnOffState.OnState);
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
		}

		public ICarrier LoadTransferCompleted()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			Carrier carrier = new Carrier
			{
				LocationID = PortName,
				CurrentLoadPort = this
			};
			Gem300Equipment equipmentObject = Gem300Manager.EquipmentObject;
			equipmentObject.CarrierList.Add(carrier);
			equipmentObject.OnCarrierActive(carrier, OnOffState.OnState);
			if(TransferStatus != LoadPortTransferStatus.TransferBlocked)
            {
				SwitchToBlockedStatus();
			}
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.MaterialReceivedEvent, null, this);
			CurrentCarrier = carrier;
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End ");
			return CurrentCarrier;
		}

		private void postFA300Alarm(string alarmName, OnOffState onOffState)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: alarmName={alarmName},onOffState={onOffState} ");
			switch (onOffState)
			{
			case OnOffState.OnState:
				Gem300Manager.EquipmentObject.PostNamedAlarm(alarmName, ALCD.Set);
				break;
			case OnOffState.OffState:
				Gem300Manager.EquipmentObject.PostNamedAlarm(alarmName, ALCD.Cleared);
				break;
			case OnOffState.Pulse:
				Gem300Manager.EquipmentObject.PostNamedAlarm(alarmName, ALCD.Set);
				Thread.Sleep(10);
				Gem300Manager.EquipmentObject.PostNamedAlarm(alarmName, ALCD.Cleared);
				break;
			}
		}

		public int AlarmLoadPortOutOfService(OnOffState state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			postFA300Alarm(FA300Alarm.AlarmLoadPortOOS, state);
			return 0;
		}

		public int AlarmAccessMode(OnOffState state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,OnOffState={state} ");
			postFA300Alarm(FA300Alarm.AlarmPortAccessMode, state);
			return 0;
		}

		public int AlarmCarrierPlacement(OnOffState state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,OnOffState={state} ");
			postFA300Alarm(FA300Alarm.AlarmCarrierPlacementError, state);
			return 0;
		}

		public int AlarmPIO(OnOffState state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,OnOffState={state} ");
			postFA300Alarm(FA300Alarm.AlarmPIO, state);
			return 0;
		}

		public int AMHSArrived()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			return 0;
		}

		public int ClearPort()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			SwitchToReadyToLoad();
			if (CurrentCarrier != null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: this.CurrentCarrier != null ");
				Gem300Equipment equipmentObject = Gem300Manager.EquipmentObject;
				equipmentObject.CarrierList.Remove(CurrentCarrier);
				Carrier carrier = CurrentCarrier as Carrier;
				carrier.Release();
				CurrentCarrier = null;
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: CurrentCarrier= null ");
			}
			return 0;
		}

		public int TransferCommitted()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start ");
			SwitchToBlockedStatus();
			return 0;
		}

		public int TransferStopped()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,m_prevStatus={m_prevStatus}");
			if (TransferStatus != LoadPortTransferStatus.TransferBlocked)
			{
				Console.WriteLine($"invalid LoadPortTransferStatus state: {TransferStatus}");
				return -1;
			}
			if (m_prevStatus == LoadPortTransferStatus.ReadyToLoad)
			{
				SwitchToReadyToLoad();
			}
			else
			{
				if (m_prevStatus != LoadPortTransferStatus.ReadyToLoad)
				{
					Console.WriteLine($"previous state is {m_prevStatus}, needn't stop. ");
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: previous state is {m_prevStatus}, needn't stop");
					return -1;
				}
				SwitchToReadyToUnload();
			}
			return 0;
		}

		public int UnloadTransferCompleted()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			if (TransferStatus != LoadPortTransferStatus.TransferBlocked)
			{
				SwitchToBlockedStatus();
			}
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.E87ReadyToUnLoadTrans07TransferBlocked, null, this);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.MaterialRemovedEvent, null, this);
			PortAssociationState = PortAssociationStatus.NOTASSOCIATED;
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.AssocToNotAssoc, null, this);
			Gem300Equipment equipmentObject = Gem300Manager.EquipmentObject;
            if (CurrentCarrier != null)
            {
				equipmentObject.CarrierList.Remove(CurrentCarrier);
				equipmentObject.OnCarrierActive(CurrentCarrier, OnOffState.OffState);
			}
			SwitchToReadyToLoad();
			return 0;
		}

		public void Initialize()
		{
			m_fsm.Start();
		}

		public override void Release()
		{
			m_fsm.Stop();
			base.Release();
		}
	}
}
