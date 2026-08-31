using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;
using SecsLite300.E90;

namespace SecsLite300.E87
{
	public class Carrier : OSSObject, ICarrier
	{
		private string m_currentMoveDestination = "";

		private SlotState[] m_ecsSlotmMapData = null;

		public List<ISubstrate> SubstList = null;

		private ISubstrateLocation[] m_substLocationArray = null;

		private FSMCarrierID m_fsmCarrierID = null;

		private FSMCarrierSlotMap m_fsmCarrierSlotMap = null;

		private FSMCarrierAccessing m_fsmCarrierAccessing = null;

		private static readonly string attrCarrierID = "CarrierID";

		private static readonly string attrCapacity = "Capacity";

		private static readonly string attrCarrierAccessingStatus = "CarrierAccessingStatus";

		private static readonly string attrCarrierIDStatus = "CarrierIDStatus";

		private static readonly string attrContentMap = "ContentMap";

		private static readonly string attrLocationID = "LocationID";

		private static readonly string attrSlotMap = "SlotMap";

		private static readonly string attrSlotMapStatus = "SlotMapStatus";

		private static readonly string attrSubstrateCount = "SubstrateCount";

		private static readonly string attrUsage = "Usage";

		private static readonly string attrPortID = "PortID";  

		private static readonly string attrCarrierPortTransferState = "PortTransferState";

		private static readonly string attrPortAssociateState = "PortAssociationState";

		private static readonly string attrClampState = "PortClampState";

		public int Capacity { get; set; }

		public uint SubstrateCount { get; set; }

		public string Usage { get; set; }

		public CarrierIDStatus IDStatus => (CarrierIDStatus)m_fsmCarrierID.CurrentState;

		public CarrierAccessingStatus AccessingStatus => (CarrierAccessingStatus)m_fsmCarrierAccessing.CurrentState;

		public CarrierSlotMapStatus SlotMapStatus => (CarrierSlotMapStatus)m_fsmCarrierSlotMap.CurrentState;

		public CarrierSlotInfo[] ContentMapArray { get; set; }

		public SlotState[] SlotMapArray { get; set; }

		public string LocationID { get; set; }

		public ILoadPort CurrentLoadPort { get; set; }

		public string CarrierID => base.ObjID;

		public ISubstrateLocation[] CarrierSubstrateLocations => m_substLocationArray;

		public virtual string ClassName => GetType().Name;

		public Carrier()
			: base("", "Carrier")
		{
			m_fsmCarrierID = new FSMCarrierID(this);
			m_fsmCarrierID.Name = base.ObjID + "_ID";
			m_fsmCarrierID.Start();
			m_fsmCarrierSlotMap = new FSMCarrierSlotMap(this);
			m_fsmCarrierSlotMap.Name = base.ObjID + "_SlotMap";
			m_fsmCarrierSlotMap.Start();
			m_fsmCarrierAccessing = new FSMCarrierAccessing(this);
			m_fsmCarrierAccessing.Name = base.ObjID + "_Accessing";
			m_fsmCarrierAccessing.Start();
		}

