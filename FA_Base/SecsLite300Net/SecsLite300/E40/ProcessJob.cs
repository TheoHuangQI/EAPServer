using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FALogger;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;
using SecsLite300.E39;
using SecsLite300.E90;

namespace SecsLite300.E40
{
	public class ProcessJob : OSSObject, IProcessJob
	{
		private List<MaterialInfo> m_prMtlNameList = null;

		private List<ISubstrate> m_substrateList = new List<ISubstrate>();

		private FSMProcessJob m_fsm = null;

		private E40Manager m_mgr = null;

		private static readonly string attrPauseEvent = "PauseEvent";

		private static readonly string attrPRJobState = "PRJobState";

		private static readonly string attrProcessJobID = "ProcessJobID";

		private static readonly string attrPRMtlNameList = "PRMtlNameList";

		private static readonly string attrPRMtlType = "PRMtlType";

		private static readonly string attrPRProcessStart = "PRProcessStart";

		private static readonly string attrPRRecipeMethod = "PRRecipeMethod";

		private static readonly string attrRecID = "RecID";

		private static readonly string attrRecVariableList = "RecVariableList";

		private static List<string> m_attrNameList = null;

		public string ProcessJobID => base.ObjID;
		public static string ClassName = "ProcessJob";
		public ProcessJobState PRJobState
		{
			get
			{
				if (m_fsm.CurrentState == null)
				{
					return ProcessJobState.NoState;
				}
				return (ProcessJobState)m_fsm.CurrentState;
			}
		}

		public ProcessJobState PRJobPreviousState { get; set; }

		public List<MaterialInfo> PRMtlNameList
		{
			get
			{
				return m_prMtlNameList;
			}
			set
			{
				m_prMtlNameList = value;
			}
		}

		public MaterialFormat PRMtlType { get; set; }

		public bool PRProcessStart { get; set; }

		public ProcessRecipeMethod PRRecipeMethod { get; set; }

		public string RecipeID { get; set; }

		public uint[] PauseEventArray { get; set; }

		public List<ISubstrate> Substrates => m_substrateList;
		
		public List<RecipeParamInfo> RecVariableList { get; set; }

		public string ControlJobID { get; set; }

