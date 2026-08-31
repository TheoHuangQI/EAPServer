#define DEBUG
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;
using FALogger;
using FSMLite;
using SecsLite.Model;
using SecsLite.Utils;

namespace SecsLite.Gem
{
	public class GemEquipment : HsmsSession
	{
		public virtual string ClassName => GetType().Name;

		private Dictionary<string, ECItem> m_namedECDict = new Dictionary<string, ECItem>();

		private uint m_msgId_S1F13 = 0u;
		private uint m_msgId_S1F1 = 0u;
		internal uint m_msgId_RevS1F13 = 0u;

		private Dictionary<string, DataTraceInfo> m_traceDict = new Dictionary<string, DataTraceInfo>();

		private string m_persistFilePath = "";

		private Collection<uint> m_persistEventSet = new Collection<uint>();

		private Dictionary<uint, bool> m_persistAlarmStateDict = new Dictionary<uint, bool>();

		private bool? m_persistAlarmAllEnabled = null;

		protected Dictionary<uint, DVItem> DVItemDict = new Dictionary<uint, DVItem>();

		protected Dictionary<uint, SVItem> SVItemDict = new Dictionary<uint, SVItem>();

		protected Dictionary<uint, ECItem> ECItemDict = new Dictionary<uint, ECItem>();

		protected Dictionary<uint, AlarmItem> AlarmItemDict = new Dictionary<uint, AlarmItem>();

		protected Dictionary<uint, EventItem> EventDict = new Dictionary<uint, EventItem>();

		protected Dictionary<string, EventItem> NamedEventDict = new Dictionary<string, EventItem>();

		protected Dictionary<string, AlarmItem> NamedAlarmDict = new Dictionary<string, AlarmItem>();

		protected DataReportDictionary DataRptDict = new DataReportDictionary();

		private readonly byte ALCD_MASK = 128;

		private HsmsConfig m_cfg = null;

		private FSMCommunicationState m_fsmCommState = null;
		private FSMNotCommunicating_HostState m_fsmCommStateHost = null;
		private FSMNotCommunicating_EquipState m_fsmCommStateEquip = null;

		private FSMControlState m_fsmCtrlState = null;

		public string MDLN { get; set; }

		public string SoftRev { get; set; }

		public HsmsConfig HsmsCfg => m_cfg;

		public uint SamplePeriodLimitMin { get; set; }

		public bool EnableLinkedEventByDefault { get; set; }

		public bool EquipmentInitiateConnect { get; set; }

		public FSMCommunicationState.State CommunicationState => (m_fsmCommState != null) ? ((FSMCommunicationState.State)m_fsmCommState.CurrentState) : FSMCommunicationState.State.Disabled;

		public FSMNotCommunicating_HostState.State CommunicationStateHost => (m_fsmCommStateHost != null) ? ((FSMNotCommunicating_HostState.State)m_fsmCommStateHost.CurrentState) : FSMNotCommunicating_HostState.State.WaitCRFromHost;

		public FSMNotCommunicating_EquipState.State CommunicationStateEquip => (m_fsmCommStateEquip != null) ? ((FSMNotCommunicating_EquipState.State)m_fsmCommStateEquip.CurrentState) : FSMNotCommunicating_EquipState.State.WaitCRA;


		public FSMControlState.State ControlState => (m_fsmCtrlState == null) ? FSMControlState.State.EqOffline : ((FSMControlState.State)m_fsmCtrlState.CurrentState);

		public FSMControlState.State PreviousControlState => (m_fsmCtrlState == null) ? FSMControlState.State.EqOffline : ((FSMControlState.State)m_fsmCtrlState.PreviousState);

		public bool AlarmStatePersistEnabled { get; set; }

		public bool EventLinkReportPersistEnabled { get; set; }

		public int EstablishCommunicationsTimeout { get; set; }

		public TimeFormat TimeStringFormat { get; set; }

		internal FSMCommunicationState CommStateFSM => m_fsmCommState;
		internal FSMNotCommunicating_HostState CommStateHostFSM => m_fsmCommStateHost;
		internal FSMNotCommunicating_EquipState CommStateEquipFSM => m_fsmCommStateEquip;
		internal FSMControlState CtrlStateFSM => m_fsmCtrlState;


		public GemEquipment()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			MDLN = "";
			SoftRev = "";
			SamplePeriodLimitMin = 1000u;
			TimeStringFormat = TimeFormat.LongFormat;
			EquipmentInitiateConnect = false;
			EnableLinkedEventByDefault = false;
			AlarmStatePersistEnabled = false;
			EventLinkReportPersistEnabled = false;
			EstablishCommunicationsTimeout = 10;
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
		}


		public GemResult StartGEM(HsmsConfig cfg)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			m_cfg = cfg;
			m_persistFilePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
			m_persistFilePath += "\\data\\";
			Directory.CreateDirectory(m_persistFilePath);

			m_fsmCommState = new FSMCommunicationState(this, m_cfg.DefaultCommunicationState);
			m_fsmCommState.CurrentState = FSMCommunicationState.State.Disabled;

			m_fsmCommStateHost = new FSMNotCommunicating_HostState(this);
			m_fsmCommStateHost.CurrentState = FSMNotCommunicating_HostState.State.WaitCRFromHost;

			m_fsmCommStateEquip = new FSMNotCommunicating_EquipState(this);
			m_fsmCommStateEquip.CurrentState = FSMNotCommunicating_EquipState.State.WaitCRA;

			FSMControlState.State DefaulControlState = FSMControlState.State.EqOffline;
			if(m_cfg.DefaultControlState== ControlStateEnum.Online)
				//即便是默认状态为Online，也要先切换到AttemptOnline发送S1F1 获取 Online权限之后才可切换
				m_fsmCtrlState = new FSMControlState(this, FSMControlState.State.AttemptOnline , m_cfg.DefaultOnlineControlState);
			else
				m_fsmCtrlState = new FSMControlState(this, m_cfg.DefaultOfflineControlState, m_cfg.DefaultOnlineControlState);
			m_fsmCtrlState.CurrentState = FSMControlState.State.Undefined;

