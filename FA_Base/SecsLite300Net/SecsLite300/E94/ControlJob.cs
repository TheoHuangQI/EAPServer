using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;
using SecsLite300.E40;

namespace SecsLite300.E94
{
	public class ControlJob : OSSObject, IControlJob
	{
		private List<string> m_carrierList = new List<string>();

		private List<ProcessCtrl> m_processCtrlSpec = new List<ProcessCtrl>();

		private List<MtrlOutSpec> m_mtlOutSpecList = new List<MtrlOutSpec>();

		private uint[] m_pauseEventArray = null;

		public CJSelectResult LastSelectChoice = CJSelectResult.Accepted;

		public DateTime LastDeferredTime = DateTime.MinValue;

		private FSMControlJob m_fsm = null;
		private E94Manager m_mgr = null;

		private List<IProcessJob> m_currentProcessJobList = new List<IProcessJob>();

		private static readonly string attrProcessJobID = "ProcessJobID";

		private static readonly string attrCurrentPRJob = "CurrentPRJob";

		private static readonly string attrDataCollectionPlan = "DataCollectionPlan";

		private static readonly string attrCtrlJobID = "CtrlJobID";

		private static readonly string attrCarrierInputSpec = "CarrierInputSpec";

		private static readonly string attrMtrlOutSpec = "MtrlOutSpec";

		private static readonly string attrMtrlOutByStatus = "MtrlOutByStatus";

		private static readonly string attrPauseEvent = "PauseEvent";

		private static readonly string attrPRJobStatusList = "PRJobStatusList";

		private static readonly string attrProcessingCtrlSpec = "ProcessingCtrlSpec";

		private static readonly string attrProcessOrderMgmt = "ProcessOrderMgmt";

		private static readonly string attrStartMethod = "StartMethod";

		private static readonly string attrState = "CtrlJobState";

		private static readonly string attrRecid = "Recid";

		private static List<string> m_attrNameList = null;

		public string CJID => base.ObjID;

		public ControlJobState CJobState => (ControlJobState)m_fsm.CurrentState;

		public List<IProcessJob> CurrentPRJob => m_currentProcessJobList;

		public CJProcessOrderMgmt ProcessOrderMgmt { get; set; }

		public bool StartMethod { get; }

		public List<string> CarrierInputSpec => m_carrierList;

		public List<ProcessCtrl> ProcessingCtrlSpec => m_processCtrlSpec;

		public List<MtrlOutSpec> MtrlOutSpecList => m_mtlOutSpecList;

		public uint[] PauseEventArray => m_pauseEventArray;

		public string DataCollectionPlan { get; set; }


		public static string ClassName= "ControlJob";

		public ControlJob(string id, string type, bool autoStart, E94Manager mgr)
			: base(id, type)
		{
			StartMethod = autoStart;
			m_mgr = mgr;
			m_fsm = new FSMControlJob(this);
			m_fsm.Name = base.ObjID;
			m_fsm.Start();
		}

		public  void Initialize()
		{
			m_fsm.Start();
		}

		public override void Release()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.Stop();
			base.Release();
		}

