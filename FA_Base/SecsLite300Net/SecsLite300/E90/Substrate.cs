using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;
using SecsLite300.E40;
using SecsLite300.E87;

namespace SecsLite300.E90
{
	public class Substrate : OSSObject, ISubstrate
	{
		public List<SubstHistoryInfo> SubstHistoryList = null;

		private List<ISubstrateHistoryInfo> m_historyInfoList = new List<ISubstrateHistoryInfo>();

		private FSMSubstrateTransport m_fsmTransport = null;

		private FSMSubstrateProcessing m_fsmProcessing = null;

		private FSMSubstrateIDReading m_fsmIDReading = null;

		private static readonly string attrAcquiredID = "AcquiredID";

		private static readonly string attrSubstDestination = "SubstDestination";

		private static readonly string attrSubstHistory = "SubstHistory";

		private static readonly string attrSubstID = "SubstID";

		private static readonly string attrSubstIDStatus = "SubstIDStatus";

		private static readonly string attrLotID = "LotID";

		private static readonly string attrMaterialStatus = "MaterialStatus";

		private static readonly string attrSubstPosInBatch = "SubstPosInBatch";

		private static readonly string attrSubstProcState = "SubstProcState";

		private static readonly string attrSubstSource = "SubstSource";

		private static readonly string attrSubstState = "SubstState";

		private static readonly string attrSubstType = "SubstType";

		private static readonly string attrSubstUsage = "SubstUsage";

		private static readonly string attrSubstLocID = "SubstLocSubstID";

		private static readonly string attrBatchLocID = "BatchLocID";
		
		private static readonly string attrSubstLocStateID = "SubstLocSubstLocID";

		private static List<string> m_attrNameList = null;

		public string SubstDestination { get; }

		public SubstIDReadingState SubstIDState => (m_fsmIDReading == null) ? SubstIDReadingState.NotConfirmed : (SubstIDReadingState)m_fsmIDReading.CurrentState;

		public SubstProcessingState SubstProcState => (m_fsmProcessing == null) ? SubstProcessingState.NeedsProcessing : (SubstProcessingState)m_fsmProcessing.CurrentState;

		public SubstTransportState SubstState => (m_fsmTransport == null) ? SubstTransportState.AtSource : (SubstTransportState)m_fsmTransport.CurrentState;

		public string LotID { get; set; }

		public string SubstLocID { get; set; }

		public string SubstSource { get; set; }

		public SubstrateType SubstType { get; set; }

		public SubstUsage Usage { get; set; }

		public ICarrier CurrentCarrier { get; set; }

		public List<ISubstrateHistoryInfo> SubstrateHistory => m_historyInfoList;

		public string AcquiredID { get; set; }

		public string CurrentSlot { get; set; }

		public int SourceSlot { get; set; }

		public string ProcessJobID { get; set; }

		public virtual string ClassName => GetType().Name;

		public Substrate(string id)
			: base(id, "Substrate")
		{
			m_fsmTransport = new FSMSubstrateTransport(this);
			m_fsmTransport.Name = base.ObjID + "_Transport";
			m_fsmProcessing = new FSMSubstrateProcessing(this);
			m_fsmProcessing.Name = base.ObjID + "_Processing";
			m_fsmIDReading = new FSMSubstrateIDReading(this);
			m_fsmIDReading.Name = base.ObjID + "_IDReading";
		}

		public void Start()
		{
			if (m_fsmTransport != null && m_fsmProcessing != null && m_fsmIDReading != null)
			{
				m_fsmTransport.Start();
				Thread.Sleep(200);
				m_fsmProcessing.Start();
				Thread.Sleep(200);
				m_fsmIDReading.Start();
			}
		}

		public override void Release()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsmTransport.Stop();
			m_fsmProcessing.Stop();
			m_fsmIDReading.Stop();
			base.Release();
		}

