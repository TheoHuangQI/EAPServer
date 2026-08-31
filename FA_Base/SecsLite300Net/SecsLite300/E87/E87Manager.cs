using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FALogger;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;
using SecsLite300.E39;
using SecsLite300.E40;
using SecsLite300.E90;

namespace SecsLite300.E87
{
	public class E87Manager : IE39Module
	{
		private delegate CAACK CarrierAction(string carrierId, byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList);

		private delegate CAACK PortAction(byte ptn, List<KeyValuePair<string, SecsItem>> paramList, ref List<KeyValuePair<ERRCODE, string>> errList);

		private int m_loadPortCount = 0;

		private Dictionary<string, CarrierAction> m_carrierActionDict = new Dictionary<string, CarrierAction>();

		private Dictionary<string, PortAction> m_portActionDict = new Dictionary<string, PortAction>();

		private IE87Impl m_impl = null;

		private OssEquipment m_eq = null;

		private ILoadPort[] m_loadPortArray = null;

		private List<ICarrier> m_carrierList = new List<ICarrier>();

		public int LoadPortCount => m_loadPortCount;

		public List<ICarrier> CarrierList => m_carrierList;

		public virtual string ClassName => GetType().Name;

		public bool Initialize(OssEquipment equipment)
		{
			registerCarrierActionHandler("ProceedWithCarrier", doProceedWithCarrier);
			registerCarrierActionHandler("CancelCarrier", doCancelCarrier);
			registerCarrierActionHandler("CarrierRelease", doCarrierRelease);
			registerCarrierActionHandler("Bind", doBindCarrier);
			registerPortActionHandler("ChangeServiceStatus", doChangeServiceStatus);
			registerPortActionHandler("CancelReservationAtPort", doCancelReservationAtPort);
			registerPortActionHandler("ReserveAtPort", doReserveAtPort);
			if (!equipment.RegisterStreamFunctionHandler(3, 17, this))
			{
				return false;
			}
			if (!equipment.RegisterStreamFunctionHandler(3, 19, this))
			{
				return false;
			}
			if (!equipment.RegisterStreamFunctionHandler(3, 25, this))
			{
				return false;
			}
			if (!equipment.RegisterStreamFunctionHandler(3, 27, this))
			{
				return false;
			}
			if (!equipment.RegisterStreamFunctionHandler(3, 29, this))
			{
				return false;
			}
			if (!equipment.RegisterStreamFunctionHandler(3, 31, this))
			{
				return false;
			}
			m_eq = equipment;
			m_impl = equipment as IE87Impl;
			equipment.RegisterObjectTypeHandler("Carrier", this);
			ILoadPort[] loadPortArray = m_loadPortArray;
			foreach (ILoadPort loadPort in loadPortArray)
			{
				LoadPort loadPort1 = loadPort as LoadPort;
				loadPort1.Initialize();
			}
			foreach (ICarrier carrier2 in CarrierList)
			{
				Carrier carrier = carrier2 as Carrier;
				carrier.Initialize();
			}
			return true;
		}

		public void Uninitialize(OssEquipment equipment)
		{
			ILoadPort[] loadPortArray = m_loadPortArray;
			foreach (ILoadPort loadPort in loadPortArray)
			{
				loadPort.Release();
			}
			foreach (ICarrier carrier2 in CarrierList)
			{
				Carrier carrier = carrier2 as Carrier;
				carrier.Release();
			}
			equipment.UnRegisterStreamFunctionHandler();
			equipment.UnRegisterObjectTypeHandler();
		}

		public void e87InitLoadport(int lpCount)
		{
			if (m_loadPortCount > 0)
			{
				throw new Exception($"LoadPortCount already set, count = {m_loadPortCount}");
			}
			ILoadPort[] array = (m_loadPortArray = new LoadPort[lpCount]);
			for (int i = 0; i < lpCount; i++)
			{
				m_loadPortArray[i] = new LoadPort((byte)(i + 1));
			}
			m_loadPortCount = lpCount;
		}

		public int ServiceRequest()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public object CreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, ref SecsItem replyItem, ref ERRCODE errCode, ref string errText)
		{
			if (objType.Equals("Carrier", StringComparison.OrdinalIgnoreCase))
			{
			}
			return null;
		}

		public SecsErrorCode ProcessMessage(byte stream, byte func, SecsItem item, uint msgID)
		{
			byte b = stream;
			byte b2 = b;
			if (b2 == 3)
			{
				return func switch
				{
					17 => onCarrierActionRequest(item, msgID), 
					19 => onCancelAllCarrierOutRequest(item, msgID), 
					25 => onPortActionRequest(item, msgID), 
					27 => onChangeAccessRequest(item, msgID), 
					29 => onCarrierTagReadRequest(item, msgID), 
					31 => onCarrierTagWriteRequest(item, msgID), 
					_ => SecsErrorCode.ErrorFunctionType, 
				};
			}
			return SecsErrorCode.ErrorStreamType;
		}