			if (AlarmStatePersistEnabled)
			{
				try
				{
					if (!loadPersistAlarmState())
					{
						return GemResult.ConfigFileError;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start Gem ALARM: {ex.Message +"\r\n"+ ex.StackTrace}");
					return GemResult.ConfigFileError;
				}
			}
			if (EventLinkReportPersistEnabled)
			{
				try
				{
					if (!loadPersistEventReportSetting())
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Event Link Report False");
						return GemResult.ConfigFileError;
					}
				}
				catch (Exception ex2)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Event Link Report Exception:{ex2.Message +"\r\n"+ ex2.StackTrace}");
					Console.WriteLine(ex2.Message);
					return GemResult.ConfigFileError;
				}
			}
			
			m_fsmCommState.Start();
			
			m_fsmCtrlState.Start();
			m_fsmCommStateEquip.Start();
			m_fsmCommStateHost.Start();
			m_fsmCommState.TriggerEvent(FSMCommunicationState.Event.CommuInitialization);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult StopGEM()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			if (m_fsmCommState != null)
			{
				m_fsmCommState.TriggerEvent(FSMCommunicationState.Event.DisableCommunication);
				Thread.Sleep(100);
				m_fsmCommState.Stop();
				m_fsmCommState = null;
			}
            if (m_fsmCtrlState != null)
            {
				m_fsmCtrlState.TriggerEvent(FSMControlState.Event.ControlUndfined);
				Thread.Sleep(100);
				m_fsmCtrlState.Stop();
				m_fsmCtrlState = null;
			}

			if(m_fsmCommStateEquip != null)
            {
				m_fsmCommStateEquip.Stop();
				m_fsmCommStateEquip = null;
			}

			if (m_fsmCommStateHost != null)
			{
				m_fsmCommStateHost.Stop();
				m_fsmCommStateHost = null;
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadSV(List<SVItem> svList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			foreach (SVItem sv in svList)
			{
				SVItemDict[sv.SVID] = sv;
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadDV(List<DVItem> dvList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			foreach (DVItem dv in dvList)
			{
				DVItemDict[dv.VID] = dv;
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadEC(List<ECItem> ecList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			foreach (ECItem ec in ecList)
			{
				ECItemDict[ec.ECID] = ec;
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadEC(string ecConfigFileName = "")
		{

			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			string text = ecConfigFileName;
			if (text == "")
			{
				//text = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
				//text = Directory.GetParent(text).FullName;
				//text += "\\conf\\ecs_ec_def.csv";
				text= @"Z:Bin\Module_Gem\conf\ecs_ec_def.csv";
			}
			if (!File.Exists(text))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: File Not Exists. fullPathName={text}");
				return GemResult.FileNotFound;
			}
			using (StreamReader streamReader = File.OpenText(text))
			{
				string empty = string.Empty;
				streamReader.ReadLine();
				while ((empty = streamReader.ReadLine()) != null)
				{
					empty = empty.Trim();
					if (empty.Length != 0 && empty[0] != '#')
					{
						string[] array = empty.Split(',');
						if (array.Length < 8)
						{
							return GemResult.FormatError;
						}
						uint num = Convert.ToUInt32(array[0]);
						string text2 = array[1].Trim();
						string eCName = array[1].Trim();
						SecsFormat secsFormat = SecsUtil.StringToFormat(array[2].Trim());
						if (secsFormat == SecsFormat.Unknown)
						{
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Unknown SecsFormat: {array[2].Trim()}.");
							return GemResult.ConfigFileError;
						}
						string units = array[5].Trim();
						string minLimit = array[3].Trim();
						string maxLimit = array[4].Trim();
						string defaultValue = array[6].Trim();
						string desc = array[7].Trim();
						ECItem value = new ECItem
						{
							ECID = num,
							ECName = eCName,
							Format = secsFormat,
							Units = units,
							MaxLimit = maxLimit,
							MinLimit = minLimit,
							DefaultValue = defaultValue,
							Desc = desc
						};
						if (!ECItemDict.ContainsKey(num))
						{
							ECItemDict.Add(num, value);
						}
						else
						{
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ECid is already Exists(ECID={num}).");
						}
						if (text2.Length > 0 && !m_namedECDict.ContainsKey(text2))
						{
							m_namedECDict.Add(text2, value);
						}
						else
						{
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ECKEY is already Exists OR ECKEY IS NULL(ECKEY={text2}");
						}
					}
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadEvent(string eventConfigFileName = "")
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			string text = eventConfigFileName;
			if (text == "")
			{
				//text = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
				//text = Directory.GetParent(text).FullName;
				//text += "\\conf\\event_def_300.csv";
				text= @"Z:Bin\Module_Gem\conf\event_def_300.csv";
			}
			if (!File.Exists(text))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: File Not Exists. fullPathName={text}");
				return GemResult.FileNotFound;
			}
			using (StreamReader streamReader = File.OpenText(text))
			{
				string empty = string.Empty;
				streamReader.ReadLine();
				while ((empty = streamReader.ReadLine()) != null)
				{
					empty = empty.Trim();
					if (empty.Length != 0 && empty[0] != '#')
					{
						string[] array = empty.Split(',');
						if (array.Length < 4)
						{
							return GemResult.FormatError;
						}
						uint ceid = Convert.ToUInt32(array[0]);
						string name = array[1].Trim();
						bool enabled = !array[2].Trim().Equals("0");
						string desc = array[3].Trim();
						defineEvent(ceid, name, enabled, desc);
					}
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}
		private bool defineEvent(uint ceid, string name, bool enabled, string desc)
		{
			EventItem value = new EventItem(ceid, name, enabled, desc);
			EventDict[ceid] = value;
			if (name != "")
			{
				NamedEventDict[name] = value;
			}
			return true;
		}

		public uint GetEventIDByName(string evName)
		{
			if (NamedEventDict.ContainsKey(evName))
			{
				return NamedEventDict[evName].CEID;
			}
			return GemEvent.InvalidEventID;
		}

		public ECItem GetNamedECItem(string ecKeyName)
		{
			if (m_namedECDict.ContainsKey(ecKeyName))
			{
				return m_namedECDict[ecKeyName];
			}
			return null;
		}

		public GemResult LoadEvent(List<EventItem> evItemList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			foreach (EventItem evItem in evItemList)
			{
				EventDict[evItem.CEID] = evItem;
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadAlarm(List<AlarmItem> alarmList)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			foreach (AlarmItem alarm in alarmList)
			{
				AlarmItemDict[alarm.ALID] = alarm;
				if (!string.IsNullOrEmpty(alarm.Name) && alarm.Name.Trim() != "")
				{
					string key = alarm.Name.Trim();
					if (NamedAlarmDict.ContainsKey(key))
					{
						return GemResult.NameDuplicated;
					}
					NamedAlarmDict[alarm.Name.Trim()] = alarm;
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadAlarm(string alarmCfgFileName)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			if (!File.Exists(alarmCfgFileName))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: File Not Exists. fullPathName={alarmCfgFileName}");
				return GemResult.FileNotFound;
			}
			using (StreamReader streamReader = File.OpenText(alarmCfgFileName))
			{
				string empty = string.Empty;
				streamReader.ReadLine();
				while ((empty = streamReader.ReadLine()) != null)
				{
					empty = empty.Trim();
					if (empty.Length != 0 && empty[0] != '#')
					{
						string[] array = empty.Split(',');
						if (array.Length != 7)
						{
							return GemResult.FormatError;
						}
						uint num = Convert.ToUInt32(array[0]);
						byte aLCD = Convert.ToByte(array[1]);
						string text = array[2].Trim();
						string text2 = array[3].Trim();
						uint cEIDSet = Convert.ToUInt32(array[4]);
						uint cEIDClear = Convert.ToUInt32(array[5]);
						string text3 = array[6].Trim();
						AlarmItem value = new AlarmItem
						{
							ALID = num,
							ALCD = aLCD,
							Name = text,
							Text = text2,
							CEIDSet = cEIDSet,
							CEIDClear = cEIDClear,
							Enabled = true
						};
						AlarmItemDict[num] = value;
						NamedAlarmDict[text] = value;
					}
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult LoadEventAlarmMap()
		{
			return GemResult.Success;
		}

		public GemResult SwitchOnline(FSMControlState.State state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			m_fsmCtrlState.DefaultOnlineState = state; 
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.OperatorSwitchToOnline);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult SwitchOffline()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.OperatorSwitchToOffline);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult SwitchLocal()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.OperatorSwitchToLocal);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult SwitchRemote()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.OperatorSwitchToRemote);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}
		public GemResult SwitchDefaultControState(HsmsConfig hsmsConfig)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			m_fsmCommState.DefaultState = hsmsConfig.DefaultCommunicationState;
			m_fsmCtrlState.DefaultOnlineState = hsmsConfig.DefaultOnlineControlState;
			if (hsmsConfig.DefaultControlState == ControlStateEnum.Offline)
            {
				m_fsmCtrlState.DefaultState = hsmsConfig.DefaultOfflineControlState;
			}
            else
            {
				m_fsmCtrlState.DefaultState =FSMControlState.State.AttemptOnline;
			}
			m_fsmCommState.UndefineTransitionTable();
			m_fsmCommState.defineTransitionTable();
			m_fsmCtrlState.UndefineTransitionTable();
			m_fsmCtrlState.defineTransitionTable();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult PostEvent(uint ceid, DataVariableSet dvSet = null)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			if (!EventDict.ContainsKey(ceid))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: UnDefinedEventID：{ceid}");
				return GemResult.UndefinedEventID;
			}
			EventItem eventItem = EventDict[ceid];
			if (!eventItem.Enabled)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {ceid} Not Enabled.");
				return GemResult.Success;
			}
			SecsItem secsItem = makeEventReportItem(eventItem, dvSet);
			if (secsItem == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Read Equipment Val Error.");
				return GemResult.ReadEquipmentValError;
			}
			if ((m_fsmCtrlState.ControlState != FSMControlState.State.OnlineLocal && m_fsmCtrlState.ControlState != FSMControlState.State.OnlineRemote) && eventItem.CEID != 8)
			{
				return GemResult.HsmsFailed;
			}
			SendMessage(6, 11, secsItem, needReply: true);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

		public GemResult PostNamedEvent(string evName, DataVariableSet dvSet = null, object dvProvider = null)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			if (!NamedEventDict.ContainsKey(evName))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: UnDefinedEventName：{evName}");
				return GemResult.UndefinedEventID;
			}
			EventItem eventItem = NamedEventDict[evName];
			if (!eventItem.Enabled)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {eventItem.CEID} Not Enabled.");
				return GemResult.Success;
			}
			SecsItem secsItem = makeEventReportItem(eventItem, dvSet, dvProvider);
			if (secsItem == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Read Equipment Val Error.");
				return GemResult.ReadEquipmentValError;
			}
			if ((m_fsmCtrlState.ControlState != FSMControlState.State.OnlineLocal && m_fsmCtrlState.ControlState != FSMControlState.State.OnlineRemote) && eventItem.CEID != 8)
			{
				return GemResult.HsmsFailed;
			}
			SendMessage(6, 11, secsItem, needReply: true);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}
		public GemResult PostAlarm(uint alid, string text = "")
		{
			return sendS5F1(alid, ALCD.Set, text);
		}

		public GemResult PostAlarmClear(uint alid, string text = "")
		{
			return sendS5F1(alid, ALCD.Cleared, text);
		}

		public GemResult PostNamedAlarm(string alarmName, ALCD state, string text = "")
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start.");
			if (!NamedAlarmDict.ContainsKey(alarmName))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: NamedAlarmDict Not Contain Key:{alarmName}.");
				return GemResult.UndefinedAlarmName;
			}
			AlarmItem alarmItem = NamedAlarmDict[alarmName];
			uint aLID = alarmItem.ALID;
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return sendS5F1(aLID, state, text);
		}

		public GemResult ClearAlarm(uint alid, string text = "")
		{
			return sendS5F1(alid, ALCD.Cleared, text);
		}

		public GemResult TerminalSendToHost(string text, byte tid = 0)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,Message:{text}");
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(SecsItem.CreateBinaryItem(tid));
			secsItem.AppendItem(new SecsItem(text));
			SendMessage(10, 1, secsItem, needReply: true);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End.");
			return GemResult.Success;
		}

        #region  开放给适配层可重写和调用的接口
        public virtual void OnCommunicationStateChange(int newstage)
		{
		}
		public virtual void OnControlStateChange(int newstage)
		{
		}
		public virtual GemResult OnCommDisabled()
		{
			return GemResult.NotImplemented;
		}

		public virtual ONLACK OnHostRequestOnline()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start,ControlState:{m_fsmCtrlState.ControlState}");
			switch (m_fsmCtrlState.ControlState)
			{
				case FSMControlState.State.EqOffline:
					return ONLACK.NotAllowed;
				case FSMControlState.State.AttemptOnline:
				case FSMControlState.State.HostOffline:
					return ONLACK.Accepted;
				case FSMControlState.State.OnlineLocal:
				case FSMControlState.State.OnlineRemote:
					return ONLACK.OnlineAlready;
				default:
					return ONLACK.NotAllowed;
			}
		}

		public virtual GemResult OnGetDV(uint vid, ref SecsItem item)
		{
			return GemResult.NotImplemented;
		}

		public virtual GemResult OnGetSV(uint svid, ref SecsItem item)
		{
			return GemResult.NotImplemented;
		}

		public virtual GemResult OnGetEC(uint ecid, ref SecsItem item)
		{
			return GemResult.NotImplemented;
		}

		public virtual EAC OnSetEC(uint ecid, SecsItem item)
		{
			return EAC.DeniedECIDNotExist;
		}

		public virtual bool LoadInternalEC()
		{
			return true;
		}

		public virtual ACKC7 OnPPDelete(string ppid)
		{
			return ACKC7.PermissionNotGranted;
		}

		public virtual ACKC7 OnPPDeleteAll()
		{
			return ACKC7.PermissionNotGranted;
		}

		public virtual SecsItem OnProcessRequire(string pPID, string pPIDLength)
		{
			return null;
		}

		public virtual SecsItem OnProcessRecive(SecsItem secsItem)
		{
			return null;
		}

		public virtual SecsItem OnPPGetFormatted(string ppid)
		{
			return null;
		}

		public virtual SecsItem OnPPGetUnformatted(string ppid)
		{
			return null;
		}

		public virtual SecsItem OnPPDownload(SecsItem item)
		{
			return null;
		}

		public virtual GemResult OnPPGetList(ref List<string> ppList)
		{
			return GemResult.NotImplemented;
		}

		public virtual HCACK OnHostCommand(string cmd, SecsItem param, ref SecsItem resultParam)
		{
			return HCACK.CommandNotExist;
		}

		public virtual HCACK OnHostEnhancedCommand(string cmd, string objspec, SecsItem param, ref SecsItem resultParam)
		{
			return HCACK.CommandNotExist;
		}

		public virtual TIACK OnSetClock(string timeStr)
		{
			if (timeStr.Length != 16)
			{
				return TIACK.Error;
			}
			SYSTEMTIME st = default(SYSTEMTIME);
			st.wYear = Convert.ToInt16(timeStr.Substring(0, 4));
			st.wMonth = Convert.ToInt16(timeStr.Substring(4, 2));
			st.wDay = Convert.ToInt16(timeStr.Substring(6, 2));
			st.wHour = Convert.ToInt16(timeStr.Substring(8, 2));
			st.wMinute = Convert.ToInt16(timeStr.Substring(10, 2));
			st.wSecond = Convert.ToInt16(timeStr.Substring(12, 2));
			st.wMilliseconds = (short)(Convert.ToInt16(timeStr.Substring(14, 2)) * 10);
			return (!Win32API.SetLocalTime(ref st)) ? TIACK.Error : TIACK.OK;
		}

		public virtual ACKC10 OnTerminalMessage(string text)
		{
			return ACKC10.MsgNotDisplayed;
		}

		public virtual ACKC10 OnTerminalMessage(int terminalId, string text)
		{
			return ACKC10.MsgNotDisplayed;
		}

		public virtual ACKC5 OnEnableAlarm(uint? alid, bool aled)
		{
			if (!alid.HasValue)
			{
				foreach (AlarmItem value2 in AlarmItemDict.Values)
				{
					value2.Enabled = aled;
				}
			}
			else
			{
				uint value = alid.Value;
				if (!AlarmItemDict.ContainsKey(value))
				{
					return ACKC5.Denied;
				}
				AlarmItemDict[value].Enabled = aled;
			}
			return ACKC5.Accepted;
		}

		public virtual SecsItem OnGetDVIDList(SecsItem item)
		{
			SecsItem dvsItem = new SecsItem(SecsFormat.List);
			if (item.ItemList.Count == 0) // request all SV name
			{
				foreach (DVItem dv in DVItemDict.Values)
				{
					SecsItem dvInfoItem = new SecsItem(SecsFormat.List);
					dvInfoItem.AppendItem(new SecsItem(dv.VID));
					dvInfoItem.AppendItem(new SecsItem(dv.DVName));
					dvInfoItem.AppendItem(new SecsItem(dv.Units));
					dvsItem.AppendItem(dvInfoItem);
				}
			}
			else
			{
				for (int i = 0; i < item.ItemList.Count; i++)
				{
					uint dvid = Convert.ToUInt32(item.ItemList[i].Value);
					if (!DVItemDict.ContainsKey(dvid))
					{
						// empty list means the SV does not exist.
						SecsItem dvInfoItem = new SecsItem(SecsFormat.List);
						dvInfoItem.AppendItem(new SecsItem(dvid));
						dvInfoItem.AppendItem(new SecsItem(""));
						dvInfoItem.AppendItem(new SecsItem(""));
						dvsItem.AppendItem(dvInfoItem);
						continue;
					}
					DVItem dv = DVItemDict[dvid];
					SecsItem dvInfoItem1 = new SecsItem(SecsFormat.List);
					dvInfoItem1.AppendItem(new SecsItem(dv.VID));
					dvInfoItem1.AppendItem(new SecsItem(dv.DVName));
					dvInfoItem1.AppendItem(new SecsItem(dv.Units));
					dvsItem.AppendItem(dvInfoItem1);
				}
			}
			return dvsItem;
		}

		public virtual SecsItem OnGetCEIDList(SecsItem item)
		{
			SecsItem evsItem = new SecsItem(SecsFormat.List);
			if (item.ItemList.Count == 0) // request all EVENT name
			{
				foreach (EventItem ev in EventDict.Values)
				{
					SecsItem evInfoItem = new SecsItem(SecsFormat.List);
					evInfoItem.AppendItem(new SecsItem(ev.CEID));
					evInfoItem.AppendItem(new SecsItem(ev.Name));
					evInfoItem.AppendItem(new SecsItem(SecsFormat.List));
					foreach(var report in ev.LinkedReportList)
                    {
                        if (DataRptDict.ContainsKey(report))
                        {
							foreach (var vid in DataRptDict[report])
                            {
								evInfoItem.ItemList[2].AppendItem(new SecsItem(vid));
							}
						}
                    }
					evsItem.AppendItem(evInfoItem);
				}
			}
			else
			{
				for (int i = 0; i < item.ItemList.Count; i++)
				{
					uint ceid = Convert.ToUInt32(item.ItemList[i].Value);
					if (!EventDict.ContainsKey(ceid))
					{
						evsItem.AppendItem(new SecsItem(SecsFormat.List));
						evsItem.AppendItem(new SecsItem(ceid));
						evsItem.AppendItem(new SecsItem(SecsFormat.Ascii));
						evsItem.AppendItem(new SecsItem(SecsFormat.List));
						continue;
					}
					EventItem ev = EventDict[ceid];
					SecsItem evInfoItem = new SecsItem(SecsFormat.List);
					evInfoItem.AppendItem(new SecsItem(ev.CEID));
					evInfoItem.AppendItem(new SecsItem(ev.Name));
					evInfoItem.AppendItem(new SecsItem(SecsFormat.List));
					foreach (var report in ev.LinkedReportList)
					{
						if (DataRptDict.ContainsKey(report))
						{
							foreach (var vid in DataRptDict[report])
							{
								evInfoItem.ItemList[2].AppendItem(new SecsItem(vid));
							}
						}
					}
					evsItem.AppendItem(evInfoItem);
				}
			}
			return evsItem;
		}
		public override bool OnConnect(HsmsSessionInfo info)
		{
			return base.OnConnect(info);
		}

		public override void OnDisconnect()
		{
			m_fsmCommState.TriggerEvent(FSMCommunicationState.Event.CommunicationFailure);
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.ControlUndfined);
			base.OnDisconnect();
		}
        #endregion

        public override SecsErrorCode OnReceiveMessage(byte stream, byte func, SecsItem item, uint messageId, bool IsEapSend = true)
		{
			SecsErrorCode secsErrorCode = base.OnReceiveMessage(stream, func, item, messageId, IsEapSend);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Recive Message From Host. S{stream}F{func},messageId={messageId}");
			if (ControlState != FSMControlState.State.OnlineLocal && ControlState != FSMControlState.State.OnlineRemote && IsEapSend)
			{
				if (stream == 6 && func == 12)
				{
					return SecsErrorCode.Success;
				}
				if (stream != 1 && func!=0)
				{
					return replySxF0(stream, messageId);
				}
				if (func != 0 && func != 3 && func != 2 && func != 13 && func != 14 && func != 17 && func != 18)
				{
					return replySxF0(stream, messageId);
				}
			}
			try
			{
				secsErrorCode=processMessage(stream, func, item, messageId);
			}
			catch (Exception ex)
			{
				secsErrorCode = SecsErrorCode.ErrorIllegalData;
				Console.WriteLine($"S{stream},F{func} process error");
				Console.WriteLine(ex.Message);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: { stream},F{ func} process error");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: { ex.Message +"\r\n"+ ex.StackTrace}");
			}
			
			return secsErrorCode;
		}

		private SecsErrorCode processMessage(byte stream, byte func, SecsItem item, uint messageId)
		{
			SecsErrorCode secsErrorCode = SecsErrorCode.Success;
			switch (stream)
			{
				case 1:
					switch (func)
					{
						case 0:
							secsErrorCode = onS1F0(item, messageId);
							break;
						case 1:
							secsErrorCode = onS1F1(item, messageId);
							break;
						case 2:
							secsErrorCode = onS1F2(item, messageId);
							break;
						case 3:
							secsErrorCode = onS1F3(item, messageId);
							break;
						case 11:
							secsErrorCode = onS1F11(item, messageId);
							break;
						case 13:
							secsErrorCode = onS1F13(item, messageId);
							break;
						case 14:
							secsErrorCode = onS1F14(item, messageId);
							break;
						case 15:
							secsErrorCode = onS1F15(item, messageId);
							break;
						case 17:
							secsErrorCode = onS1F17(item, messageId);
							break;
						case 21:
							secsErrorCode = onS1F21(item, messageId);
							break;                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
						case 23:
							secsErrorCode = onS1F23(item, messageId);
							break;
						default:
							return SecsErrorCode.ErrorFunctionType;
					}
					break;
				case 2:
					switch (func)
					{
						case 13:
							secsErrorCode = onS2F13(item, messageId);
							break;
						case 15:
							secsErrorCode = onS2F15(item, messageId);
							break;
						case 17:
							secsErrorCode = onS2F17(item, messageId);
							break;
						case 23:
							secsErrorCode = onS2F23(item, messageId);
							break;
						case 29:
							secsErrorCode = onS2F29(item, messageId);
							break;
						case 31:
							secsErrorCode = onS2F31(item, messageId);
							break;
						case 33:
							secsErrorCode = onS2F33(item, messageId);
							break;
						case 35:
							secsErrorCode = onS2F35(item, messageId);
							break;
						case 37:
							secsErrorCode = onS2F37(item, messageId);
							break;
						case 41:
							secsErrorCode = onS2F41(item, messageId);
							break;
						case 43:
							secsErrorCode = onS2F43(item, messageId);
							break;
						case 49:
							secsErrorCode = onS2F49(item, messageId);
							break;
						default:
							return SecsErrorCode.ErrorFunctionType;
					}
					break;
				case 5:
					switch (func)
					{
						case 2:
							secsErrorCode = SecsErrorCode.Success;
							break;
						case 3:
							secsErrorCode = onS5F3(item, messageId);
							break;
						case 5:
							secsErrorCode = onS5F5(item, messageId);
							break;
						case 7:
							secsErrorCode = onS5F7(item, messageId);
							break;
						default:
							return SecsErrorCode.ErrorFunctionType;
					}
					break;
				case 6:
					switch (func)
					{
						case 2:
							secsErrorCode = SecsErrorCode.Success;
							break;
						case 12:
							secsErrorCode = SecsErrorCode.Success;
							break;
						case 15:
							secsErrorCode = onS6F15(item, messageId);
							break;
						case 19:
							secsErrorCode = onS6F19(item, messageId);
							break;
						case 23:
							secsErrorCode = onS6F23(item, messageId);
							break;
						default:
							return SecsErrorCode.ErrorFunctionType;
					}
					break;
				case 7:
					switch (func)
					{
						case 1:
							secsErrorCode = onS7F1(item, messageId);
							break;
						case 3:
							secsErrorCode = onS7F3(item, messageId);
							break;
						case 5:
							secsErrorCode = onS7F5(item, messageId);
							break;
						case 17:
							secsErrorCode = onS7F17(item, messageId);
							break;
						case 19:
							secsErrorCode = onS7F19(item, messageId);
							break;
						case 23:
							secsErrorCode = onS7F23(item, messageId);
							break;
						case 25:
							secsErrorCode = onS7F25(item, messageId);
							break;
						default:
							return SecsErrorCode.ErrorFunctionType;
					}
					break;
				case 10:
					switch (func)
					{
						case 2:
							secsErrorCode = SecsErrorCode.Success;
							break;
						case 3:
							secsErrorCode = onS10F3(item, messageId);
							break;
						case 9:
							secsErrorCode = onS10F9(item, messageId);
							break;
						default:
							return SecsErrorCode.ErrorFunctionType;
					}
					break;
				default:
					return SecsErrorCode.ErrorStreamType;
			}
			return secsErrorCode;
		}

		public override void OnMessageReplyTimeout(uint messageId)
		{
			base.OnMessageReplyTimeout(messageId);
			if (messageId == m_msgId_S1F13 && CommunicationStateEquip == FSMNotCommunicating_EquipState.State.WaitCRA)
			{
				//Console.WriteLine("S1,F13 reply timeout");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: S1,F13 reply timeout");
				m_fsmCommStateEquip.TriggerEvent(FSMNotCommunicating_EquipState.Event.ConnectionTransactionFailure);
				m_msgId_S1F13 = 0u;
			}
			else if(messageId == m_msgId_S1F1)
            {
				Console.WriteLine("S1,F1 reply timeout");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: S1,F1 reply timeout");
				m_fsmCommState.TriggerEvent(FSMControlState.Event.S1F1ReplyTimeOut);
				m_msgId_S1F1 = 0u;
			}
		}

        #region  Send or Relpy Message To Host
        private SecsErrorCode replySxF0(byte stream, uint messageId)
		{
			ReplyMessage(stream, 0, null, messageId);
			return SecsErrorCode.Success;
		}

		private GemResult sendS5F1(uint alid, ALCD state, string text = "")
		{
			if (m_fsmCtrlState.ControlState != FSMControlState.State.OnlineLocal && m_fsmCtrlState.ControlState != FSMControlState.State.OnlineRemote)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ControlState Is Not Online");
				return GemResult.HsmsFailed;
			}
			if (!AlarmItemDict.ContainsKey(alid))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: AlarmItemDict Not Contains ALID: {alid}");
				return GemResult.UndefinedAlarmID;
			}
			AlarmItem alarmItem = AlarmItemDict[alid];
			if (!alarmItem.Enabled)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Alarm Enabled=False");
				return GemResult.AlarmDisabled;
			}
			byte b = alarmItem.ALCD;
			if (state == ALCD.Set)
			{
				b = (byte)(b + ALCD_MASK);
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(SecsItem.CreateBinaryItem(b));
			secsItem.AppendItem(new SecsItem(alid));
			secsItem.AppendItem(new SecsItem(string.IsNullOrEmpty(text) ? alarmItem.Text : text));
			SendMessage(5, 1, secsItem, needReply: true);
			uint num = ((state == ALCD.Set) ? alarmItem.CEIDSet : alarmItem.CEIDClear);
			if (num != GemEvent.InvalidEventID)
			{
				PostEvent(num);
			}
			return GemResult.Success;
		}

		internal uint SendS1F13()
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(new SecsItem(MDLN));
			secsItem.AppendItem(new SecsItem(SoftRev));
			m_msgId_S1F13 = SendMessage(1, 13, secsItem, needReply: true);
			return m_msgId_S1F13;
		}

		internal uint msgSend_S1F1()
		{
			m_msgId_S1F1 = SendMessage(1, 1, null, needReply: true);
			return m_msgId_S1F1;
		}
		#endregion

		#region S1 系列消息处理
		private SecsErrorCode onS1F0(SecsItem item, uint messageId)
		{
			if (messageId == m_msgId_S1F1)
			{
				Console.WriteLine("S1,F1 reply timeout");
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Send S1F1 Reply S1F0,MessageId={messageId}.");
				m_fsmCommState.TriggerEvent(FSMControlState.Event.ReveiveMessageS1F0);
				m_msgId_S1F1 = 0u;
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F1(SecsItem item, uint messageId)
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(new SecsItem(MDLN));
			secsItem.AppendItem(new SecsItem(SoftRev));
			ReplyMessage(1, 2, secsItem, messageId);
			if (OnHostRequestOnline() == ONLACK.Accepted)
			{
				m_fsmCtrlState.TriggerEvent(FSMControlState.Event.HostOnlineRequest);
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F2(SecsItem item, uint messageId)
		{
			if (messageId == m_msgId_S1F1)
            {
				if (item.Format != 0)
				{
					ReplyS1F14();
					return SecsErrorCode.ErrorIllegalData;
				}
				if (m_fsmCtrlState.DefaultOnlineState == FSMControlState.State.OnlineRemote)
				{
					m_fsmCtrlState.TriggerEvent(FSMControlState.Event.OnlineConfirmRemote);
				}
				else
				{
					m_fsmCtrlState.TriggerEvent(FSMControlState.Event.OnlineConfirmLocal);
				}
				m_msgId_S1F1 = 0u;
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F3(SecsItem item, uint messageId)
		{
			if (m_fsmCtrlState.ControlState == FSMControlState.State.EqOffline)
			{
				return replySxF0(1, messageId);
			}
			if (item.Format != 0)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (item.ItemList.Count == 0)
			{
				foreach (uint key in SVItemDict.Keys)
				{
					SecsItem item2 = null;
					if (OnGetSV(key, ref item2) != 0)
					{
						secsItem.AppendItem(new SecsItem(SecsFormat.List));
					}
					else
					{
						secsItem.AppendItem(item2);
					}
				}
			}
			else
			{
				for (int i = 0; i < item.ItemList.Count; i++)
				{
					uint svid = Convert.ToUInt32(item.ItemList[i].Value);
					SecsItem item3 = null;
					if (OnGetSV(svid, ref item3) != 0)
					{
						secsItem.AppendItem(new SecsItem(SecsFormat.List));
					}
					else
					{
						secsItem.AppendItem(item3);
					}
				}
			}
			ReplyMessage(1, 4, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F11(SecsItem item, uint messageId)
		{
			if (item.Format != 0)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (item.ItemList.Count == 0)
			{
				foreach (SVItem value in SVItemDict.Values)
				{
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(new SecsItem(value.SVID));
					secsItem2.AppendItem(new SecsItem(value.SVName));
					secsItem2.AppendItem(new SecsItem(value.Units));
					secsItem.AppendItem(secsItem2);
				}
			}
			else
			{
				for (int i = 0; i < item.ItemList.Count; i++)
				{
					uint key = Convert.ToUInt32(item.ItemList[i].Value);
					if (!SVItemDict.ContainsKey(key))
					{
						SecsItem secsItem3 = new SecsItem(SecsFormat.List);
						secsItem3.AppendItem(new SecsItem(key));
						secsItem3.AppendItem(new SecsItem(""));
						secsItem3.AppendItem(new SecsItem(""));
						secsItem.AppendItem(secsItem3);
						continue;
					}
					SVItem sVItem = SVItemDict[key];
					SecsItem secsItem4 = new SecsItem(SecsFormat.List);
					secsItem4.AppendItem(new SecsItem(sVItem.SVID));
					secsItem4.AppendItem(new SecsItem(sVItem.SVName));
					secsItem4.AppendItem(new SecsItem(sVItem.Units));
					secsItem.AppendItem(secsItem4);
				}
			}
			ReplyMessage(1, 12, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F13(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 0)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			m_msgId_RevS1F13 = messageId;
			if(CommunicationState== FSMCommunicationState.State.Communicating)
            {
				ReplyS1F14();
			}
			m_fsmCommStateHost.TriggerEvent(FSMNotCommunicating_HostState.Event.ReceiveCommRequest);
			return SecsErrorCode.Success;
		}
		public void ReplyS1F14()
		{
            if (m_msgId_RevS1F13 != 0u)
            {
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				SecsItem replyItem = SecsItem.CreateBinaryItem((byte)COMMACK.Accepted);
				secsItem.AppendItem(replyItem);
				SecsItem secsItem2 = new SecsItem(SecsFormat.List);
				secsItem2.AppendItem(new SecsItem(MDLN));
				secsItem2.AppendItem(new SecsItem(SoftRev));
				secsItem.AppendItem(secsItem2);
				ReplyMessage(1, 14, secsItem, m_msgId_RevS1F13);
				m_msgId_RevS1F13 = 0u;
			}
		}


		private SecsErrorCode onS1F14(SecsItem item, uint messageId)
		{
			m_fsmCommStateEquip.TriggerEvent(FSMNotCommunicating_EquipState.Event.ReveiveMessageS1F14);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F15(SecsItem item, uint messageId)
		{
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.HostOfflineRequest);
			SecsItem item2 = SecsItem.CreateBinaryItem(0);
			ReplyMessage(1, 16, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F17(SecsItem item, uint messageId)
		{
			ONLACK oNLACK = OnHostRequestOnline();
			if (oNLACK == ONLACK.Accepted)
			{
				m_fsmCtrlState.TriggerEvent(FSMControlState.Event.HostOnlineRequest);
			}
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)oNLACK);
			ReplyMessage(1, 18, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F21(SecsItem item, uint messageId)
		{
			if (item.Format != SecsFormat.List)
			{
				return SecsErrorCode.ErrorIllegalData;
			}

			SecsItem itemS1F22 = OnGetDVIDList(item);

			ReplyMessage(1, 22, itemS1F22, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS1F23(SecsItem item, uint messageId)
		{
			if (item.Format != SecsFormat.List)
			{
				return SecsErrorCode.ErrorIllegalData;
			}

			SecsItem itemS1F24 = OnGetCEIDList(item);

			ReplyMessage(1, 24, itemS1F24, messageId);
			return SecsErrorCode.Success;
		}
        #endregion

        #region S2 系列消息处理
        private SecsErrorCode onS2F13(SecsItem item, uint messageId)
		{
			if (item.Format != 0)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (item.ItemList.Count == 0)
			{
				foreach (uint key in ECItemDict.Keys)
				{
					SecsItem item2 = null;
					if (OnGetEC(key, ref item2) == GemResult.Success)
					{
						secsItem.AppendItem(item2);
					}
					else
					{
						secsItem.AppendItem(new SecsItem(SecsFormat.List));
					}
				}
			}
			else
			{
				for (int i = 0; i < item.ItemList.Count; i++)
				{
					if (item.ItemList[i] == null)
					{
						continue;
					}
					uint num = Convert.ToUInt32(item.ItemList[i].Value);
					SecsItem item3 = null;
					if (num == 97)
					{
						if (ECItemDict.ContainsKey(num))
						{
							secsItem.AppendItem(new SecsItem(Convert.ToUInt32(ECItemDict[num].DefaultValue)));
						}
					}
					else if (OnGetEC(num, ref item3) == GemResult.Success)
					{
						secsItem.AppendItem(item3);
					}
					else
					{
						secsItem.AppendItem(new SecsItem(SecsFormat.List));
					}
				}
			}
			ReplyMessage(2, 14, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F15(SecsItem item, uint messageId)
		{
			if (item.Format != 0)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			EAC eAC = EAC.Acknowledge;
			for (int i = 0; i < item.ItemList.Count; i++)
			{
				SecsItem secsItem = item.ItemList[i];
				if (secsItem.Format != 0)
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				uint num = Convert.ToUInt32(secsItem.ItemList[0].Value);
				if (!ECItemDict.ContainsKey(num))
				{
					eAC = EAC.DeniedECIDNotExist;
					break;
				}
				eAC = OnSetEC(num, secsItem.ItemList[1]);
				if (eAC != 0)
				{
					break;
				}
				LoadInternalEC();
			}
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)eAC);
			ReplyMessage(2, 16, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F17(SecsItem item, uint messageId)
		{
			if (item != null)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string v = DateTime.Now.ToString("yyyyMMddHHmmss") + (DateTime.Now.Millisecond / 10).ToString("D2");
			SecsItem item2 = new SecsItem(v);
			ReplyMessage(2, 18, item2, messageId);
			return SecsErrorCode.Success;
		}

        private SecsErrorCode onS2F23(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 5)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			TIAACK v = TIAACK.OK;
			string trid = item.ItemList[0].Value.ToString();
			string dsper = item.ItemList[1].Value as string;
			int num = parseDSPER(dsper);
			if (num == 0)
			{
				v = TIAACK.InvalidPeriod;
			}
			else
			{
				uint num2 = Convert.ToUInt32(item.ItemList[2].Value);
				if (num2 == 0)
				{
					stopTrace(trid);
				}
				else
				{
					uint num3 = Convert.ToUInt32(item.ItemList[3].Value);
					if (num3 == 0)
					{
						v = TIAACK.InvalidREPGSZ;
					}
					else
					{
						SecsItem secsItem = item.ItemList[4];
						if (item.Format != 0 || secsItem.ItemList.Count == 0)
						{
							return SecsErrorCode.ErrorIllegalData;
						}
						uint[] array = new uint[secsItem.ItemList.Count];
						for (int i = 0; i < secsItem.ItemList.Count; i++)
						{
							array[i] = Convert.ToUInt32(secsItem.ItemList[i].Value);
						}
						startTrace(trid, item.ItemList[0], num, num2, num3, array);
					}
				}
			}
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)v);
			ReplyMessage(2, 24, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F29(SecsItem item, uint messageId)
		{
			if (item.Format != 0)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (item.ItemList.Count == 0)
			{
				foreach (ECItem value in ECItemDict.Values)
				{
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(new SecsItem(value.ECID));
					secsItem2.AppendItem(new SecsItem(value.ECName));
					secsItem2.AppendItem(SecsUtil.CreateBasicItemFromString(value.Format, value.MinLimit));
					secsItem2.AppendItem(SecsUtil.CreateBasicItemFromString(value.Format, value.MaxLimit));
					secsItem2.AppendItem(SecsUtil.CreateBasicItemFromString(value.Format, value.DefaultValue));
					secsItem2.AppendItem(new SecsItem(value.Units));
					secsItem.AppendItem(secsItem2);
				}
			}
			else
			{
				for (int i = 0; i < item.ItemList.Count; i++)
				{
					if (item.ItemList[i] != null)
					{
                        if (!item.ItemList[i].IsUnsignedInterger())
                        {
							return SecsErrorCode.ErrorIllegalData;
						}
						uint num = Convert.ToUInt32(item.ItemList[i].Value);
						if (ECItemDict.ContainsKey(num))
						{
							ECItem eCItem = ECItemDict[num];
							SecsItem secsItem3 = new SecsItem(SecsFormat.List);
							secsItem3.AppendItem(new SecsItem(eCItem.ECID));
							secsItem3.AppendItem(new SecsItem(eCItem.ECName));
							secsItem3.AppendItem(SecsUtil.CreateBasicItemFromString(eCItem.Format, eCItem.MinLimit));
							secsItem3.AppendItem(SecsUtil.CreateBasicItemFromString(eCItem.Format, eCItem.MaxLimit));
							secsItem3.AppendItem(SecsUtil.CreateBasicItemFromString(eCItem.Format, eCItem.DefaultValue));
							secsItem3.AppendItem(new SecsItem(eCItem.Units));
							secsItem.AppendItem(secsItem3);
						}
						else
						{
							SecsItem secsItem4 = new SecsItem(SecsFormat.List);
							secsItem4.AppendItem(new SecsItem(num));
							secsItem4.AppendItem(new SecsItem(""));
							secsItem4.AppendItem(new SecsItem(""));
							secsItem4.AppendItem(new SecsItem(""));
							secsItem4.AppendItem(new SecsItem(""));
							secsItem4.AppendItem(new SecsItem(""));
							secsItem.AppendItem(secsItem4);
						}
					}
				}
			}
			ReplyMessage(2, 30, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F31(SecsItem item, uint messageId)
		{
			if (item.Format != SecsFormat.Ascii)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string timeStr = item.Value as string;
			TIACK v = OnSetClock(timeStr);
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)v);
			ReplyMessage(2, 32, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F33(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(item.ItemList[0].Value);
			DRACK dRACK = DRACK.Accept;
			if (item.ItemList[1].ItemList.Count == 0)
			{
				removeAllReport();
			}
			else
			{
				dRACK = updateReports(item.ItemList[1]);
			}
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)dRACK);
			ReplyMessage(2, 34, item2, messageId);
			if (EventLinkReportPersistEnabled && dRACK == DRACK.Accept)
			{
				storeEventReportSetting();
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F35(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(item.ItemList[0].Value);
			LRACK v = LRACK.Accepted;
			foreach (SecsItem item4 in item.ItemList[1].ItemList)
			{
				if (item4.Format != 0 || item4.ItemList.Count != 2)
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				uint key = Convert.ToUInt32(item4.ItemList[0].Value);
				if (!EventDict.ContainsKey(key))
				{
					v = LRACK.CEIDNotExist;
				}
				else
				{
					EventItem eventItem = EventDict[key];
					if (item4.ItemList[1].ItemList.Count <= 0 || eventItem.LinkedReportList.Count <= 0)
					{
						foreach (SecsItem item5 in item4.ItemList[1].ItemList)
						{
							uint key2 = Convert.ToUInt32(item5.Value);
							if (DataRptDict.ContainsKey(key2))
							{
								continue;
							}
							v = LRACK.RPTIDNotExist;
							goto IL_035e;
						}
						continue;
					}
					v = LRACK.CEIDDefined;
				}
				goto IL_035e;
			}
			foreach (SecsItem item6 in item.ItemList[1].ItemList)
			{
				uint num2 = Convert.ToUInt32(item6.ItemList[0].Value);
				if (EventLinkReportPersistEnabled && !m_persistEventSet.Contains(num2))
				{
					m_persistEventSet.Add(num2);
				}
				EventItem eventItem2 = EventDict[num2];
				if (item6.ItemList[1].ItemList.Count == 0)
				{
					lock (eventItem2.LinkedReportList)
					{
						eventItem2.LinkedReportList.Clear();
					}
					continue;
				}
				eventItem2.Enabled = EnableLinkedEventByDefault;
				if(eventItem2.LinkedReportList.Count != 0)
                {
					v = LRACK.CEIDDefined;
					goto IL_035e;
				}
				//Debug.Assert(eventItem2.LinkedReportList.Count == 0);
				foreach (SecsItem item7 in item6.ItemList[1].ItemList)
				{
					uint item2 = Convert.ToUInt32(item7.Value);
					lock (eventItem2.LinkedReportList)
					{
                        if (eventItem2.LinkedReportList.Contains(item2))
                        {
							v = LRACK.CEIDDefined;
							goto IL_035e;
						}
						eventItem2.LinkedReportList.Add(item2);
					}
				}
			}
			if (EventLinkReportPersistEnabled)
			{
				storeEventReportSetting();
			}
			goto IL_035e;
		IL_035e:
			SecsItem item3 = SecsItem.CreateBinaryItem((byte)v);
			ReplyMessage(2, 36, item3, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F37(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			byte b = 0;
			bool enabled = Convert.ToBoolean(item.ItemList[0].Value);
			if (item.ItemList[1].ItemList.Count == 0)
			{
				foreach (EventItem value in EventDict.Values)
				{
					value.Enabled = enabled;
					if (EventLinkReportPersistEnabled && !m_persistEventSet.Contains(value.CEID))
					{
						m_persistEventSet.Add(value.CEID);
					}
				}
			}
			else
			{
				foreach (SecsItem item3 in item.ItemList[1].ItemList)
				{
					uint key = Convert.ToUInt32(item3.Value);
					if (!EventDict.ContainsKey(key))
					{
						b = 1;
						break;
					}
				}
			}
			if (b == 0)
			{
				foreach (SecsItem item4 in item.ItemList[1].ItemList)
				{
					uint num = Convert.ToUInt32(item4.Value);
					EventDict[num].Enabled = enabled;
					if (EventLinkReportPersistEnabled && !m_persistEventSet.Contains(num))
					{
						m_persistEventSet.Add(num);
					}
				}
				if (EventLinkReportPersistEnabled)
				{
					storeEventReportSetting();
				}
			}
			SecsItem item2 = SecsItem.CreateBinaryItem(b);
			ReplyMessage(2, 38, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F43(SecsItem item, uint messageId)
		{
			byte v = 0;
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(SecsItem.CreateBinaryItem(v));
			secsItem.AppendItem(new SecsItem(SecsFormat.List));
			ReplyMessage(2, 44, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F41(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string cmd = item.ItemList[0].Value.ToString();
			SecsItem resultParam = null;
			HCACK v = OnHostCommand(cmd, item, ref resultParam);
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(SecsItem.CreateBinaryItem((byte)v));
			secsItem.AppendItem((resultParam == null) ? new SecsItem(SecsFormat.List) : resultParam);
			ReplyMessage(2, 42, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS2F49(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 4)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string objspec = item.ItemList[1].Value.ToString();
			string cmd = item.ItemList[2].Value.ToString();
			SecsItem param = item.ItemList[3];
			SecsItem resultParam = null;
			HCACK v = OnHostEnhancedCommand(cmd, objspec, param, ref resultParam);
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(SecsItem.CreateBinaryItem((byte)v));
			secsItem.AppendItem((resultParam == null) ? new SecsItem(SecsFormat.List) : resultParam);
			ReplyMessage(2, 50, secsItem, messageId);
			return SecsErrorCode.Success;
		}
        #endregion

        #region S5 系列消息处理
        private SecsErrorCode onS5F3(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			bool aled = item.ItemList[0].ToBoolean();
			uint? alid = null;
			if (item.ItemList[1].Value != null)
			{
				alid = Convert.ToUInt32(item.ItemList[1].Value);
			}
			ACKC5 aCKC = OnEnableAlarm(alid, aled);
			if (AlarmStatePersistEnabled && aCKC == ACKC5.Accepted)
			{
				storeAlarmState(alid, aled);
			}
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)aCKC);
			ReplyMessage(5, 4, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS5F5(SecsItem item, uint messageId)
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (item.Format == SecsFormat.List)
				return SecsErrorCode.ErrorIllegalData;
			if (item.ValueArray.Length == 0)
			{
				foreach (AlarmItem value in AlarmItemDict.Values)
				{
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(new SecsItem(value.ALCD));
					secsItem2.AppendItem(new SecsItem(value.ALID));
					secsItem2.AppendItem(new SecsItem(value.Text));
					secsItem.AppendItem(secsItem2);
				}
			}
			else
			{
				for (int i = 0; i < item.ValueArray.Length; i++)
				{
					uint num = Convert.ToUInt32(item.ValueArray[i]);
					if (AlarmItemDict.ContainsKey(num))
					{
						AlarmItem alarmItem = AlarmItemDict[num];
						SecsItem secsItem3 = new SecsItem(SecsFormat.List);
						secsItem3.AppendItem(new SecsItem(alarmItem.ALCD));
						secsItem3.AppendItem(new SecsItem(alarmItem.ALID));
						secsItem3.AppendItem(new SecsItem(alarmItem.Text));
						secsItem.AppendItem(secsItem3);
					}
					else
					{
						SecsItem secsItem4 = new SecsItem(SecsFormat.List);
						secsItem4.AppendItem(new SecsItem(SecsFormat.U1));
						secsItem4.AppendItem(new SecsItem(num));
						secsItem4.AppendItem(new SecsItem(SecsFormat.Ascii));
						secsItem.AppendItem(secsItem4);
					}
				}
			}
			ReplyMessage(5, 6, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS5F7(SecsItem item, uint messageId)
		{
			if (item != null)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			foreach (AlarmItem value in AlarmItemDict.Values)
			{
				if (value.Enabled)
				{
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(new SecsItem(value.ALCD));
					secsItem2.AppendItem(new SecsItem(value.ALID));
					secsItem2.AppendItem(new SecsItem(value.Text));
					secsItem.AppendItem(secsItem2);
				}
			}
			ReplyMessage(5, 8, secsItem, messageId);
			return SecsErrorCode.Success;
		}
        #endregion

        #region S6 系列消息处理
        private SecsErrorCode onS6F15(SecsItem item, uint messageId)
		{
			uint key = Convert.ToUInt32(item.Value);
			SecsItem secsItem = null;
			if (!EventDict.ContainsKey(key))
			{
				secsItem = new SecsItem(SecsFormat.List);
			}
			else
			{
				EventItem ev = EventDict[key];
				secsItem = makeEventReportItem(ev, null);
			}
			ReplyMessage(6, 16, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS6F23(SecsItem item, uint messageId)
		{
			byte v = 2;
			SecsItem item2 = SecsItem.CreateBinaryItem(v);
			ReplyMessage(6, 24, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS6F19(SecsItem item, uint messageId)
		{
			uint key = Convert.ToUInt32(item.Value);
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (DataRptDict.ContainsKey(key))
			{
				DataReport dataReport = DataRptDict[key];
				foreach (uint item3 in dataReport)
				{
					SecsItem item2 = null;
					if (OnGetDV(item3, ref item2) == GemResult.Success)
					{
						secsItem.AppendItem(item2);
					}
					else
					{
						secsItem.AppendItem(new SecsItem(SecsFormat.List));
					}
				}
			}
			ReplyMessage(6, 20, secsItem, messageId);
			return SecsErrorCode.Success;
		}
        #endregion

        #region S7 系列消息处理
        private SecsErrorCode onS7F1(SecsItem item, uint messageId)
		{
			SecsItem itemS7F2;
			if (ControlState != FSMControlState.State.OnlineRemote)
            {
				itemS7F2 = SecsItem.CreateBinaryItem((byte)PPGNT.NotAccept);
			}
            else
            {
				if (item.Format != SecsFormat.List || item.ItemList.Count != 2 ||
				!(item.ItemList[0].Format == SecsFormat.Ascii || item.ItemList[0].Format == SecsFormat.Binary))
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				var lengthItem = item.ItemList[1];
				if (lengthItem.Format != SecsFormat.I1 && lengthItem.Format != SecsFormat.I2 && lengthItem.Format != SecsFormat.I4 && lengthItem.Format != SecsFormat.I8
					&& lengthItem.Format != SecsFormat.U1 && lengthItem.Format != SecsFormat.U2 && lengthItem.Format != SecsFormat.U4 && lengthItem.Format != SecsFormat.U8)
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				var pPID = item.ItemList[0].Value.ToString();
				var pPIDLength = item.ItemList[1].Value.ToString();
				itemS7F2 = OnProcessRequire(pPID, pPIDLength);
			}
			ReplyMessage(7, 2, itemS7F2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS7F3(SecsItem item, uint messageId)
		{
			SecsItem itemS7F4;
			if (ControlState != FSMControlState.State.OnlineRemote)
			{
				itemS7F4 = SecsItem.CreateBinaryItem((byte)ACKC7.ModeUpsupported);
			}
            else
            {
				if (item.Format != SecsFormat.List || item.ItemList.Count != 2 ||
				!(item.ItemList[0].Format == SecsFormat.Ascii || item.ItemList[0].Format == SecsFormat.Binary))
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				var ppBodyItem = item.ItemList[1];
				if (ppBodyItem.Format != SecsFormat.I1 && ppBodyItem.Format != SecsFormat.I2 && ppBodyItem.Format != SecsFormat.I4 && ppBodyItem.Format != SecsFormat.I8
					&& ppBodyItem.Format != SecsFormat.U1 && ppBodyItem.Format != SecsFormat.U2 && ppBodyItem.Format != SecsFormat.U4 && ppBodyItem.Format != SecsFormat.U8
					&& ppBodyItem.Format != SecsFormat.Binary && ppBodyItem.Format != SecsFormat.Ascii)
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				itemS7F4 = OnProcessRecive(item);
			}
			ReplyMessage(7, 4, itemS7F4, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS7F5(SecsItem item, uint messageId)
		{
			if (item.Format != SecsFormat.Ascii)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = item.Value as string;
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			SecsItem secsItem2 = OnPPGetUnformatted(text);
			if (secsItem2 != null)
			{
				secsItem.AppendItem(new SecsItem(text));
				secsItem.AppendItem(secsItem2);
			}
			ReplyMessage(7, 6, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS7F17(SecsItem item, uint messageId)
		{
			ACKC7 aCKC = ACKC7.Accepted;
			if (ControlState != FSMControlState.State.OnlineRemote)
            {
				aCKC = ACKC7.ModeUpsupported;
			}
            else
            {
				if (item.Format != 0)
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				if (item.ItemList.Count == 0)
				{
					aCKC = OnPPDeleteAll();
				}
				else
				{
					foreach (SecsItem item3 in item.ItemList)
					{
						string ppid = item3.Value as string;
						aCKC = OnPPDelete(ppid);
						if (aCKC != 0)
						{
							break;
						}
					}
				}
			}
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)aCKC);
			ReplyMessage(7, 18, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS7F23(SecsItem item, uint messageId)
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			if (ControlState != FSMControlState.State.OnlineRemote)
			{
				secsItem = SecsItem.CreateBinaryItem((byte)ACKC7.ModeUpsupported);
			}
            else
            {
				if (item.Format != 0)
				{
					return SecsErrorCode.ErrorIllegalData;
				}
				
				secsItem = OnPPDownload(item);
			}
			ReplyMessage(7, 24, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS7F19(SecsItem item, uint messageId)
		{
			if (item != null)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			List<string> ppList = null;
			if (OnPPGetList(ref ppList) == GemResult.Success)
			{
				foreach (string item2 in ppList)
				{
					secsItem.AppendItem(new SecsItem(item2));
				}
			}
			ReplyMessage(7, 20, secsItem, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS7F25(SecsItem item, uint messageId)
		{
			if (item.Format != SecsFormat.Ascii)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = item.Value as string;
			SecsItem secsItem = OnPPGetFormatted(text);
			SecsItem secsItem2 = new SecsItem(SecsFormat.List);
			if (secsItem != null)
			{
				secsItem2.AppendItem(new SecsItem(text));
				secsItem2.AppendItem(new SecsItem(MDLN));
				secsItem2.AppendItem(new SecsItem(SoftRev));
				secsItem2.AppendItem(secsItem);
			}
			ReplyMessage(7, 26, secsItem2, messageId);
			return SecsErrorCode.Success;
		}
        #endregion

        #region S10 系列消息处理
        private SecsErrorCode onS10F3(SecsItem item, uint messageId)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			if (item.ItemList[0].Format != SecsFormat.Binary)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			byte[] array = item.ItemList[0].Value as byte[];
			if (array.Length != 1)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			int terminalId = array[0];
			string text = item.ItemList[1].Value as string;
			ACKC10 v = OnTerminalMessage(terminalId, text);
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)v);
			ReplyMessage(10, 4, item2, messageId);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onS10F9(SecsItem item, uint messageId)
		{
			if (item.Format != SecsFormat.Ascii)
			{
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = item.Value as string;
			ACKC10 v = OnTerminalMessage(text);
			SecsItem item2 = SecsItem.CreateBinaryItem((byte)v);
			ReplyMessage(10, 10, item2, messageId);
			return SecsErrorCode.Success;
		}
        #endregion

        #region CEID.RPID,VID Link初始化动作
        private void removeReport(uint rptId)
		{
			foreach (EventItem value in EventDict.Values)
			{
				lock (value.LinkedReportList)
				{
					value.LinkedReportList.Remove(rptId);
				}
			}
			lock (DataRptDict)
			{
				DataRptDict.Remove(rptId);
			}
		}

		private void removeAllReport()
		{
			foreach (EventItem value in EventDict.Values)
			{
				lock (value.LinkedReportList)
				{
					value.LinkedReportList.Clear();
				}
			}
			lock (DataRptDict)
			{
				DataRptDict.Clear();
			}
		}

		private SecsItem makeEventReportItem(EventItem ev, DataVariableSet dvSet, object dvProvider = null)
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(new SecsItem((ushort)0));
			secsItem.AppendItem(new SecsItem(ev.CEID));
			SecsItem secsItem2 = new SecsItem(SecsFormat.List);
			lock (ev.LinkedReportList)
			{
				for (int i = 0; i < ev.LinkedReportList.Count; i++)
				{
					SecsItem secsItem3 = new SecsItem(SecsFormat.List);
					uint num = ev.LinkedReportList[i];
					secsItem3.AppendItem(new SecsItem(num));
					SecsItem secsItem4 = new SecsItem(SecsFormat.List);
					lock (DataRptDict)
					{
						if (!DataRptDict.ContainsKey(num))
						{
							continue;
						}
						DataReport dataReport = DataRptDict[num];
						for (int j = 0; j < dataReport.Count; j++)
						{
							uint num2 = dataReport[j];
							SecsItem item = null;
							if (dvSet != null)
							{
								object value = dvSet.GetValue(num2);
								if (value != null)
								{
									if (value.GetType() == typeof(SecsItem))
									{
										SecsItem item2 = value as SecsItem;
										secsItem4.AppendItem(item2);
										continue;
									}
									if (DVItemDict.ContainsKey(num2))
									{
										DVItem dVItem = DVItemDict[num2];
										item = SecsItem.CreateItem(value, dVItem.Format);
									}
									else
									{
										item = SecsItem.CreateItem(value);
									}
									secsItem4.AppendItem(item);
									continue;
								}
							}
							if (dvProvider != null)
							{
								if (dvProvider is IDVProvider iDVProvider)
								{
									SecsItem dV = iDVProvider.GetDV(num2);
									if (dV != null)
									{
										secsItem4.AppendItem(dV);
									}
									else if (OnGetDV(num2, ref item) == GemResult.Success)
									{
										secsItem4.AppendItem(item);
									}
								}
								else if (OnGetDV(num2, ref item) == GemResult.Success)
								{
									secsItem4.AppendItem(item);
								}
							}
							else if (OnGetDV(num2, ref item) == GemResult.Success)
							{
								secsItem4.AppendItem(item);
							}
						}
						goto IL_0265;
					}
				IL_0265:
					secsItem3.AppendItem(secsItem4);
					secsItem2.AppendItem(secsItem3);
				}
			}
			secsItem.AppendItem(secsItem2);
			return secsItem;
		}

		public GemResult ReportAppendVID(uint rptId, uint vid)
		{
			lock (DataRptDict)
			{
				DataReport dataReport = null;
				if (DataRptDict.ContainsKey(rptId))
				{
					dataReport = DataRptDict[rptId];
					if (dataReport.Contains(vid))
					{
						return GemResult.AlreadyExist;
					}
				}
				else
				{
					dataReport = new DataReport(rptId);
					DataRptDict[rptId] = dataReport;
				}
				dataReport.Add(vid);
			}
			return GemResult.Success;
		}

		public GemResult EnableEvent(uint eventId, bool enabled)
		{
			lock (EventDict)
			{
				if (!EventDict.ContainsKey(eventId))
				{
					return GemResult.UndefinedEventID;
				}
				EventItem eventItem = EventDict[eventId];
				eventItem.Enabled = enabled;
			}
			return GemResult.Success;
		}

		public GemResult LinkEventReport(uint eventId, uint rptId)
		{
			lock (DataRptDict)
			{
				if (!DataRptDict.ContainsKey(rptId))
				{
					return GemResult.UndefinedReportID;
				}
			}
			EventItem eventItem = null;
			lock (EventDict)
			{
				if (!EventDict.ContainsKey(eventId))
				{
					return GemResult.UndefinedEventID;
				}
				eventItem = EventDict[eventId];
			}
			lock (eventItem.LinkedReportList)
			{
				if (eventItem.LinkedReportList.Contains(rptId))
				{
					return GemResult.AlreadyExist;
				}
				eventItem.LinkedReportList.Add(rptId);
			}
			return GemResult.Success;
		}

		private DRACK updateReports(SecsItem itemDef)
		{
			if (itemDef.Format != 0)
			{
				return DRACK.InvalidFormat;
			}
			DRACK result = DRACK.Accept;
			foreach (SecsItem item2 in itemDef.ItemList)
			{
				if (item2.Format != 0 || item2.ItemList.Count != 2)
				{
					return DRACK.InvalidFormat;
				}
				if (item2.ItemList[1].ItemList.Count == 0)
				{
					continue;
				}
				uint key = Convert.ToUInt32(item2.ItemList[0].Value);
				lock (DataRptDict)
				{
					if (DataRptDict.ContainsKey(key))
					{
						return DRACK.ReportIDDefined;
					}
				}
				foreach (SecsItem item3 in item2.ItemList[1].ItemList)
				{
					uint key2 = Convert.ToUInt32(item3.Value);
					if (!DVItemDict.ContainsKey(key2) && !SVItemDict.ContainsKey(key2))
					{
						return DRACK.VIDNotExist;
					}
				}
			}
			foreach (SecsItem item4 in itemDef.ItemList)
			{
				uint num = Convert.ToUInt32(item4.ItemList[0].Value);
				if (item4.ItemList[1].ItemList.Count == 0)
				{
					removeReport(num);
					continue;
				}
				DataReport dataReport = new DataReport(num);
				foreach (SecsItem item5 in item4.ItemList[1].ItemList)
				{
					uint item = Convert.ToUInt32(item5.Value);
					dataReport.Add(item);
				}
				DataRptDict[num] = dataReport;
			}
			return result;
		}

		#endregion

		#region Trace report 功能相关方法
		private int parseDSPER(string dsper)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: dsper= dsper.");
			if (dsper.Length != 6 && dsper.Length != 8)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: dsper data error.");
				return 0;
			}
			int num = Convert.ToInt32(dsper.Substring(0, 2));
			int num2 = Convert.ToInt32(dsper.Substring(2, 2));
			int num3 = Convert.ToInt32(dsper.Substring(4, 2));
			int num4 = (num * 3600 + num2 * 60 + num3) * 1000;
			if (dsper.Length == 8)
			{
				int num5 = Convert.ToInt32(dsper.Substring(6, 2));
				num4 += num5 * 10;
			}
			if (num4 < SamplePeriodLimitMin)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: msec={num4},SamplePeriodLimitMin={SamplePeriodLimitMin}.");
				return 0;
			}
			return num4;
		}

		private TIAACK startTrace(string trid, SecsItem tridItem, int dsper, uint totsmp, uint repgsz, uint[] svidArray)
		{
			stopTrace(trid);
			DataTraceInfo dataTraceInfo = new DataTraceInfo
			{
				DSPER = dsper,
				REPGSZ = repgsz,
				TOTSMP = totsmp,
				TRID = trid,
				RawTRID = tridItem,
				SVIDArray = svidArray,
				EQ = this
			};
			dataTraceInfo.SamplingThread = new Thread(traceThread);
			dataTraceInfo.StopEvent = new ManualResetEvent(initialState: false);
			dataTraceInfo.SamplingThread.Start(dataTraceInfo);
			lock (m_traceDict)
			{
				m_traceDict[trid] = dataTraceInfo;
			}
			return TIAACK.OK;
		}

		private void stopTrace(string trid)
		{
			lock (m_traceDict)
			{
				if (m_traceDict.ContainsKey(trid))
				{
					DataTraceInfo dataTraceInfo = m_traceDict[trid];
					dataTraceInfo.StopEvent.Set();
					dataTraceInfo.SamplingThread.Join(100);
					m_traceDict.Remove(trid);
				}
			}
		}

		protected void stopAllTrace()
		{
			lock (m_traceDict)
			{
				foreach (DataTraceInfo value in m_traceDict.Values)
				{
					value.StopEvent.Set();
					value.SamplingThread.Join(100);
				}
				m_traceDict.Clear();
			}
		}

		private static void traceThread_Old(object param)
		{
			DataTraceInfo dataTraceInfo = param as DataTraceInfo;
			int millisecondsTimeout = dataTraceInfo.DSPER;
			uint num = 0u;
			uint num2 = 0u;
			while (!dataTraceInfo.StopEvent.WaitOne(millisecondsTimeout))
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				Stopwatch stopwatch = Stopwatch.StartNew();
				for (int i = 0; i < dataTraceInfo.SVIDArray.Length; i++)
				{
					uint num3 = dataTraceInfo.SVIDArray[i];
					SecsItem item = null;
					if (dataTraceInfo.EQ.OnGetSV(num3, ref item) != 0)
					{
						Console.WriteLine($"get SV [{num3}] failed");
						FALoggerClass.Inst().Error($"[GemEquipment] [traceThread]: get SV [{num3}] failed");
					}
					else
					{
						secsItem.AppendItem(item);
					}
				}
				num++;
				num2++;
				if (num == dataTraceInfo.REPGSZ)
				{
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(dataTraceInfo.RawTRID);
					secsItem2.AppendItem(new SecsItem(num2));
					string v = DateTime.Now.ToString("yyyyMMddHHmmss") + (DateTime.Now.Millisecond / 10).ToString("D2");
					secsItem2.AppendItem(new SecsItem(v));
					secsItem2.AppendItem(secsItem);
					dataTraceInfo.EQ.SendMessage(6, 1, secsItem2, needReply: true);
					num = 0u;
				}
				if (num2 >= dataTraceInfo.TOTSMP)
				{
					break;
				}
				stopwatch.Stop();
				int num4 = (int)stopwatch.ElapsedMilliseconds;
				millisecondsTimeout = ((dataTraceInfo.DSPER > num4) ? (dataTraceInfo.DSPER - num4) : 0);
			}
		}

		private static void traceThread(object param)
		{
			DataTraceInfo dataTraceInfo = param as DataTraceInfo;
			int millisecondsTimeout = dataTraceInfo.DSPER;
			uint groupCount = 0u;   // 当前组已采样数量
			uint totalCount = 0u;   // 总采样数量

			SecsItem traceBuffer = null;
			if (dataTraceInfo.REPGSZ > 1)
			{
				traceBuffer = new SecsItem(SecsFormat.List);
			}

			while (!dataTraceInfo.StopEvent.WaitOne(millisecondsTimeout))
			{
				Stopwatch stopwatch = Stopwatch.StartNew();

				// 一次 Sample（包含所有 SVID）
				SecsItem sample = new SecsItem(SecsFormat.List);

				for (int i = 0; i < dataTraceInfo.SVIDArray.Length; i++)
				{
					uint svid = dataTraceInfo.SVIDArray[i];
					SecsItem item = null;

					if (dataTraceInfo.EQ.OnGetSV(svid, ref item) != 0)
					{
						Console.WriteLine($"get SV [{svid}] failed");
						FALoggerClass.Inst().Error($"[GemEquipment] [traceThread]: get SV [{svid}] failed");
					}
					else
					{
						sample.AppendItem(item);
					}
				}

				groupCount++;
				totalCount++;

				if (dataTraceInfo.REPGSZ == 1)
				{
					// 每采样一次立即发送
					SecsItem msg = new SecsItem(SecsFormat.List);
					msg.AppendItem(dataTraceInfo.RawTRID);
					msg.AppendItem(new SecsItem(totalCount));

					string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss") +
									   (DateTime.Now.Millisecond / 10).ToString("D2");

					msg.AppendItem(new SecsItem(timestamp));
					msg.AppendItem(sample);

					dataTraceInfo.EQ.SendMessage(6, 1, msg, needReply: true);

					groupCount = 0;
				}
				else
				{
					traceBuffer.AppendItem(sample);

					if (groupCount == dataTraceInfo.REPGSZ)
					{
						SecsItem msg = new SecsItem(SecsFormat.List);
						msg.AppendItem(dataTraceInfo.RawTRID);
						msg.AppendItem(new SecsItem(totalCount));

						string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss") +
										   (DateTime.Now.Millisecond / 10).ToString("D2");

						msg.AppendItem(new SecsItem(timestamp));
						msg.AppendItem(traceBuffer);

						dataTraceInfo.EQ.SendMessage(6, 1, msg, needReply: true);

						groupCount = 0;
						traceBuffer = new SecsItem(SecsFormat.List);
					}
				}

				if (totalCount >= dataTraceInfo.TOTSMP)
				{
					// 最后一组不足 REPGSZ，也要发送
					if (dataTraceInfo.REPGSZ > 1 && groupCount > 0)
					{
						SecsItem msg = new SecsItem(SecsFormat.List);
						msg.AppendItem(dataTraceInfo.RawTRID);
						msg.AppendItem(new SecsItem(totalCount));

						string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss") +
										   (DateTime.Now.Millisecond / 10).ToString("D2");

						msg.AppendItem(new SecsItem(timestamp));
						msg.AppendItem(traceBuffer);

						dataTraceInfo.EQ.SendMessage(6, 1, msg, needReply: true);
					}

					break;
				}

				stopwatch.Stop();
				int elapsed = (int)stopwatch.ElapsedMilliseconds;
				millisecondsTimeout = (dataTraceInfo.DSPER > elapsed)
					? (dataTraceInfo.DSPER - elapsed)
					: 0;
			}
		}
		#endregion

		#region 其它
		private bool storeEventReportSetting()
		{
			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				XmlNode xmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "EVENT_REPORT", "");
				xmlDocument.AppendChild(xmlNode);
				foreach (DataReport value in DataRptDict.Values)
				{
					XmlNode xmlNode2 = xmlDocument.CreateNode(XmlNodeType.Element, "REPORT", "");
					SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode2, "id", value.ID.ToString());
					for (int i = 0; i < value.Count; i++)
					{
						XmlNode xmlNode3 = xmlDocument.CreateNode(XmlNodeType.Element, "DV", "");
						SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode3, "id", value[i].ToString());
						xmlNode2.AppendChild(xmlNode3);
					}
					xmlNode.AppendChild(xmlNode2);
				}
				foreach (uint item in m_persistEventSet)
				{
					if (!EventDict.ContainsKey(item))
					{
						Console.WriteLine($"ERROR: CEID [{item}] is not defined. ");
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: CEID [{item}] is not defined.");
						continue;
					}
					EventItem eventItem = EventDict[item];
					if (eventItem.CEID != item)
					{
						return false;
					}
					//Debug.Assert(eventItem.CEID == item);
					XmlNode xmlNode4 = xmlDocument.CreateNode(XmlNodeType.Element, "EVENT", "");
					SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode4, "id", eventItem.CEID.ToString());
					SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode4, "enabled", eventItem.Enabled ? "1" : "0");
					for (int j = 0; j < eventItem.LinkedReportList.Count; j++)
					{
						XmlNode xmlNode5 = xmlDocument.CreateNode(XmlNodeType.Element, "RPT", "");
						SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode5, "id", eventItem.LinkedReportList[j].ToString());
						xmlNode4.AppendChild(xmlNode5);
					}
					xmlNode.AppendChild(xmlNode4);
				}
				string filename = m_persistFilePath + "event-report.xml";
				xmlDocument.Save(filename);
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Exception:{ex.Message + "\r\n"+ex.StackTrace}");
				Console.WriteLine(ex.Message);
				return false;
			}
			return true;
		}

		private bool loadPersistEventReportSetting()
		{
			if (!EventLinkReportPersistEnabled)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: loadPersistEventReportSetting shouldn't be called when EventLinkReportPersistEnabled is false.");
				throw new Exception("loadPersistEventReportSetting shouldn't be called when EventLinkReportPersistEnabled is false. ");
			}
			string text = m_persistFilePath + "event-report.xml";
			try
			{
				if (!File.Exists(text))
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:File Not Exists，persistFileName：{text}");
					return true;
				}
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.Load(text);
				XmlNode xmlNode = xmlDocument.SelectSingleNode("/EVENT_REPORT");
				XmlNodeList xmlNodeList = xmlNode.SelectNodes("REPORT");
				foreach (XmlNode item in xmlNodeList)
				{
					string value = SecsUtil.XmlGetNodeAttrVal(item, "id");
					uint num = Convert.ToUInt32(value);
					if (DataRptDict.ContainsKey(num))
					{
						DataRptDict.Remove(num);
					}
					XmlNodeList xmlNodeList2 = item.SelectNodes("DV");
					foreach (XmlNode item2 in xmlNodeList2)
					{
						string value2 = SecsUtil.XmlGetNodeAttrVal(item2, "id");
						uint vid = Convert.ToUInt32(value2);
						ReportAppendVID(num, vid);
					}
				}
				XmlNodeList xmlNodeList3 = xmlNode.SelectNodes("EVENT");
				foreach (XmlNode item3 in xmlNodeList3)
				{
					string value3 = SecsUtil.XmlGetNodeAttrVal(item3, "id");
					uint num2 = Convert.ToUInt32(value3);
					if (!EventDict.ContainsKey(num2))
					{
						Console.WriteLine($"ERROR: CEID[{num2}] is not defined. ");
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:ERROR: CEID[{num2}] is not defined.");
						continue;
					}
					m_persistEventSet.Add(num2);
					EventItem eventItem = EventDict[num2];
					eventItem.LinkedReportList.Clear();
					string text2 = SecsUtil.XmlGetNodeAttrVal(item3, "enabled");
					eventItem.Enabled = !text2.Trim().Equals("0");
					XmlNodeList xmlNodeList4 = item3.SelectNodes("RPT");
					foreach (XmlNode item4 in xmlNodeList4)
					{
						string value4 = SecsUtil.XmlGetNodeAttrVal(item4, "id");
						uint num3 = Convert.ToUInt32(value4);
						if (!DataRptDict.ContainsKey(num3))
						{
							Console.WriteLine($"ERROR: DataReport[{num3}] is not defined. ");
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:ERROR: DataReport[{num3}] is not defined.");
						}
						else
						{
							eventItem.LinkedReportList.Add(num3);
						}
					}
				}
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Exception: {ex.Message + "\r\n"+ex.StackTrace}");
				Console.WriteLine(ex.Message);
				return false;
			}
			return true;
		}

		private void storeAlarmState(uint? alid, bool aled)
		{
			if (!alid.HasValue)
			{
				m_persistAlarmAllEnabled = aled;
				m_persistAlarmStateDict.Clear();
			}
			else
			{
				uint value = alid.Value;
				m_persistAlarmStateDict[value] = aled;
			}
			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				XmlNode xmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "ALARM_STATE", "");
				xmlDocument.AppendChild(xmlNode);
				string value2 = "";
				if (m_persistAlarmAllEnabled.HasValue)
				{
					value2 = (m_persistAlarmAllEnabled.Value ? "1" : "0");
				}
				SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode, "all", value2);
				foreach (uint key in m_persistAlarmStateDict.Keys)
				{
					XmlNode xmlNode2 = xmlDocument.CreateNode(XmlNodeType.Element, "ALARM", "");
					SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode2, "id", key.ToString());
					SecsUtil.XmlSetNodeAttrVal(xmlDocument, xmlNode2, "enabled", m_persistAlarmStateDict[key] ? "1" : "0");
					xmlNode.AppendChild(xmlNode2);
				}
				string filename = m_persistFilePath + "alarm-state.xml";
				xmlDocument.Save(filename);
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Exception: {ex.Message + "\r\n" + ex.StackTrace}");
				Console.WriteLine(ex.Message);
			}
		}

		private bool loadPersistAlarmState()
		{
			if (!AlarmStatePersistEnabled)
			{
				throw new Exception("loadPersistAlarmState shouldn't be called when AlarmStatePersistEnabled is false. ");
			}
			string text = m_persistFilePath + "alarm-state.xml";
			try
			{
				if (!File.Exists(text))
				{
					return true;
				}
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.Load(text);
				XmlNode xmlNode = xmlDocument.SelectSingleNode("/ALARM_STATE");
				string text2 = SecsUtil.XmlGetNodeAttrVal(xmlNode, "all");
				if (text2.Length == 0)
				{
					m_persistAlarmAllEnabled = null;
				}
				else
				{
					m_persistAlarmAllEnabled = !text2.Equals("0");
					foreach (AlarmItem value2 in AlarmItemDict.Values)
					{
						value2.Enabled = m_persistAlarmAllEnabled.Value;
					}
				}
				XmlNodeList xmlNodeList = xmlNode.SelectNodes("ALARM");
				foreach (XmlNode item in xmlNodeList)
				{
					string value = SecsUtil.XmlGetNodeAttrVal(item, "id");
					uint num = Convert.ToUInt32(value);
					string text3 = SecsUtil.XmlGetNodeAttrVal(item, "enabled");
					if (text3.Trim().Length == 0)
					{
						return false;
					}
					bool flag = !text3.Equals("0");
					if (AlarmItemDict.ContainsKey(num))
					{
						AlarmItemDict[num].Enabled = flag;
						m_persistAlarmStateDict[num] = flag;
					}
					else
					{
						Console.WriteLine($"WARNING: alarm item(id={num}) not defined. ");
					}
				}
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Exception: {ex.Message + "\r\n" + ex.StackTrace}");
				Console.WriteLine(ex.Message);
				return false;
			}
			return true;
		}

		internal void DefaultControlState()
        {
			m_fsmCtrlState.TriggerEvent(FSMControlState.Event.ControlInitialization);
		}
		#endregion
	}
}