		public override SecsItem GetAttribute(string attrName)
		{
			SecsItem attribute = base.GetAttribute(attrName);
			if (attribute != null)
			{
				return attribute;
			}
			if (OSSObject.AttrNameEqual(attrName, attrAcquiredID))
			{
				return new SecsItem(AcquiredID);
			}
			if (!OSSObject.AttrNameEqual(attrName, attrBatchLocID))
			{
				if (OSSObject.AttrNameEqual(attrName, attrLotID))
				{
					return new SecsItem(LotID);
				}
				if (OSSObject.AttrNameEqual(attrName, attrSubstID))
				{
					return new SecsItem(base.ObjID);
				}
				if (OSSObject.AttrNameEqual(attrName, attrSubstLocID))
				{
					return new SecsItem(base.ObjID);
				}
				if (!OSSObject.AttrNameEqual(attrName, attrMaterialStatus))
				{
					if (OSSObject.AttrNameEqual(attrName, attrSubstDestination))
					{
						return new SecsItem(SubstDestination);
					}
					if (OSSObject.AttrNameEqual(attrName, attrSubstHistory))
					{
						SecsItem secsItem = new SecsItem(SecsFormat.List);
                        if (m_historyInfoList?.Count > 0)
                        {
							for (int i = 0; i < m_historyInfoList.Count; i++)
							{
								ISubstrateHistoryInfo substrateHistoryInfo = m_historyInfoList[i];
								SecsItem secsItem2 = new SecsItem(SecsFormat.List);
								secsItem2.AppendItem(new SecsItem(substrateHistoryInfo.SubstLocID));
								secsItem2.AppendItem(new SecsItem(SecsUtil.FormatTimeString(substrateHistoryInfo.TimeIn, Gem300Manager.EquipmentObject.TimeStringFormat)));
								secsItem2.AppendItem(new SecsItem(SecsUtil.FormatTimeString(substrateHistoryInfo.TimeOut, Gem300Manager.EquipmentObject.TimeStringFormat)));
								secsItem.AppendItem(secsItem2);
							}
						}
						return secsItem;
					}
					if (OSSObject.AttrNameEqual(attrName, attrSubstIDStatus))
					{
						return new SecsItem((byte)SubstIDState);
					}
					if (OSSObject.AttrNameEqual(attrName, attrSubstLocStateID))
					{
						return new SecsItem(SubstLocID);
					}
					if (!OSSObject.AttrNameEqual(attrName, attrSubstPosInBatch))
					{
						if (OSSObject.AttrNameEqual(attrName, attrSubstProcState))
						{
							return new SecsItem((byte)SubstProcState);
						}
						if (OSSObject.AttrNameEqual(attrName, attrSubstSource))
						{
							return new SecsItem(SubstSource);
						}
						if (OSSObject.AttrNameEqual(attrName, attrSubstState))
						{
							return new SecsItem((int)(SubstState - 2));
						}
						if (OSSObject.AttrNameEqual(attrName, attrSubstType))
						{
							return new SecsItem((byte)SubstType);
						}
						if (OSSObject.AttrNameEqual(attrName, attrSubstUsage))
						{
							return new SecsItem((byte)Usage);
						}
					}
				}
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName={attrName}");
			return null;
		}

		public override ERRCODE SetAttribute(string attrName, SecsItem itemValue, ref string errText)
		{
			ERRCODE eRRCODE = base.SetAttribute(attrName, itemValue, ref errText);
			if (eRRCODE != ERRCODE.UnknownAttributeName)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: errCode != ERRCODE.UnknownAttributeName");
				return eRRCODE;
			}
			string text = m_attrNameList.Find((string x) => OSSObject.AttrNameEqual(x, attrName));
			if (text == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attr == null");
				return ERRCODE.UnknownAttributeName;
			}
			if (!OSSObject.AttrNameEqual(attrName, attrLotID))
			{
				if (OSSObject.AttrNameEqual(attrName, attrSubstType))
				{
					if (itemValue.Format != SecsFormat.U1)
					{
						return ERRCODE.InvalidAttributeValue;
					}
					SubstType = (SubstrateType)Convert.ToByte(itemValue.Value);
					return ERRCODE.NoError;
				}
				if (OSSObject.AttrNameEqual(attrName, attrSubstUsage))
				{
					if (itemValue.Format != SecsFormat.U1)
					{
						return ERRCODE.InvalidAttributeValue;
					}
					Usage = (SubstUsage)Convert.ToByte(itemValue.Value);
					return ERRCODE.NoError;
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName not find attrName={attrName}");
				return ERRCODE.ReadOnlyAttributeAccessDenied;
			}
			string text3 = (LotID = itemValue.Value as string);
			return ERRCODE.NoError;
		}

		public int SubstMoveDone(ISubstrateLocation destLoc, SubstTransportState newState)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: newState={newState}");
			SubstrateLocation substrateLocation = destLoc as SubstrateLocation;
			if (m_historyInfoList.Count > 0)
			{
				SubstHistoryInfo substHistoryInfo = m_historyInfoList[m_historyInfoList.Count - 1] as SubstHistoryInfo;
				substHistoryInfo.TimeOut = DateTime.Now;
			}
			SubstHistoryInfo substHistoryInfo2 = new SubstHistoryInfo
			{
				SubstLocID = substrateLocation.LocationID,
				TimeIn = DateTime.Now,
				TimeOut = DateTime.MinValue
			};
			m_historyInfoList.Add(substHistoryInfo2);
			
			switch (newState)
			{
			case SubstTransportState.AtDestination:
				m_fsmTransport.TriggerEvent(FSMSubstrateTransport.Event.SubstToDestination);
				break;
			case SubstTransportState.AtSource:
				m_fsmTransport.TriggerEvent(FSMSubstrateTransport.Event.SubstToSource);
				break;
			case SubstTransportState.AtWork:
				m_fsmTransport.TriggerEvent(FSMSubstrateTransport.Event.SubstToWork);
				break;
			}
			return 0;
		}