		public void Initialize()
		{
			m_fsmCarrierAccessing.Start();
			m_fsmCarrierID.Start();
			m_fsmCarrierSlotMap.Start();
			if (m_substLocationArray != null)
			{
				ISubstrateLocation[] substLocationArray = m_substLocationArray;
				foreach (ISubstrateLocation substrateLocation in substLocationArray)
				{
					SubstrateLocation substrateLocation2 = substrateLocation as SubstrateLocation;
					substrateLocation2.Initialize();
				}
			}
		}
		public override void Release()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start CarrierID={CarrierID}");
			m_fsmCarrierAccessing.Stop();
			m_fsmCarrierID.Stop();
			m_fsmCarrierSlotMap.Stop();
			if (m_substLocationArray != null)
			{
				ISubstrateLocation[] substLocationArray = m_substLocationArray;
				foreach (ISubstrateLocation substrateLocation in substLocationArray)
				{
					SubstrateLocation substrateLocation2 = substrateLocation as SubstrateLocation;
				    substrateLocation2?.Release();
				}
			}
            if (SubstList != null)
            {
				foreach (Substrate substrate in SubstList)
				{
					substrate?.Release();
				}
			}
			base.Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		public override SecsItem GetAttribute(string attrName)
		{
			SecsItem attribute = base.GetAttribute(attrName);
			if (attribute != null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: itemValue != null attrName={attrName}");
				return attribute;
			}
			if (OSSObject.AttrNameEqual(attrName, attrCapacity))
			{
				return new SecsItem(Convert.ToByte(Capacity));
			}
			if (OSSObject.AttrNameEqual(attrName, attrCarrierID))
			{
				return new SecsItem(base.ObjID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrCarrierAccessingStatus))
			{
				return new SecsItem((byte)AccessingStatus);
			}
			if (OSSObject.AttrNameEqual(attrName, attrCarrierIDStatus))
			{
				return new SecsItem((byte)IDStatus);
			}
			if (OSSObject.AttrNameEqual(attrName, attrContentMap))
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
                if (ContentMapArray?.Length > 0)
                {
					for (int i = 0; i < ContentMapArray.Length; i++)
					{
						CarrierSlotInfo carrierSlotInfo = ContentMapArray[i];
						SecsItem secsItem2 = new SecsItem(SecsFormat.List);
						secsItem2.AppendItem(new SecsItem(carrierSlotInfo.LotID));
						secsItem2.AppendItem(new SecsItem(carrierSlotInfo.SubstrateID));
						secsItem.AppendItem(secsItem2);
					}
				}
				return secsItem;
			}
			if (OSSObject.AttrNameEqual(attrName, attrLocationID))
			{
				return new SecsItem(LocationID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrSlotMap))
			{
				SecsItem secsItem3 = new SecsItem(SecsFormat.List);
                if (m_ecsSlotmMapData?.Length > 0)
                {
					for (int j = 0; j < m_ecsSlotmMapData.Length; j++)
					{
						int value = Convert.ToInt32(m_ecsSlotmMapData[j]);
						secsItem3.AppendItem(new SecsItem(Convert.ToByte(value)));
					}
				}
				return secsItem3;
			}
			if (OSSObject.AttrNameEqual(attrName, attrSlotMapStatus))
			{
				return new SecsItem((byte)SlotMapStatus);
			}
			if (OSSObject.AttrNameEqual(attrName, attrSubstrateCount))
			{
				return new SecsItem(Convert.ToByte(SubstrateCount));
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortID) && CurrentLoadPort != null)
			{
				return new SecsItem(Convert.ToByte(CurrentLoadPort.PortID));
			}
			if (OSSObject.AttrNameEqual(attrName, attrCarrierPortTransferState))
			{
				return new SecsItem(Convert.ToByte(CurrentLoadPort.TransferStatus));
			}
			if (OSSObject.AttrNameEqual(attrName, attrPortAssociateState))
			{
				return new SecsItem(Convert.ToByte(CurrentLoadPort.PortAssociationState));
			}
			if (OSSObject.AttrNameEqual(attrName, attrClampState))
			{
				return new SecsItem(Convert.ToByte(CurrentLoadPort.ClampState));
			}
			
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: not find attrName={attrName}");
			return null;
		}

		public void CreateSubstLocation(int slotCount)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			if (m_substLocationArray != null)
			{
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: substrate location array was already created.");
			}
			ISubstrateLocation[] array = (m_substLocationArray = new SubstrateLocation[slotCount]);
			for (int i = 0; i < slotCount; i++)
			{
				SubstrateLocation substrateLocation = new SubstrateLocation($"{CarrierID}.{i + 1}");
				m_substLocationArray[i] = substrateLocation;
			}
		}

		public void SwitchIDStatusToVerifyOK()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierID.TriggerEvent(FSMCarrierID.Event.IDVerificationOK);
		}

		public void SwitchIDStatusToVerifyFailed()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierID.TriggerEvent(FSMCarrierID.Event.IDVerificationFailed);
			Gem300Manager.EquipmentObject.OnIdVerifyFailed(this);
		}

		public void SwitchSlotMapStatusToNotRead()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierSlotMap.TriggerEvent(FSMCarrierSlotMap.Event.NotRead);
		}

		public void SwitchSlotMapStatusToVerifyOK()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierSlotMap.TriggerEvent(FSMCarrierSlotMap.Event.VerificationOK);
		}

		public void SwitchSlotMapStatusToVerifyFailed()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierSlotMap.TriggerEvent(FSMCarrierSlotMap.Event.VerificationFailed);
			Gem300Manager.EquipmentObject.OnSlotMapVerifyFailed(this);
		}

		public int AccessCompleted()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierAccessing.TriggerEvent(FSMCarrierAccessing.Event.EquipmentAccessFinished);
			return 0;
		}

		public int AccessStarted()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierAccessing.TriggerEvent(FSMCarrierAccessing.Event.EquipmentStartAccessing);
			return 0;
		}

		public int AccessStopped()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmCarrierAccessing.TriggerEvent(FSMCarrierAccessing.Event.EquipmentAccessFailed);
			return 0;
		}

		public int CarrierApproachingComplete()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierApproachingComplete, null, this);
			return 0;
		}

		public int CarrierEject()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			LoadPort loadPort = CurrentLoadPort as LoadPort;
			loadPort.SwitchToReadyToUnload();
			Gem300Manager.EquipmentObject.OnCarrierTagReleased(this, null);
			return 0;
		}

		public int CarrierIdVerify(DataStatus status, string id) 
		{
            if (status != DataStatus.ReadFailed)
            {
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
				base.ObjID = id;
				SwitchSlotMapStatusToNotRead();
				Thread.Sleep(50);
				m_fsmCarrierID.TriggerEvent(FSMCarrierID.Event.WaitForHost);
				Thread.Sleep(50);
				CurrentLoadPort.PortAssociationState = PortAssociationStatus.ASSOCIATED;
				Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.AssocSMGoAssoc, null, this);
				return 0;
			}
            else
            {
				Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierIDReadFail, null, this);
				CurrentLoadPort.PortAssociationState = PortAssociationStatus.ASSOCIATED;
				Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.AssocSMGoAssoc, null, this);
				return 0;
            }
		}

		public int CarrierMoveStarted(int destination)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start£¬ destination={destination}");
			if (CurrentLoadPort == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: carrier can't be moved, CurrentLoadPort is null");
				return -1;
			}
			int num = destination - (CurrentLoadPort.PortID - 1) * 2;
			if (num != 1 && num != 2)
			{
				Console.WriteLine($"CarrierMoveStarted: invalid destination[{destination}]");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: CarrierMoveStarted: invalid destination[{destination}]");
				return -1;
			}
			m_currentMoveDestination = ((num == 1) ? "LP" : "FIMS") + $"{CurrentLoadPort.PortID}";
			return 0;
		}

		public int CarrierMoveCompleted()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			LocationID = m_currentMoveDestination;
			m_currentMoveDestination = "";
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierLocationChanged, null, this);
			return 0;
		}

		public int CarrierMoveStopped()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_currentMoveDestination = "";
			return 0;
		}

		public int ClampStateChanged(OnOffState clamped)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			switch (clamped)
			{
				case OnOffState.OnState:
					if (CurrentLoadPort != null)
					{
						CurrentLoadPort.ClampState = clamped;
					}
					Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierClamped, null, this);
					break;
				case OnOffState.OffState:
					if (CurrentLoadPort != null)
					{
						CurrentLoadPort.ClampState = clamped;
					}
					Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierUnclamped, null, this);
					break;
				case OnOffState.Pulse:
					Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierClamped, null, this);
					Thread.Sleep(10);
					Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierUnclamped, null, this);
					break;
			}
			return 0;
		}

		public int RemoveCarrier()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public int DoorClosed()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierTagReleased, null, this);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierDoorClosed, null, this);
			return 0;
		}

		public int DoorOpened()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.CarrierDoorOpened, null, this);
			return 0;
		}

		public int SlotMapVerify(DataStatus status, ref SlotState[] slotmap, int capacity)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			if (status == DataStatus.ReadOK)
			{
				m_ecsSlotmMapData = slotmap;
			}
			else
			{
				m_ecsSlotmMapData = null;
			}
			m_fsmCarrierSlotMap.TriggerEvent(FSMCarrierSlotMap.Event.WaitForHost);
			return 0;
		}

		public bool CompareSlotMapResult(SlotState[] slotArray)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			if (m_ecsSlotmMapData != null)
			{
				if (m_ecsSlotmMapData.Length != slotArray.Length)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error m_ecsSlotmMapData.Length£º{m_ecsSlotmMapData.Length} != slotArray.Length£º{slotArray.Length}");
					return false;
				}
				for (int i = 0; i < m_ecsSlotmMapData.Length; i++)
				{
					if (m_ecsSlotmMapData[i] != slotArray[i])
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error m_ecsSlotmMapData slot£º{i}  Mapdata£º{m_ecsSlotmMapData[i]} != slotArray slot:{i} Mapdata:{slotArray[i]}");
						return false;
					}
				}
			}
			return true;
		}
	}
}