		public ILoadPort GetLoadPort(byte ptn)
		{
			if (ptn == 0 || ptn > m_loadPortArray.Length)
			{
				return null;
			}
			return m_loadPortArray[ptn - 1];
		}

		public bool ChangeLoadPortAccessMode(List<byte> ptnList, LoadPortAccessMode newMode, ref SecsItem itemErrList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: newMode={newMode}");
			if (ptnList == null)
			{
				ILoadPort[] loadPortArray = m_loadPortArray;
				foreach (ILoadPort loadPort in loadPortArray)
				{
					if (!switchLoadPortAccessMode(loadPort, newMode, ref itemErrList))
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: port={loadPort},newMode={newMode}");
						return false;
					}
				}
			}
			else
			{
				foreach (byte ptn in ptnList)
				{
					ILoadPort loadPort2 = GetLoadPort(ptn);
					if (!switchLoadPortAccessMode(loadPort2, newMode, ref itemErrList))
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: port={loadPort2},newMode={newMode}");
						return false;
					}
				}
			}
			return true;
		}

		private bool switchLoadPortAccessMode(ILoadPort port, LoadPortAccessMode newMode, ref SecsItem itemErrList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			int num = Gem300Manager.EquipmentObject.OnCancelTransfer(port);
			if (num != 0)
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				secsItem.AppendItem(new SecsItem(port.PortID));
				secsItem.AppendItem(new SecsItem((uint)num));
				secsItem.AppendItem(new SecsItem($"ECS refuse to change the access mode of {port.PortID}, OnCancelTransfer returned {num} "));
				if (itemErrList == null)
				{
					itemErrList = new SecsItem(SecsFormat.List);
				}
				itemErrList.AppendItem(secsItem);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error ecsResult :{num} != 0");
				return false;
			}
			if (port.ReservationState)
			{
				SecsItem secsItem2 = new SecsItem(SecsFormat.List);
				secsItem2.AppendItem(new SecsItem(port.PortID));
				secsItem2.AppendItem(new SecsItem(15u));
				secsItem2.AppendItem(new SecsItem($"LP{port.PortID} is reserved."));
				if (itemErrList == null)
				{
					itemErrList = new SecsItem(SecsFormat.List);
				}
				itemErrList.AppendItem(secsItem2);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error port.ReservationState");
				return false;
			}
			bool flag = false;
			flag = Gem300Manager.EquipmentObject.OnLoadPortAccessModeChanged(port, newMode);
            if (flag)
            {
				LoadPort loadPort = port as LoadPort;
				loadPort.AccessMode = newMode;
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: newMode={newMode}");
				if (newMode == LoadPortAccessMode.Auto && flag)
				{
					Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.LoadPortAccessModeChangedAuto, null, port);
				}
				else if (newMode == LoadPortAccessMode.Manual && flag)
				{
					Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.LoadPortAccessModeChangedManual, null, port);
				}
			}
			return flag;
		}

		private SecsErrorCode onCarrierActionRequest(SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,msgID= {msgID}");
			if (item.Format != 0 || item.ItemList.Count != 5)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error item.Format != SecsFormat.List({item.Format})||item.ItemList.Count != 5({item.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (!secsItem.IsUnsignedInterger())
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error !itemDataID.IsUnsignedInterger()");
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(secsItem.Value);
			SecsItem secsItem2 = item.ItemList[1];
			if (secsItem2.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemAction.Format != SecsFormat.Ascii({secsItem2.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string actionName = secsItem2.Value as string;
			SecsItem secsItem3 = item.ItemList[2];
			if (secsItem3.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemCID.Format != SecsFormat.Ascii({secsItem3.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string carrierId = secsItem3.Value as string;
			SecsItem secsItem4 = item.ItemList[3];
			if (secsItem4.Format != SecsFormat.U1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemPTN.Format != SecsFormat.U1({secsItem4.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			byte ptn = (byte)secsItem4.Value;
			SecsItem secsItem5 = item.ItemList[4];
			if (secsItem5.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemCAttrList.Format != SecsFormat.List({secsItem5.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			List<KeyValuePair<string, SecsItem>> list = new List<KeyValuePair<string, SecsItem>>();
			for (int i = 0; i < secsItem5.ItemList.Count; i++)
			{
				SecsItem secsItem6 = secsItem5.ItemList[i];
				if (secsItem6.Format != 0 || secsItem6.ItemList.Count != 2)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemCAttr.Format != SecsFormat.List({secsItem6.Format})||itemCAttr.ItemList.Count != 2({secsItem6.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				SecsItem secsItem7 = secsItem6.ItemList[0];
				if (secsItem7.Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemAttrID.Format != SecsFormat.Ascii({secsItem7.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				string key = secsItem7.Value as string;
				SecsItem value = secsItem6.ItemList[1];
				list.Add(new KeyValuePair<string, SecsItem>(key, value));
			}
			List<KeyValuePair<ERRCODE, string>> errList = new List<KeyValuePair<ERRCODE, string>>();
			CAACK v = runCarrierAction(actionName, carrierId, ptn, list, ref errList);
			SecsItem secsItem8 = new SecsItem(SecsFormat.List);
			secsItem8.AppendItem(new SecsItem((byte)v));
			SecsItem secsItem9 = new SecsItem(SecsFormat.List);
			secsItem8.AppendItem(secsItem9);
			if (errList != null)
			{
				for (int j = 0; j < errList.Count; j++)
				{
					KeyValuePair<ERRCODE, string> keyValuePair = errList[j];
					SecsItem secsItem10 = new SecsItem(SecsFormat.List);
					secsItem10.AppendItem(new SecsItem((uint)keyValuePair.Key));
					secsItem10.AppendItem(new SecsItem(keyValuePair.Value));
					secsItem9.AppendItem(secsItem10);
				}
			}
			m_eq.ReplyMessage(3, 18, secsItem8, msgID);
			return SecsErrorCode.Success;
		}

		private bool registerCarrierActionHandler(string actionName, CarrierAction handler)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,actionName={actionName}");
			string text = actionName.ToUpper();
			if (m_carrierActionDict.ContainsKey(text))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: CarrierAction [{text}] is registered already.");
				return false;
			}
			m_carrierActionDict[text] = handler;
			return true;
		}

		private CarrierAction findCarrierActionHandler(string actionName)
		{
			string key = actionName.ToUpper();
			if (m_carrierActionDict.ContainsKey(key))
			{
				return m_carrierActionDict[key];
			}
			return null;
		}

		private bool registerPortActionHandler(string actionName, PortAction handler)
		{
			string text = actionName.ToUpper();
			if (m_portActionDict.ContainsKey(text))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PortAction [{text}] is registered already.");
				return false;
			}
			m_portActionDict[text] = handler;
			return true;
		}

		private PortAction findPortActionHandler(string actionName)
		{
			string key = actionName.ToUpper();
			if (m_portActionDict.ContainsKey(key))
			{
				return m_portActionDict[key];
			}
			return null;
		}

		private CAACK runCarrierAction(string actionName, string carrierId, byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,actionName={actionName}");
			return findCarrierActionHandler(actionName)?.Invoke(carrierId, ptn, attrList, ref errList) ?? CAACK.InvalidCommand;
		}

		private CAACK runPortAction(string actionName, byte ptn, List<KeyValuePair<string, SecsItem>> paramList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,actionName={actionName}");
			return findPortActionHandler(actionName)?.Invoke(ptn, paramList, ref errList) ?? CAACK.InvalidCommand;
		}

		private SecsErrorCode onCancelAllCarrierOutRequest(SecsItem item, uint msgID)
		{
			if (item != null)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onPortActionRequest(SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,msgID ={msgID}");
			if (item == null || item.Format != 0 || item.ItemList.Count != 3)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {item == null} {item.Format != SecsFormat.List}{item.ItemList.Count != 3}");
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = "";
			byte b = 0;
			SecsItem secsItem = item.ItemList[0];
			text = secsItem.Value as string;
			SecsItem secsItem2 = item.ItemList[1];
			b = (byte)secsItem2.Value;
			SecsItem secsItem3 = item.ItemList[3];
			if (secsItem3.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemParamList.Format != SecsFormat.List({secsItem3.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			List<KeyValuePair<string, SecsItem>> list = new List<KeyValuePair<string, SecsItem>>();
			for (int i = 0; i < secsItem3.ItemList.Count; i++)
			{
				SecsItem secsItem4 = secsItem3.ItemList[i];
				if (secsItem4.Format != 0 || secsItem4.ItemList.Count != 2)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemParam.Format != SecsFormat.List({secsItem4.Format})||itemParam.ItemList.Count != 2({secsItem4.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				SecsItem secsItem5 = secsItem4.ItemList[0];
				if (secsItem5.Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error itemParamList.Format != SecsFormat.List({secsItem3.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				string key = secsItem5.Value as string;
				SecsItem value = secsItem4.ItemList[1];
				list.Add(new KeyValuePair<string, SecsItem>(key, value));
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: portActionName={text} portNumber={b}");
			List<KeyValuePair<ERRCODE, string>> errList = null;
			CAACK v = runPortAction(text, b, list, ref errList);
			SecsItem secsItem6 = new SecsItem(SecsFormat.List);
			secsItem6.AppendItem(new SecsItem((byte)v));
			SecsItem secsItem7 = new SecsItem(SecsFormat.List);
			secsItem6.AppendItem(secsItem7);
			if (errList != null)
			{
				for (int j = 0; j < errList.Count; j++)
				{
					KeyValuePair<ERRCODE, string> keyValuePair = errList[j];
					SecsItem secsItem8 = new SecsItem(SecsFormat.List);
					secsItem8.AppendItem(new SecsItem((uint)keyValuePair.Key));
					secsItem8.AppendItem(new SecsItem(keyValuePair.Value));
					secsItem7.AppendItem(secsItem8);
				}
			}
			m_eq.ReplyMessage(3, 26, secsItem6, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onChangeAccessRequest(SecsItem item, uint msgID)
		{
			if (item == null || item.Format != 0 || item.ItemList.Count != 2)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {item == null} ||{item.Format != SecsFormat.List}||{item.ItemList.Count != 2}");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (secsItem.Format != SecsFormat.U1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: itemAccessMode.Format != SecsFormat.U1({secsItem.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			LoadPortAccessMode newMode = LoadPortAccessMode.Manual;
			if (secsItem.Value.ToString() == "1")
			{
				newMode = LoadPortAccessMode.Auto;
			}
			SecsItem secsItem2 = item.ItemList[1];
			if (secsItem2.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: itemPTNList.Format != SecsFormat.List({secsItem2.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			List<byte> list = null;
			if (secsItem2.ItemList.Count > 0)
			{
				list = new List<byte>();
				foreach (SecsItem item3 in secsItem2.ItemList)
				{
					if (item3.Format != SecsFormat.U1)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: itemPTN.Format != SecsFormat.U1({item3.Format})");
						return SecsErrorCode.ErrorIllegalData;
					}
					byte item2 = (byte)item3.Value;
					list.Add(item2);
				}
			}

			SecsItem secsItem3 = new SecsItem(SecsFormat.List);
			SecsItem itemErrList = new SecsItem(SecsFormat.List);
			CAACK v = CAACK.Acknowledge;
			if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
			{
				v = CAACK.Rejected;
                if (list == null)
                {
					ILoadPort[] loadPortArray = m_loadPortArray;
					foreach (ILoadPort loadPort in loadPortArray)
					{
						SecsItem secsItem4 = new SecsItem(SecsFormat.List);
						secsItem4.AppendItem(new SecsItem(loadPort.PortID));
						secsItem4.AppendItem(new SecsItem((uint)ERRCODE.CommandNotValidForCurrentState));
						secsItem4.AppendItem(new SecsItem($"This command cannot be executed in the local state"));
						itemErrList.AppendItem(secsItem4);
					}
				}
                else
                {
					foreach (var portId in list)
					{
						SecsItem secsItem4 = new SecsItem(SecsFormat.List);
						secsItem4.AppendItem(new SecsItem(portId));
						secsItem4.AppendItem(new SecsItem((uint)ERRCODE.CommandNotValidForCurrentState));
						secsItem4.AppendItem(new SecsItem($"This command cannot be executed in the local state"));
						itemErrList.AppendItem(secsItem4);
					}
				}
			}
            else
            {
				bool flag = ChangeLoadPortAccessMode(list, newMode, ref itemErrList);
				v = ((!flag) ? CAACK.CommandPerformedWithErrors : CAACK.Acknowledge);
			}
			secsItem3.AppendItem(new SecsItem((byte)v));
			secsItem3.AppendItem(itemErrList);
			m_eq.ReplyMessage(3, 28, secsItem3, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onCarrierTagReadRequest(SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,msgID ={msgID}");
			if (item == null || item.Format != 0 || item.ItemList.Count != 4)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error {item == null} || {item.Format != SecsFormat.List}||{item.ItemList.Count != 4}");
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = "";
			string text2 = "";
			string text3 = "";
			uint num = 0u;
			SecsItem secsItem = item.ItemList[0];
			text = secsItem.Value as string;
			SecsItem secsItem2 = item.ItemList[1];
			text2 = secsItem2.Value as string;
			SecsItem secsItem3 = item.ItemList[2];
			text3 = secsItem3.Value as string;
			SecsItem secsItem4 = item.ItemList[3];
			num = Convert.ToUInt32(secsItem4.Value);
			string data = "";
			List<KeyValuePair<ERRCODE, string>> errList = null;
			CAACK v = doReadCarrierTag(text, text2, text3, num, ref data, ref errList);
			SecsItem secsItem5 = new SecsItem(SecsFormat.List);
			SecsItem secsItem6 = new SecsItem(SecsFormat.List);
			secsItem5.AppendItem(new SecsItem(data));
			secsItem5.AppendItem(secsItem6);
			secsItem6.AppendItem(new SecsItem((byte)v));
			SecsItem secsItem7 = new SecsItem(SecsFormat.List);
			secsItem6.AppendItem(secsItem7);
			if (errList != null)
			{
				for (int i = 0; i < errList.Count; i++)
				{
					KeyValuePair<ERRCODE, string> keyValuePair = errList[i];
					SecsItem secsItem8 = new SecsItem(SecsFormat.List);
					secsItem8.AppendItem(new SecsItem((uint)keyValuePair.Key));
					secsItem8.AppendItem(new SecsItem(keyValuePair.Value));
					secsItem7.AppendItem(secsItem8);
				}
			}
			m_eq.ReplyMessage(3, 30, secsItem5, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onCarrierTagWriteRequest(SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,msgID ={msgID}");
			if (item == null || item.Format != 0 || item.ItemList.Count != 5)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error {item == null} || {item.Format != SecsFormat.List}||{item.ItemList.Count != 5}");
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = "";
			string text2 = "";
			string text3 = "";
			uint num = 0u;
			string text4 = "";
			SecsItem secsItem = item.ItemList[0];
			text = secsItem.Value as string;
			SecsItem secsItem2 = item.ItemList[1];
			text2 = secsItem2.Value as string;
			SecsItem secsItem3 = item.ItemList[2];
			text3 = secsItem3.Value as string;
			SecsItem secsItem4 = item.ItemList[3];
			num = (uint)secsItem4.Value;
			SecsItem secsItem5 = item.ItemList[4];
			text4 = secsItem5.Value as string;
			List<KeyValuePair<ERRCODE, string>> errList = null;
			CAACK v = doWriteCarrierTag(text, text2, text3, num, text4, ref errList);
			SecsItem secsItem6 = new SecsItem(SecsFormat.List);
			secsItem6.AppendItem(new SecsItem((byte)v));
			SecsItem secsItem7 = new SecsItem(SecsFormat.List);
			secsItem6.AppendItem(secsItem7);
			if (errList != null)
			{
				for (int i = 0; i < errList.Count; i++)
				{
					KeyValuePair<ERRCODE, string> keyValuePair = errList[i];
					SecsItem secsItem8 = new SecsItem(SecsFormat.List);
					secsItem8.AppendItem(new SecsItem((uint)keyValuePair.Key));
					secsItem8.AppendItem(new SecsItem(keyValuePair.Value));
					secsItem7.AppendItem(secsItem8);
				}
			}
			m_eq.ReplyMessage(3, 32, secsItem6, msgID);
			return SecsErrorCode.Success;
		}

		private SecsItem actionGetAttr(string attrName, List<KeyValuePair<string, SecsItem>> attrList)
		{
			KeyValuePair<string, SecsItem> keyValuePair = attrList.Find((KeyValuePair<string, SecsItem> x) => x.Key.Equals(attrName, StringComparison.OrdinalIgnoreCase));
			if (keyValuePair.Key == null)
			{
				return null;
			}
			return keyValuePair.Value;
		}

		private CAACK doCarrierRelease(string carrierId, byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,carrierId= {carrierId},ptn={ptn}");
			if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
			{
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.CommandNotValidForCurrentState, "This command cannot be executed in the local state"));
				return CAACK.Rejected;
			}
			if (attrList.Count > 0)
			{
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.ParametersImproperlySpecified, "Carrier attributes length is not zero"));
				return CAACK.InvalidDataOrArgument;
			}
			ICarrier carrier = FindCarrier(carrierId);
			if (carrier == null)
			{
				ILoadPort loadPort = GetLoadPort(ptn);
				if (loadPort == null)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error port == null,ptn={ptn}");
					return CAACK.InvalidDataOrArgument;
				}
				carrier = loadPort.CurrentCarrier;
			}
			if (carrier.AccessingStatus == CarrierAccessingStatus.InAccess)
			{
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.CommandNotValidForCurrentState, "carrier obj AccessingStatus is InAccess,can not cancel carrier"));
				return CAACK.CanNotPerformNow;
			}
			Gem300Equipment equipmentObject = Gem300Manager.EquipmentObject;
			return equipmentObject.OnCarrierTagReleased(carrier, null);
			
		}

		private CAACK doCancelCarrier(string carrierId, byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,carrierId= {carrierId},ptn={ptn}");
            if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
            {
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.CommandNotValidForCurrentState, "This command cannot be executed in the local state"));
				return CAACK.Rejected;
			}
			if (attrList.Count > 0)
			{
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.ParametersImproperlySpecified, "Carrier attributes length is not zero"));
				return CAACK.InvalidDataOrArgument;
			}
			Carrier carrier = FindCarrier(carrierId);
			if (carrier == null)
			{
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.UnknownObjectInstance, "Carrier obj is null"));
				return CAACK.InvalidDataOrArgument;
			}
			if (carrier.AccessingStatus == CarrierAccessingStatus.InAccess)
			{
				errList.Add(new KeyValuePair<ERRCODE, string>(ERRCODE.CommandNotValidForCurrentState, "carrier obj AccessingStatus is InAccess,can not cancel carrier"));
				return CAACK.CanNotPerformNow;
			}
			carrier.SwitchIDStatusToVerifyFailed();
			carrier.SwitchSlotMapStatusToVerifyFailed();
			Gem300Equipment equipmentObject = Gem300Manager.EquipmentObject;
			return  equipmentObject.OnCancelCarrier(carrier);
			//FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Gem300Manager.EquipmentObject.CarrierHoldControl={Gem300Manager.EquipmentObject.CarrierHoldControl}");
			//if (Gem300Manager.EquipmentObject.CarrierHoldControl == CarrierHoldControlMode.AutoRelease)
			//{
			//	Gem300Manager.EquipmentObject.OnCarrierTagReleased(carrier, null);
			//}
		}

		public Carrier FindCarrier(string carrierId)
		{
			foreach (ICarrier carrier in m_carrierList)
			{
				if (carrier.CarrierID.Equals(carrierId.Trim(), StringComparison.OrdinalIgnoreCase))
				{
					return carrier as Carrier;
				}
			}
			return null;
		}

		private CAACK doProceedWithCarrier(string carrierId, byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,carrierId= {carrierId},ptn={ptn}");
			CAACK ackCode = CAACK.Acknowledge;
			ERRCODE eRRCODE = ERRCODE.NoError;
			string errInfo = "";
            if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
            {
				ackCode = CAACK.Rejected;
				eRRCODE = ERRCODE.CommandNotValidForCurrentState;
				errInfo = "This command cannot be executed in the local state";
			}
            else
            {
				Carrier carrier = FindCarrier(carrierId);
				ILoadPort loadPort = GetLoadPort(ptn);
				if (carrier != null && loadPort != null && carrier.CurrentLoadPort.PortID != loadPort.PortID)
				{
					eRRCODE = ERRCODE.InvalidAttributeValue;
					errInfo = $"port id mismatch: {carrier.CurrentLoadPort.PortID}/{loadPort.PortID}";
					ackCode = CAACK.InvalidDataOrArgument;
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,errInfo= {errInfo}");
				}
				else if (carrier == null)
				{
					if (loadPort == null)
					{
						eRRCODE = ERRCODE.UnknownObjectInSpecifier;
						errInfo = carrierId + " not exist.";
					}
					else if (loadPort.CurrentCarrier == null)
					{
						eRRCODE = ERRCODE.UnknownObjectInSpecifier;
						errInfo = "loadport " + loadPort.PortName + " has no carrier now.";
					}
					else
					{
						eRRCODE = ERRCODE.UnknownObjectInSpecifier;
						errInfo = "loadport " + loadPort.PortName + " carrier id is " + loadPort.CurrentCarrier.CarrierID + " now.";
					}
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,errInfo= {errInfo}");
					ackCode = CAACK.InvalidDataOrArgument;
				}
				else
				{
					if (loadPort == null)
					{
						eRRCODE = ERRCODE.UnknownObjectInSpecifier;
						errInfo = "Loadport Not Exist";
						ackCode = CAACK.InvalidDataOrArgument;
					}
                    else
                    {
						SecsItem secsItem = actionGetAttr("Usage", attrList);
						if (secsItem != null)
						{
							string text2 = (carrier.Usage = secsItem.Value as string);
						}
						SecsItem secsItem2 = actionGetAttr("Capacity", attrList);
						if (secsItem2 != null)
						{
							int num = 0;
							num = (carrier.Capacity = Convert.ToInt32(secsItem2.Value));
						}
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:theCarrier.IDStatus = {carrier.IDStatus} ({secsItem2 != null})");
						if (carrier.IDStatus == CarrierIDStatus.WaitingForHost || carrier.IDStatus == CarrierIDStatus.IDNotRead)
						{
							proceedWithCarrier_Phase1(carrier, carrierId);
							goto IL_0301;
						}
						if ((carrier.SlotMapStatus == CarrierSlotMapStatus.WaitingForHost || carrier.SlotMapStatus == CarrierSlotMapStatus.NotRead) && carrier.IDStatus == CarrierIDStatus.VerificationOK)
						{
							eRRCODE = proceedWithCarrier_Phase2(carrier, carrierId, attrList, loadPort, ref ackCode, ref errInfo);
							if (eRRCODE == ERRCODE.NoError)
							{
								goto IL_0301;
							}
						}
						else
						{
							eRRCODE = ERRCODE.CommandNotValidForCurrentState;
							errInfo = "carrier[" + carrier.CarrierID + "] state: ID." + carrier.IDStatus.ToString() + " / SlotMap." + carrier.SlotMapStatus.ToString() + " ";
							ackCode = CAACK.CanNotPerformNow;
						}
					}
				}
				if (errList == null)
				{
					errList = new List<KeyValuePair<ERRCODE, string>>();
				}
			}
			errList.Add(new KeyValuePair<ERRCODE, string>(eRRCODE, errInfo));
			return ackCode;
			IL_0301:
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo}");
			return CAACK.Acknowledge;
		}

		private void proceedWithCarrier_Phase1(Carrier carrier, string carrierId)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: carrierId={carrierId}");
			m_impl.OnIdVerifyOK(carrier, carrierId);
			IProcessJob processJob = Gem300Manager.E40.FindProcessJobByCarrierID(carrierId);
			if (processJob == null)
			{
				Console.WriteLine("can't find process job associated with carrier [" + carrierId + "]");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: can't find process job associated with carrier [{carrierId}]");
			}
			else
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: EquipmentObject.OnProcessJobCarrierArrived(pj)");
				Gem300Manager.EquipmentObject.OnProcessJobCarrierArrived(processJob);
			}
			carrier.SwitchIDStatusToVerifyOK();
		}

		private ERRCODE proceedWithCarrier_Phase2(Carrier theCarrier, string carrierId, List<KeyValuePair<string, SecsItem>> attrList, ILoadPort loadport, ref CAACK ackCode, ref string errInfo)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: carrierId={carrierId},loadport={loadport}");
			SecsItem secsItem = actionGetAttr("SubstrateCount", attrList);
			if (secsItem != null)
			{
				if (!secsItem.IsUnsignedInterger())
				{
					errInfo = "SubstrateCount format error. (" + secsItem.Format.ToString() + ")";
					ackCode = CAACK.InvalidDataOrArgument;
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo}");
					return ERRCODE.InvalidAttributeValue;
				}
				uint num = Convert.ToUInt32(secsItem.Value);
			}
			SecsItem secsItem2 = actionGetAttr("SlotMap", attrList);
			if (secsItem2 == null || secsItem2.Format != 0)
			{
				errInfo = "SlotMap missing or format error. ";
				ackCode = CAACK.InvalidDataOrArgument;
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo}");
				return ERRCODE.InvalidAttributeValue;
			}
			SlotState[] slotArray = new SlotState[secsItem2.ItemList.Count];
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: slotArray.Length={slotArray.Length}");
			for (int i = 0; i < secsItem2.ItemList.Count; i++)
			{
				SecsItem secsItem3 = secsItem2.ItemList[i];
				if (secsItem3.Format != SecsFormat.U1)
				{
					errInfo = "SlotMap slot status format(U1) error. (" + secsItem3.Format.ToString() + ")";
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo}");
					ackCode = CAACK.InvalidDataOrArgument;
					return ERRCODE.InvalidAttributeValue;
				}
				SlotState slotState = (SlotState)secsItem3.Value;
				slotArray[i] = slotState;
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: slotArray[{i}] = ss");
			}
			if (!theCarrier.CompareSlotMapResult(slotArray))
			{
				errInfo = "SlotMap result mismatch. ";
				ackCode = CAACK.InvalidDataOrArgument;
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo}");
				return ERRCODE.InvalidAttributeValue;
			}
			theCarrier.SlotMapArray = slotArray;
			theCarrier.CreateSubstLocation(slotArray.Length);
			SecsItem secsItem4 = actionGetAttr("ContentMap", attrList);
			string _contentMapCount = secsItem4 == null ? "0" : secsItem4.ItemList.Count.ToString();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: itemContentMap.Length={_contentMapCount}");
			if (secsItem4 == null || secsItem4.Format != 0)
			{
				secsItem4 = new SecsItem(SecsFormat.List);
				for (int i =0;i< slotArray.Length; i++)
                {
					SecsItem _content = new SecsItem(SecsFormat.List);
					_content.AppendItem(new SecsItem(carrierId));
					_content.AppendItem(new SecsItem(carrierId+"."+ i.ToString()));
					secsItem4.AppendItem(_content);

				}
				//errInfo = "ContentMap missing or format error. ";
				//ackCode = CAACK.InvalidDataOrArgument;
				//return ERRCODE.InvalidAttributeValue;
			}
			CarrierSlotInfo[] array = new CarrierSlotInfo[secsItem4.ItemList.Count];
			List<ISubstrate> substList = new List<ISubstrate>();
			for (int j = 0; j < secsItem4.ItemList.Count; j++)
			{
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: i={j}");
				SecsItem secsItem5 = secsItem4.ItemList[j];
				if (secsItem5 == null || secsItem5.Format != 0 || secsItem5.ItemList.Count != 2)
				{
					errInfo = $"ContentMap[{j}] missing or format error. ";
					ackCode = CAACK.InvalidDataOrArgument;
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo} {secsItem5 == null} || {secsItem5.Format != SecsFormat.List} || {secsItem5.ItemList.Count != 2}");
					return ERRCODE.InvalidAttributeValue;
				}
				SecsItem secsItem6 = secsItem5.ItemList[0];
				SecsItem secsItem7 = secsItem5.ItemList[1];
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:itemLotID={secsItem6},itemSubstrateID={secsItem7}");
				if (secsItem6 == null || secsItem6.Format != SecsFormat.Ascii || secsItem7 == null || secsItem7.Format != SecsFormat.Ascii)
				{
					errInfo = $"ContentMap[{j}] lotid or subst id missing or format error. ";
					ackCode = CAACK.InvalidDataOrArgument;
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errInfo} {secsItem6 == null} || {secsItem6.Format != SecsFormat.Ascii} ||  {secsItem7 == null} || {secsItem7.Format != SecsFormat.Ascii}");
					return ERRCODE.InvalidAttributeValue;
				}
				string lotID = secsItem6.Value as string;
				string text = secsItem7.Value as string;
				CarrierSlotInfo obj = new CarrierSlotInfo
				{
					LotID = lotID,
					SubstrateID = text
				};
				CarrierSlotInfo carrierSlotInfo = (array[j] = obj);
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cntArray[i]={carrierSlotInfo.SubstrateID}");
				if (slotArray[j] == SlotState.CorrectlyOccupied && text.Trim().Length > 0)
				{
					Substrate substrate = new Substrate(text)
					{
						CurrentCarrier = loadport.CurrentCarrier,
						LotID = lotID,
						CurrentSlot = j.ToString(),
						SourceSlot = j,
						SubstSource = loadport.CurrentCarrier.CarrierID + "." + (j + 1),
						SubstLocID = loadport.CurrentCarrier.CarrierID + "." + (j + 1),
						SubstType = SubstrateType.Wafer,
						Usage = SubstUsage.Product
					};
					substList.Add(substrate);
					FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: itemLotID={secsItem6},substOb.SourceSlot={substrate.SourceSlot}");
					IProcessJob processJob = Gem300Manager.E40.AssociateSubstateToProcessJob(carrierId, j, substrate);
					if (processJob == null)
					{
						Console.WriteLine("can't find process job of substrate[" + substrate.ObjID + "] ");
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:can't find process job of substrate[{substrate.ObjID}]");
					}
				}
			}
			theCarrier.ContentMapArray = array;
			theCarrier.SubstList = substList;
			ISubstrateLocation[] substLocArray = null;
			bool slotmapResult = theCarrier.CompareSlotMapResult(slotArray);
			Task.Factory.StartNew(delegate
			{
				MappingEventPost(slotmapResult, theCarrier, slotArray, substList, substLocArray);
			});
			return ERRCODE.NoError;
		}

		private void MappingEventPost(bool slotmapResult, Carrier theCarrier, SlotState[] slotArray, List<ISubstrate> substList, ISubstrateLocation[] substLocArray)
		{
			if (slotmapResult)
			{
				m_impl.OnSlotMapVerifyOK(theCarrier, ref slotArray, (theCarrier.Capacity == 0) ? slotArray.Length : theCarrier.Capacity, ref substList, ref substLocArray);
				theCarrier.SwitchSlotMapStatusToVerifyOK();
				{
					foreach (ISubstrate subst in theCarrier.SubstList)
					{
						subst.Start();
					}
					return;
				}
			}
			m_impl.OnSlotMapVerifyFailed(theCarrier);
		}

		private CAACK doBindCarrier(string carrierId, byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: This feature is not implemented");
			throw new NotImplementedException();
		}

		private CAACK doChangeServiceStatus(byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: This feature is not implemented");
			throw new NotImplementedException();
		}

		private CAACK doReserveAtPort(byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			if (ptn > m_loadPortArray.Length)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ptn（{ptn}） > m_loadPortArray.Length（{m_loadPortArray.Length}）");
				return CAACK.InvalidDataOrArgument;
			}
			LoadPort loadPort = m_loadPortArray[ptn - 1] as LoadPort;
			loadPort.SwitchReservationMode(newMode: true);
			return CAACK.Acknowledge;
		}

		private CAACK doCancelReservationAtPort(byte ptn, List<KeyValuePair<string, SecsItem>> attrList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			if (ptn > m_loadPortArray.Length)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ptn（{ptn}） > m_loadPortArray.Length（{m_loadPortArray.Length}）");
				return CAACK.InvalidDataOrArgument;
			}
			LoadPort loadPort = m_loadPortArray[ptn - 1] as LoadPort;
			loadPort.SwitchReservationMode(newMode: false);
			return CAACK.Acknowledge;
		}

		private CAACK doReadCarrierTag(string locID, string carrierID, string dataSeg, uint dataLength, ref string data, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: locID={locID},carrierID={carrierID},dataSeg={dataSeg}");
			Carrier carrier = FindCarrier(carrierID);
            if (carrier == null)
            {
				return CAACK.CanNotPerformNow;
            }
			int num = Convert.ToByte(locID[locID.Length - 1]);
			int num2 = num * 2 + 1;
			if (locID.Length > 4 && locID.Substring(0, 4).Equals("FIMS"))
			{
				num2++;
			}
			int num3 = m_impl.OnCarrierTagRead(carrier, num2, dataSeg, ref data);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: result（{num3}）");
			if (num3 == 0)
			{
				return CAACK.Acknowledge;
			}
			return CAACK.Rejected;
		}

		private CAACK doWriteCarrierTag(string locID, string carrierID, string dataSeg, uint dataLength, string data, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: locID={locID},carrierID={carrierID},dataSeg={dataSeg}");
			Carrier carrier = FindCarrier(carrierID);
			int num = Convert.ToByte(locID[locID.Length - 1]);
			int num2 = num * 2 + 1;
			if (locID.Length > 4 && locID.Substring(0, 4).Equals("FIMS"))
			{
				num2++;
			}
			int num3 = m_impl.OnCarrierTagWrite(carrier, num2, carrierID, dataSeg, data);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: result（{num3}）");
			if (num3 == 0)
			{
				return CAACK.Acknowledge;
			}
			return CAACK.Rejected;
		}
	}
}