		public override SecsItem GetAttribute(string attrName)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName={attrName}");
			SecsItem attribute = base.GetAttribute(attrName);
			if (attribute != null)
			{
				return attribute;
			}
			if (OSSObject.AttrNameEqual(attrName, attrCarrierInputSpec))
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				foreach (string item in CarrierInputSpec)
				{
					secsItem.AppendItem(new SecsItem(item));
				}
				return secsItem;
			}
			if (OSSObject.AttrNameEqual(attrName, attrCurrentPRJob))
			{
				SecsItem secsItem2 = new SecsItem(SecsFormat.List);
				foreach(var pj in CurrentPRJob)
                {
					secsItem2.AppendItem(new SecsItem(pj.ProcessJobID));
				}
				return secsItem2;
			}
			if (OSSObject.AttrNameEqual(attrName, attrProcessJobID))
			{
				return new SecsItem(CurrentPRJob[0].ProcessJobID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrDataCollectionPlan))
			{
				return new SecsItem(DataCollectionPlan);
			}
			if (OSSObject.AttrNameEqual(attrName, attrCtrlJobID))
			{
				return new SecsItem(base.ObjID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrMtrlOutByStatus))
			{
				return new SecsItem(SecsFormat.List);
			}
			if (OSSObject.AttrNameEqual(attrName, attrMtrlOutSpec))
			{
				return new SecsItem(SecsFormat.List);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPauseEvent))
			{
				return new SecsItem(SecsFormat.List);
			}
			if (OSSObject.AttrNameEqual(attrName, attrProcessingCtrlSpec))
			{
				return ComposeAttrItem_ProcessCtrlSpec();
			}
			if (OSSObject.AttrNameEqual(attrName, attrProcessOrderMgmt))
			{
				return new SecsItem((byte)ProcessOrderMgmt);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPRJobStatusList))
			{
				return ComposeAttrItem_PRJobStatusList();
			}
			if (OSSObject.AttrNameEqual(attrName, attrStartMethod))
			{
				return new SecsItem(StartMethod);
			}
			if (OSSObject.AttrNameEqual(attrName, attrState))
			{
				return new SecsItem((byte)CJobState);
			}
			if (OSSObject.AttrNameEqual(attrName, attrRecid))
			{
				return new SecsItem(CurrentPRJob[0].RecipeID);
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, attrName={attrName}");
			return null;
		}

		public SecsItem ComposeAttrItem_MtrlOutSpec()
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(new SecsItem("MtrlOutSpec"));
			SecsItem secsItem2 = new SecsItem(SecsFormat.List);
			foreach (MtrlOutSpec mtrlOutSpec in MtrlOutSpecList)
			{
				SecsItem secsItem3 = new SecsItem(SecsFormat.List);
				SecsItem secsItem4 = new SecsItem(SecsFormat.List);
				secsItem4.AppendItem(new SecsItem(mtrlOutSpec.SourceCarrierID));
				SecsItem secsItem5 = new SecsItem(SecsFormat.List);
				for (int i = 0; i < mtrlOutSpec.SourceSlots.Length; i++)
				{
					secsItem5.AppendItem(new SecsItem((byte)mtrlOutSpec.SourceSlots[i]));
				}
				secsItem4.AppendItem(secsItem5);
				secsItem3.AppendItem(secsItem4);
				SecsItem secsItem6 = new SecsItem(SecsFormat.List);
				secsItem4.AppendItem(new SecsItem(mtrlOutSpec.DestinationCarrierID));
				SecsItem secsItem7 = new SecsItem(SecsFormat.List);
				for (int j = 0; j < mtrlOutSpec.DestinationSlots.Length; j++)
				{
					secsItem7.AppendItem(new SecsItem((byte)mtrlOutSpec.DestinationSlots[j]));
				}
				secsItem6.AppendItem(secsItem7);
				secsItem3.AppendItem(secsItem6);
				secsItem2.AppendItem(secsItem3);
			}
			secsItem.AppendItem(secsItem2);
			return secsItem;
		}

		public SecsItem ComposeAttrItem_PRJobStatusList()
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			foreach (IProcessJob item in CurrentPRJob)
			{
				SecsItem secsItem2 = new SecsItem(SecsFormat.List);
				secsItem2.AppendItem(new SecsItem(item.ProcessJobID));
				secsItem2.AppendItem(new SecsItem((byte)item.PRJobState));
				secsItem.AppendItem(secsItem2);
			}
			return secsItem;
		}

		public SecsItem ComposeAttrItem_ProcessCtrlSpec()
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
            if (ProcessingCtrlSpec?.Count > 0)
            {
				for (int i = 0; i < ProcessingCtrlSpec.Count; i++)
				{
					ProcessCtrl processCtrl = ProcessingCtrlSpec[i];
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(new SecsItem(processCtrl.PRJobID));
					SecsItem secsItem3 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(secsItem3);
                    if (processCtrl.ControlRules?.Length > 0)
                    {
						for (int j = 0; j < processCtrl.ControlRules.Length; j++)
						{
							ControlRule controlRule = processCtrl.ControlRules[j];
							SecsItem secsItem4 = new SecsItem(SecsFormat.List);
							secsItem4.AppendItem(new SecsItem(controlRule.RuleName));
							secsItem4.AppendItem(controlRule.RuleValue as SecsItem);
							secsItem3.AppendItem(secsItem4);
						}
					}
					SecsItem item = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(item);
                    if (processCtrl.OutputRules?.Length > 0)
                    {
						for (int k = 0; k < processCtrl.OutputRules.Length; k++)
						{
							OutputRule outputRule = processCtrl.OutputRules[k];
							SecsItem secsItem5 = new SecsItem(SecsFormat.List);
							secsItem5.AppendItem(new SecsItem(outputRule.MaterialStatus));
							secsItem5.AppendItem(outputRule.RuleValue as SecsItem);
							secsItem3.AppendItem(secsItem5);
						}
					}
					secsItem.AppendItem(secsItem2);
				}
			}
			return secsItem;
		}

		public SecsItem ComposeAttrItem_PauseEvent()
		{
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			for (int i = 0; i < m_pauseEventArray.Length; i++)
			{
				secsItem.AppendItem(new SecsItem(m_pauseEventArray[i]));
			}
			return secsItem;
		}

		public void ParseAttribute_PauseEvent(List<SecsItem> itemPauseEventList)
		{
			int count = itemPauseEventList.Count;
			m_pauseEventArray = new uint[count];
            if (count >0){
				for (int i = 0; i < count; i++)
				{
					SecsItem secsItem = itemPauseEventList[i];
					try
					{
						m_pauseEventArray[i] = Convert.ToUInt32(secsItem.Value);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"invalid CEID item: PauseEvent[{i}]");
						Console.WriteLine(ex.Message);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Duplicate Control Job:Error,invalid CEID item: PauseEvent[{i}],{ex.Message}");
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Duplicate Control Job: {ex.Message} \r\n {ex.StackTrace}");
					}
				}
			}
		}

		public override ERRCODE SetAttribute(string attrName, SecsItem itemValue, ref string errText)
		{
			ERRCODE eRRCODE = base.SetAttribute(attrName, itemValue, ref errText);
			if (eRRCODE != ERRCODE.UnknownAttributeName)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: errCode={eRRCODE}");
				return eRRCODE;
			}
			string text = m_attrNameList.Find((string x) => OSSObject.AttrNameEqual(x, attrName));
			if (text == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attr= null");
				return ERRCODE.UnknownAttributeName;
			}
			if (!OSSObject.AttrNameEqual(attrName, attrDataCollectionPlan))
			{
				if (OSSObject.AttrNameEqual(attrName, attrCarrierInputSpec))
				{
					return ERRCODE.ReadOnlyAttributeAccessDenied;
				}
				if (OSSObject.AttrNameEqual(attrName, attrMtrlOutSpec))
				{
					return ERRCODE.ReadOnlyAttributeAccessDenied;
				}
				if (OSSObject.AttrNameEqual(attrName, attrMtrlOutByStatus))
				{
					return ERRCODE.ReadOnlyAttributeAccessDenied;
				}
				if (OSSObject.AttrNameEqual(attrName, attrPauseEvent))
				{
					return ERRCODE.ReadOnlyAttributeAccessDenied;
				}
				if (OSSObject.AttrNameEqual(attrName, attrProcessingCtrlSpec))
				{
					return ERRCODE.ReadOnlyAttributeAccessDenied;
				}
				if (OSSObject.AttrNameEqual(attrName, attrProcessOrderMgmt))
				{
					return ERRCODE.ReadOnlyAttributeAccessDenied;
				}
				return ERRCODE.ReadOnlyAttributeAccessDenied;
			}
			string text3 = (DataCollectionPlan = itemValue.Value as string);
			return ERRCODE.NoError;
		}

		public static void InitAttrNameList()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			if (m_attrNameList != null)
			{
				return;
				//throw new Exception("attribute name list has been created. ");
			}
			m_attrNameList = new List<string>();
			m_attrNameList.Add(attrCurrentPRJob);
			m_attrNameList.Add(attrDataCollectionPlan);
			m_attrNameList.Add(attrCarrierInputSpec);
			m_attrNameList.Add(attrMtrlOutSpec);
			m_attrNameList.Add(attrPauseEvent);
			m_attrNameList.Add(attrPRJobStatusList);
			m_attrNameList.Add(attrProcessingCtrlSpec);
			m_attrNameList.Add(attrProcessOrderMgmt);
			m_attrNameList.Add(attrStartMethod);
			m_attrNameList.Add(attrState);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		#region switch Status

		
		public void SwitchStatusToQueued()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Accepted);
		}

		public void SwitchQueuedToAbort()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Abort);
			Thread.Sleep(500);
			m_mgr.RemoveControlJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		internal void SwitchQueuedToCancel()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Cancel);
			Thread.Sleep(500);
			m_mgr.RemoveControlJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		public void SwitchQueuedToStop()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Stop);
			Thread.Sleep(500);
			m_mgr.RemoveControlJob(this);
			Release();
			m_fsm.TriggerEvent(FSMControlJob.Event.Stop);
		}

		public void SwitchStatusToSelected()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Select);
		}

		public void SwitchStatusToDeselected()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Deselect);
		}

		public void SwitchStatusToWaitForStart()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.WaitStart);
		}

		public void SwitchStatusToExecuting()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Start);
		}

		public void SwitchStatusToComplete()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.AllPJCompleted);
		}

		public void SwitchStatusToRemoved()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.CJRemove);
		}
		#endregion

		public void Stop()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Stop);
		}

		public void Resume()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Resume);
		}

		public void Pause()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Pause);
		}

		public void Abort()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Abort);
		}

		public void CJDone()
        {
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
            if (CJobState == ControlJobState.Queued)
            {
				m_fsm.TriggerEvent(FSMControlJob.Event.Cancel);
			}
			else if(CJobState == ControlJobState.Completed)
            {
				m_fsm.TriggerEvent(FSMControlJob.Event.CJRemove);
			}
            else
            {
				m_fsm.TriggerEvent(FSMControlJob.Event.Abort);
				Thread.Sleep(100);
				m_fsm.TriggerEvent(FSMControlJob.Event.CJRemove);
			}
			Thread.Sleep(500);
			m_mgr.RemoveControlJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		public void Select()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMControlJob.Event.Select);
		}
	}
}