		public int SubstProcessStateChanged(SubstProcessingState state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: state={state}");
			switch (state)
			{
			case SubstProcessingState.InProcess:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.StartProcessing);
				break;
			case SubstProcessingState.Processed:
			{
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.SubstProcessed);
				ProcessJob processJob = Gem300Manager.E40.GetProcessJob(ProcessJobID);
				processJob.Substrates.Remove(this);
				break;
			}
			case SubstProcessingState.Aborted:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.SubstAborted);
				break;
			case SubstProcessingState.Lost:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.SubstLost);
				break;
			case SubstProcessingState.NeedsProcessing:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.NeedsProcessing);
				break;
			case SubstProcessingState.Rejected:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.SubstRejected);
				break;
			case SubstProcessingState.Skipped:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.SubstSkipped);
				break;
			case SubstProcessingState.Stopped:
				m_fsmProcessing.TriggerEvent(FSMSubstrateProcessing.Event.SubstStopped);
				break;
			}
			return 0;
		}

		public static void InitAttrNameList()
		{
			if (m_attrNameList != null)
			{
				return;
				//throw new Exception("attribute name list has been created. ");
			}
			m_attrNameList = new List<string>();
			m_attrNameList.Add(attrAcquiredID);
			m_attrNameList.Add(attrBatchLocID);
			m_attrNameList.Add(attrLotID);
			m_attrNameList.Add(attrMaterialStatus);
			m_attrNameList.Add(attrSubstDestination);
			m_attrNameList.Add(attrSubstHistory);
			m_attrNameList.Add(attrSubstIDStatus);
			m_attrNameList.Add(attrSubstLocID);
			m_attrNameList.Add(attrSubstPosInBatch);
			m_attrNameList.Add(attrSubstProcState);
			m_attrNameList.Add(attrSubstSource);
			m_attrNameList.Add(attrSubstState);
			m_attrNameList.Add(attrSubstType);
			m_attrNameList.Add(attrSubstUsage);
		}
	}
}