		public ProcessJob(string id, E40Manager mgr)
			: base(id, "PROCESSJOB")
		{
			PRProcessStart = false;
			RecipeID = "";
			m_mgr = mgr;
			m_fsm = new FSMProcessJob(this);
			m_fsm.Name = base.ObjID;
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

		public override SecsItem GetAttribute(string attrName)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName={attrName}");
			SecsItem attribute = base.GetAttribute(attrName);
			if (attribute != null)
			{
				return attribute;
			}
			if (OSSObject.AttrNameEqual(attrName, attrPauseEvent))
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				if (PauseEventArray?.Length > 0)
				{
					for (int i = 0; i < PauseEventArray.Length; i++)
					{
						secsItem.AppendItem(new SecsItem(PauseEventArray[i]));
					}
				}
				return secsItem;
			}
			if (OSSObject.AttrNameEqual(attrName, attrPRJobState))
			{
				return new SecsItem((byte)PRJobState);
			}
			if (OSSObject.AttrNameEqual(attrName, attrProcessJobID))
			{
				return new SecsItem(base.ObjID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPRMtlNameList))
			{
				if (PRMtlType == MaterialFormat.QuantitiesInCarriers)
				{
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					if (PRMtlNameList?.Count > 0)
					{
						for (int j = 0; j < PRMtlNameList.Count; j++)
						{
							MaterialInfo materialInfo = PRMtlNameList[j];
							SecsItem secsItem3 = new SecsItem(SecsFormat.List);
							secsItem3.AppendItem(new SecsItem(materialInfo.CarrierID));
							SecsItem secsItem4 = new SecsItem(SecsFormat.List);
							for (int k = 0; k < materialInfo.SlotIDArray.Length; k++)
							{
								secsItem4.AppendItem(new SecsItem((byte)materialInfo.SlotIDArray[k]));
							}
							secsItem3.AppendItem(secsItem4);
							secsItem2.AppendItem(secsItem3);
						}
					}
					return secsItem2;
				}
				if (PRMtlType == MaterialFormat.QuantitiesInSubstrates)
				{
					SecsItem secsItem5 = new SecsItem(SecsFormat.List);
					if (PRMtlNameList?.Count > 0)
					{
						for (int l = 0; l < PRMtlNameList.Count; l++)
						{
							MaterialInfo materialInfo2 = PRMtlNameList[l];
							secsItem5.AppendItem(new SecsItem(materialInfo2.SubstrateID));
						}
					}
					return secsItem5;
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,PRMtlType={PRMtlType}");
				throw new Exception($"invalid MF type: {PRMtlType}");
			}
			if (OSSObject.AttrNameEqual(attrName, attrPRMtlType))
			{
				return SecsItem.CreateBinaryItem((byte)PRMtlType);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPRProcessStart))
			{
				return new SecsItem(PRProcessStart);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPRRecipeMethod))
			{
				return new SecsItem((byte)PRRecipeMethod);
			}
			if (OSSObject.AttrNameEqual(attrName, attrRecID))
			{
				return new SecsItem(RecipeID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrRecVariableList))
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				if (PRRecipeMethod == ProcessRecipeMethod.RecipeWithVariableTunning)
				{
					foreach (var RecVariable in RecVariableList)
					{
						SecsItem secsItem2 = new SecsItem(SecsFormat.List);
						secsItem2.AppendItem(new SecsItem(RecVariable.ParamName));
						secsItem2.AppendItem(RecVariable.ParamValue);
						secsItem.AppendItem(secsItem2);
					}
				}
				return secsItem;
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, attrName={attrName}");
			return null;
		}

		public override ERRCODE SetAttribute(string attrName, SecsItem itemValue, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:attrName={attrName}");
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
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName not find attrName={attrName}");
			return ERRCODE.ReadOnlyAttributeAccessDenied;
		}

		public static void InitAttrNameList()
		{
			if (m_attrNameList != null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_attrNameList != null");
				return;
				//throw new Exception("attribute name list has been created. ");
			}
			m_attrNameList = new List<string>();
			m_attrNameList.Add(attrPauseEvent);
			m_attrNameList.Add(attrPRJobState);
			m_attrNameList.Add(attrPRMtlNameList);
			m_attrNameList.Add(attrPRMtlType);
			m_attrNameList.Add(attrPRProcessStart);
			m_attrNameList.Add(attrPRRecipeMethod);
			m_attrNameList.Add(attrRecID);
			m_attrNameList.Add(attrRecVariableList);
		}

		


		#region Switch Status
		public void SwitchStatusToProcessing()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Start);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobProcessing, null, this);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		public void SwitchStatusToSetup()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.SettingUp);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobSetup, null, this);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		public void SwitchStatusToWaitForStart()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.WaitForStart);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		internal void SwitchQueuedToCancel()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Cancel);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobComplete, null, this);
			m_mgr.RemoveProcessJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		internal void SwitchQueuedToAbort()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Abort);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobComplete, null, this);
			m_mgr.RemoveProcessJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}

		internal void SwitchQueuedToStop()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Stop);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobComplete, null, this);
			m_mgr.RemoveProcessJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
		}
        #endregion

		public PJResultCode PRJobSetup()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			SwitchStatusToSetup();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobSetupDone()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PRProcessStart={PRProcessStart}");
			if (PRProcessStart)
			{
				Gem300Manager.EquipmentObject.OnProcessJobProcessing(this);
				SwitchStatusToProcessing();
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PRProcessStart=TRUE");
			}
			else
			{
				SwitchStatusToWaitForStart();
				Gem300Manager.EquipmentObject.OnProcessJobWaitForStart(this);
				Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobWaitingForStart, null, this);
				Gem300Manager.EquipmentObject.OnProcessJobProcessing(this);
				SwitchStatusToProcessing();
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PRProcessStart=FALSE");
			}
			return PJResultCode.Success;
		}

		public PJResultCode PRJobDone()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
            if (PRJobState == ProcessJobState.Queued)
            {
				m_fsm.TriggerEvent(FSMProcessJob.Event.Cancel);
			}
			else if(PRJobState == ProcessJobState.ProcessComplete || PRJobState == ProcessJobState.Aborted || PRJobState == ProcessJobState.Stopped)
            {
				m_fsm.TriggerEvent(FSMProcessJob.Event.JobDone);
			}
            else
            {
				m_fsm.TriggerEvent(FSMProcessJob.Event.Abort);
				Thread.Sleep(100);
				m_fsm.TriggerEvent(FSMProcessJob.Event.AbortDone);
				Thread.Sleep(100);
				m_fsm.TriggerEvent(FSMProcessJob.Event.JobDone);
			}
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobComplete,null,this);
			Thread.Sleep(500);
			m_mgr.RemoveProcessJob(this);
			Release();
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobProcessed()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.ProcessComplete);
			Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.PRJobProcessingComplete, null, this);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobAbort()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Abort);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobAbortDone()
        {
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.AbortDone);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobStop()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Stop);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobStopDone()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.StopDone);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobPause()
        {
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Pause);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobPauseDone()
        {
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.PauseDone);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}

		public PJResultCode PRJobResumed()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			m_fsm.TriggerEvent(FSMProcessJob.Event.Resume);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return PJResultCode.Success;
		}
	}
}
