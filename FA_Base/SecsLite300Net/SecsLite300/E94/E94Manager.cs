#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using FALogger;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;
using SecsLite300.E39;
using SecsLite300.E40;

namespace SecsLite300.E94
{
	public class E94Manager : IE39Module
	{
		private List<ControlJob> m_cjList = new List<ControlJob>();

		private List<ControlJob> m_cjExecutingList = new List<ControlJob>();

		private IE94Impl m_impl = null;

		private Gem300Equipment m_eq = null;

		public List<ControlJob> CJobList => m_cjList;

		public List<ControlJob> ExecutingCJobList => m_cjExecutingList;

		public ControlJob SelectedControlJob { get; set; }

		public virtual string ClassName => GetType().Name;

		public bool Initialize(OssEquipment equipment)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			ControlJob.InitAttrNameList();
			if (!equipment.RegisterStreamFunctionHandler(16, 27, this))
			{
				return false;
			}
			m_eq = equipment as Gem300Equipment;
			m_impl = equipment as IE94Impl;
			lock (m_cjList)
			{
				foreach (ControlJob cj in m_cjList)
				{
					cj.Initialize();
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: End");
			return equipment.RegisterObjectTypeHandler("ControlJob", this);
		}

		public void Uninitialize(OssEquipment equipment)
		{
			lock (m_cjList)
			{
				foreach (ControlJob cj in m_cjList)
				{
					cj.Release();
				}
			}
			equipment.UnRegisterStreamFunctionHandler();
			equipment.UnRegisterObjectTypeHandler();
		}

		public int ServiceRequest()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 1;
		}

		public object CreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, ref SecsItem replyItem, ref ERRCODE errCode, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			if (objType.ToLower().Equals("controljob"))
			{
				try
				{
                    if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
                    {
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: createCJ Error,ControlState Is Not Remote");
						return null;
					}
					ControlJob controlJob = createCJ(attrList, ref errCode, ref errText);
					if (controlJob == null)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: createCJ Error,Return Null");
						return null;
					}
					ERRCODE num = m_impl.OnControlJobCreate(controlJob);
					if (num != ERRCODE.NoError)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: createCJ Error,OnControlJobCreate Return {num}");
						errCode = num;
						errText = "Invalid Parameter";
						return null;
					}
					lock (m_cjList)
					{
						m_cjList.Add(controlJob);
						controlJob.SwitchStatusToQueued();
					}
					replyItem = new SecsItem(SecsFormat.List);
					replyItem.AppendItem(new SecsItem(controlJob.CJID));
					SecsItem secsItem = new SecsItem(SecsFormat.List);
					replyItem.AppendItem(secsItem);
					SecsItem secsItem2 = new SecsItem(SecsFormat.List);
					secsItem2.AppendItem(new SecsItem("ObjID"));
					secsItem2.AppendItem(new SecsItem(controlJob.CJID));
					secsItem.AppendItem(secsItem2);
					SecsItem secsItem3 = new SecsItem(SecsFormat.List);
					secsItem3.AppendItem(new SecsItem("CarrierInputSpec"));
					SecsItem secsItem4 = new SecsItem(SecsFormat.List);
					for (int i = 0; i < controlJob.CarrierInputSpec.Count; i++)
					{
						string v = controlJob.CarrierInputSpec[i];
						secsItem4.AppendItem(new SecsItem(v));
					}
					secsItem3.AppendItem(secsItem4);
					secsItem.AppendItem(secsItem3);
					SecsItem item = controlJob.ComposeAttrItem_MtrlOutSpec();
					secsItem.AppendItem(item);
					SecsItem item2 = controlJob.ComposeAttrItem_ProcessCtrlSpec();
					secsItem.AppendItem(item2);
					SecsItem item3 = new SecsItem((byte)controlJob.ProcessOrderMgmt);
					SecsItem secsItem5 = new SecsItem(SecsFormat.List);
					secsItem5.AppendItem(new SecsItem("ProcessOrderMgmt"));
					secsItem5.AppendItem(item3);
					secsItem.AppendItem(secsItem5);
					SecsItem secsItem6 = new SecsItem(SecsFormat.List);
					secsItem6.AppendItem(new SecsItem("StartMethod"));
					secsItem6.AppendItem(new SecsItem(controlJob.StartMethod));
					secsItem.AppendItem(secsItem6);
					SecsItem secsItem7 = new SecsItem(SecsFormat.List);
					byte v2 = 0;
					secsItem7.AppendItem(new SecsItem(v2));
					secsItem7.AppendItem(new SecsItem(SecsFormat.List));
					replyItem.AppendItem(secsItem7);
					return controlJob;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{ex.Message}");
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{ex.Message}\r\n{ex.StackTrace}");
					return null;
				}
			}
			return null;
		}

		public void CtrlJobChoose()
		{
			lock (m_cjList)
			{
				foreach (ControlJob cj in m_cjList)
				{
					if (cj.LastSelectChoice == CJSelectResult.Rejected)
					{
						cj.LastSelectChoice = CJSelectResult.Accepted;
					}
				}
			}
		}

		private ControlJob createCJ(List<KeyValuePair<string, SecsItem>> attrList, ref ERRCODE errCode, ref string errText)
		{
			string attrObjID = OSSUtil.GetAttrObjID(attrList);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: CJID={attrObjID}");
			if (string.IsNullOrEmpty(attrObjID))
			{
				errCode = ERRCODE.InvalidAttributeValue;
				errText = "ControlJob ID can't be empty. ";
				Console.WriteLine(errText);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: CJID Is Empty");
				return null;
			}
			ControlJob controlJob = FindControlJob(attrObjID);
			if (controlJob != null)
			{
				errCode = ERRCODE.ObjectIdentifierInUse;
				errText = "Duplicate Control Job:" + attrObjID;
				Console.WriteLine(errText);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Duplicate Control Job:{attrObjID}");
				return null;
			}
			bool attrBool = OSSUtil.GetAttrBool(attrList, "StartMethod");
			ControlJob controlJob2 = new ControlJob(attrObjID, "ControlJob", attrBool,this);
			List<SecsItem> attrList2 = OSSUtil.GetAttrList(attrList, "ProcessingCtrlSpec");
			if (attrList2 == null)
			{
				errCode = ERRCODE.InvalidAttributeValue;
				errText = "ProcessingCtrlSpec of " + attrObjID + " not found. ";
				Console.WriteLine(errText);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
				return null;
			}
			for (int i = 0; i < attrList2.Count; i++)
			{
				SecsItem secsItem = attrList2[i];
				if (secsItem.Format != 0 || secsItem.ItemList.Count != 3)
				{
					errCode = ERRCODE.InvalidAttributeValue;
					errText = "PJSpec Format Error. ";
					Console.WriteLine(errText);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					return null;
				}
				SecsItem secsItem2 = secsItem.ItemList[0];
				if (secsItem2.Format != SecsFormat.Ascii)
				{
					errCode = ERRCODE.InvalidAttributeValue;
					errText = "PJID Item Format Error. ";
					Console.WriteLine(errText);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					return null;
				}
				string text = secsItem2.Value as string;
				IProcessJob processJob = m_eq.GetProcessJob(text);
				if (processJob == null)
				{
					errCode = ERRCODE.InvalidAttributeValue;
					errText = "Process Job [" + text + "] not found. ";
					Console.WriteLine(errText);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					return null;
				}
				ProcessJob processJob2 = processJob as ProcessJob;
				processJob2.ControlJobID = attrObjID;
				controlJob2.CurrentPRJob.Add(processJob);
				ProcessCtrl processCtrl = default(ProcessCtrl);
				processCtrl.PRJobID = text;
				ProcessCtrl item = processCtrl;
				SecsItem secsItem3 = secsItem.ItemList[1];
				if (secsItem3.Format != 0)
				{
					errCode = ERRCODE.InvalidAttributeValue;
					errText = "CtrlRuleList Item Format Error. ";
					Console.WriteLine(errText);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					return null;
				}
				item.ControlRules = new ControlRule[secsItem3.ItemList.Count];
				for (int j = 0; j < secsItem3.ItemList.Count; j++)
				{
					SecsItem secsItem4 = secsItem3.ItemList[j];
					if (secsItem4.Format != 0 || secsItem4.ItemList.Count != 2)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "CtrlRule Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					SecsItem secsItem5 = secsItem4.ItemList[0];
					if (secsItem5.Format != SecsFormat.Ascii)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "RuleName Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					string ruleName = secsItem5.Value as string;
					ControlRule controlRule = default(ControlRule);
					controlRule.RuleName = ruleName;
					controlRule.RuleValue = secsItem4.ItemList[1];
					ControlRule controlRule2 = controlRule;
					item.ControlRules[j] = controlRule2;
				}
				SecsItem secsItem6 = secsItem.ItemList[2];
				if (secsItem6.Format != 0)
				{
					errCode = ERRCODE.InvalidAttributeValue;
					errText = "OutputRuleList Item Format Error. ";
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					Console.WriteLine(errText);
					return null;
				}
				item.OutputRules = new OutputRule[secsItem6.ItemList.Count];
				for (int k = 0; k < secsItem6.ItemList.Count; k++)
				{
					SecsItem secsItem7 = secsItem6.ItemList[k];
					if (secsItem7.Format != 0 || secsItem7.ItemList.Count != 2)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "OutputRule Item Format Error. ";
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						Console.WriteLine(errText);
						return null;
					}
					SecsItem secsItem8 = secsItem7.ItemList[0];
					if (secsItem8.Format != SecsFormat.U1)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "MtlStatus Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					byte materialStatus = (byte)secsItem8.Value;
					OutputRule outputRule = default(OutputRule);
					outputRule.MaterialStatus = materialStatus;
					outputRule.RuleValue = secsItem7.ItemList[1];
					OutputRule outputRule2 = outputRule;
					item.OutputRules[k] = outputRule2;
				}
				controlJob2.ProcessingCtrlSpec.Add(item);
			}
			List<SecsItem> attrList3 = OSSUtil.GetAttrList(attrList, "CarrierInputSpec");
			for (int l = 0; l < attrList3.Count; l++)
			{
				SecsItem secsItem9 = attrList3[l];
				if (secsItem9.Format != SecsFormat.Ascii)
				{
					errCode = ERRCODE.InvalidAttributeValue;
					errText = "CID Item Format Error. ";
					Console.WriteLine(errText);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					return null;
				}
				string item2 = secsItem9.Value as string;
				controlJob2.CarrierInputSpec.Add(item2);
			}
			foreach(var carrierId in controlJob2.CarrierInputSpec)
            {
				ControlJob controlJob3 = FindControlJobByCarrierId(carrierId);
				if (controlJob3 != null)
				{
					errCode = ERRCODE.CarrierAreadyInUse;
					errText = $"Current Control Job CarrierInputSpec CarrierId: {carrierId} Have already Created CJ:{controlJob3.CJID}" ;
					Console.WriteLine(errText);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Duplicate Control Job:{attrObjID}");
					return null;
				}
			}
			controlJob2.ProcessOrderMgmt = (CJProcessOrderMgmt)OSSUtil.GetAttrU1(attrList, "ProcessOrderMgmt");
			List<SecsItem> attrList4 = OSSUtil.GetAttrList(attrList, "MtrlOutSpec");
			if (attrList4 == null)
			{
				Console.WriteLine("MtrlOutSpec of " + attrObjID + " not found. ");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:MtrlOutSpecList Item == null");
			}
			else
			{
				foreach (SecsItem item4 in attrList4)
				{
					if (item4.Format != 0 || item4.ItemList.Count != 2)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "MtrlOutSpec Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					MtrlOutSpec item3 = default(MtrlOutSpec);
					SecsItem secsItem10 = item4.ItemList[0];
					if (secsItem10.Format != 0 || secsItem10.ItemList.Count != 2)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "SrcSpec Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					SecsItem secsItem11 = secsItem10.ItemList[0];
					if (secsItem11.Format != SecsFormat.Ascii)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "Src CarrierID Item  Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					item3.SourceCarrierID = secsItem11.Value as string;
					SecsItem secsItem12 = secsItem10.ItemList[1];
					if (secsItem12.Format != 0)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "SlotArray Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					int count = secsItem12.ItemList.Count;
					item3.SourceSlots = new int[count];
					for (int m = 0; m < count; m++)
					{
						SecsItem secsItem13 = secsItem12.ItemList[m];
						if (secsItem13.Format != SecsFormat.U1)
						{
							errCode = ERRCODE.InvalidAttributeValue;
							errText = "SlotID Item Format Error. ";
							Console.WriteLine(errText);
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
							return null;
						}
						item3.SourceSlots[m] = (byte)secsItem13.Value;
					}
					SecsItem secsItem14 = item4.ItemList[1];
					if (secsItem14.Format != 0 || secsItem14.ItemList.Count != 2)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "DestSpec Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					SecsItem secsItem15 = secsItem14.ItemList[0];
					if (secsItem15.Format != SecsFormat.Ascii)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "Dest CarrierID Item format error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					item3.DestinationCarrierID = secsItem15.Value as string;
					SecsItem secsItem16 = secsItem14.ItemList[1];
					if (secsItem16.Format != 0)
					{
						errCode = ERRCODE.InvalidAttributeValue;
						errText = "Dest SlotArray Item Format Error. ";
						Console.WriteLine(errText);
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
						return null;
					}
					int count2 = secsItem16.ItemList.Count;
					item3.DestinationSlots = new int[count2];
					for (int n = 0; n < count2; n++)
					{
						SecsItem secsItem17 = secsItem16.ItemList[n];
						if (secsItem17.Format != SecsFormat.U1)
						{
							errCode = ERRCODE.InvalidAttributeValue;
							errText = "Dest SlotID Item Format Error. ";
							Console.WriteLine(errText);
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
							return null;
						}
						item3.DestinationSlots[n] = (byte)secsItem17.Value;
					}
					controlJob2.MtrlOutSpecList.Add(item3);
				}
			}
			List<SecsItem> attrList5 = OSSUtil.GetAttrList(attrList, "PauseEvent");
			if (attrList5 == null)
			{
				Console.WriteLine("PauseEvent of " + attrObjID + " not found. ");
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PauseEvent Object Item == null");
			}
			else
			{
				controlJob2.ParseAttribute_PauseEvent(attrList5);
			}
			return controlJob2;
		}

		public SecsErrorCode ProcessMessage(byte stream, byte func, SecsItem item, uint msgID)
		{
			byte b = stream;
			byte b2 = b;
			if (b2 == 16)
			{
				byte b3 = func;
				byte b4 = b3;
				if (b4 == 27)
				{
					return onControlJobCommandRequest(item, msgID);
				}
				return SecsErrorCode.ErrorFunctionType;
			}
			return SecsErrorCode.ErrorStreamType;
		}

		private SecsErrorCode onControlJobCommandRequest(SecsItem item, uint msgID)
		{
			if (item.Format != 0 || item.ItemList.Count != 3)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 3({item.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (secsItem.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,itemCJID.Format != SecsFormat.Ascii({secsItem.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = secsItem.Value as string;
			ControlJob controlJob = FindControlJob(text);
			if (controlJob == null)
			{
				SecsItem secsItem2 = new SecsItem(SecsFormat.List);
				secsItem2.AppendItem(new SecsItem(v: false));
				SecsItem secsItem3 = new SecsItem(SecsFormat.List);
				secsItem2.AppendItem(secsItem3);
				secsItem3.AppendItem(new SecsItem(2u));
				secsItem3.AppendItem(new SecsItem("control job [" + text + "] was not found."));
				m_eq.ReplyMessage(16, 28, secsItem2, msgID);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error cjOb == null");
				return SecsErrorCode.Success;
			}
			SecsItem secsItem4 = item.ItemList[1];
			if (secsItem4.Format != SecsFormat.U1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,itemCJID.Format != SecsFormat.U1({secsItem4.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			if (!Enum.IsDefined(typeof(ControlJobCommand), (byte)secsItem4.Value))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,Enum.IsDefined(typeof(ControlJobCommand), (byte)itemCMD.Value),({(byte)secsItem4.Value})");
				return SecsErrorCode.ErrorIllegalData;
			}
			ControlJobCommand controlJobCommand = (ControlJobCommand)secsItem4.Value;
			SecsItem secsItem5 = item.ItemList[2];
			if (secsItem5.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,subItem3.Format != SecsFormat.List({secsItem5.Format})||subItem3.ItemList.Count != 2({secsItem5.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			if(secsItem5.ItemList.Count == 2)
            {
				SecsItem secsItem6 = secsItem5.ItemList[0];
				SecsItem secsItem7 = secsItem5.ItemList[1];
			}
			try
			{
				ERRCODE eRRCODE = ERRCODE.NoError;
				string errText = "";
				int num = 0;
				Monitor.Enter(m_cjList);
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: cjCMD={controlJobCommand}");
				switch (controlJobCommand)
				{
					case ControlJobCommand.CJAbort:
						if (controlJob.CJobState == ControlJobState.Queued)
						{
							controlJob.SwitchQueuedToAbort();
						}
						else
						{
							eRRCODE = m_impl.OnControlJobAbort(controlJob, ref errText);
						}
						break;
					case ControlJobCommand.CJCancel:
						if (controlJob.CJobState != ControlJobState.Queued)
						{
							eRRCODE = ERRCODE.CommandNotValidForCurrentState;
							errText = $"Control job [{controlJob.CJID}] can't be Cancel because it is in {controlJob.CJobState.ToString()} state now. ";
						}
						if (eRRCODE == ERRCODE.NoError)
						{
							eRRCODE = m_impl.OnControlJobCancel(controlJob, ref errText);
							controlJob.SwitchQueuedToCancel();
						}
						break;
					case ControlJobCommand.CJDeselect:
						if (controlJob.CJobState != ControlJobState.Selected)
						{
							eRRCODE = ERRCODE.CommandNotValidForCurrentState;
							errText = $"Control job [{controlJob.CJID}] can't be Deselect because it is in {controlJob.CJobState.ToString()} state now. ";
						}
						if (eRRCODE == ERRCODE.NoError)
						{
							eRRCODE = cmdCJDeselect(controlJob, ref errText);
						}
						break;
					case ControlJobCommand.CJHOQ:
						eRRCODE = cmdCJHOQ(controlJob, ref errText);
						break;
					case ControlJobCommand.CJPause:
						if (controlJob.CJobState != ControlJobState.Executing)
						{
							eRRCODE = ERRCODE.CommandNotValidForCurrentState;
							errText = $"Control job [{controlJob.CJID}] can't be Pause because it is in {controlJob.CJobState.ToString()} state now. ";
						}
						if (eRRCODE == ERRCODE.NoError)
						{
							eRRCODE = m_impl.OnControlJobPause(controlJob, ref errText);
						}
						break;
					case ControlJobCommand.CJResume:
						if (controlJob.CJobState != ControlJobState.Paused)
						{
							eRRCODE = ERRCODE.CommandNotValidForCurrentState;
							errText = $"Control job [{controlJob.CJID}] can't be Resume because it is in {controlJob.CJobState.ToString()} state now. ";
						}
						if (eRRCODE == ERRCODE.NoError)
						{
							eRRCODE = m_impl.OnControlJobResume(controlJob, ref errText);
						}
						break;
					case ControlJobCommand.CJStart:
						controlJob.SwitchStatusToExecuting();
						num = m_impl.OnControlJobStart(controlJob);
						break;
					case ControlJobCommand.CJStop:
						if (controlJob.CJobState == ControlJobState.Queued)
						{
							controlJob.SwitchQueuedToStop();
						}
						else
						{
							eRRCODE = m_impl.OnControlJobStop(controlJob, ref errText);
						}
						break;
					default:
						eRRCODE = ERRCODE.InvalidParameter;
						errText = "Command does not exist";
						break;
				}
				Monitor.Exit(m_cjList);
				bool flag = eRRCODE == ERRCODE.NoError;
				SecsItem secsItem8 = new SecsItem(SecsFormat.List);
				secsItem8.AppendItem(new SecsItem(flag));
				SecsItem secsItem9 = new SecsItem(SecsFormat.List);
				secsItem8.AppendItem(secsItem9);
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdAck={flag}");
				if (!flag)
				{
					secsItem9.AppendItem(new SecsItem((uint)eRRCODE));
					secsItem9.AppendItem(new SecsItem(errText));
				}
				m_eq.ReplyMessage(16, 28, secsItem8, msgID);
				return SecsErrorCode.Success;
			}
			catch (Exception ex)
            {
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: cjCMD={controlJobCommand}" + ex.ToString());
				return SecsErrorCode.ErrorIllegalData;
			}
		}
		private ERRCODE cmdCJDeselect(ControlJob cj, ref string errText)
		{
			if (SelectedControlJob != cj)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SelectedControlJob: {SelectedControlJob.CJID} != cj: {cj.CJID}");
				return ERRCODE.CommandNotValidForCurrentState;
			}
			lock (m_cjList)
			{
				if (m_cjList.Count == 0)
				{
					errText = "control job queue is empty now. ";
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_cjList.Count == 0");
					return ERRCODE.Busy;
				}
				ProcessJob pjOb = m_cjList[0].CurrentPRJob[0] as ProcessJob;
				if (!Gem300Manager.IsPJobMaterialPresent(pjOb))
				{
					errText = "header control job in queue can't be selected. ";
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
					return ERRCODE.Busy;
				}
				SelectedControlJob = null;
				cj.SwitchStatusToDeselected();
				m_cjList.Add(cj);
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_cjList.Add CJ:{cj.CJID};");
			}
			return ERRCODE.NoError;
		}

		private ERRCODE cmdCJHOQ(ControlJob cj, ref string errText)
		{
			if (cj.CJobState != 0)
			{
				errText = $"CJHOQ command can't be performed to control job [{cj.CJID}], it is in {cj.CJobState} state now. ";
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {errText}");
				return ERRCODE.CommandNotValidForCurrentState;
			}
			lock (m_cjList)
			{
				if (m_cjList.Count > 1)
				{
					m_cjList.Remove(cj);
					m_cjList.Insert(0, cj);
				}
			}
			return ERRCODE.NoError;
		}

		public ControlJob FindControlJob(string cjId)
		{
			lock (m_cjList)
			{
				foreach (ControlJob cj in m_cjList)
				{
					if (cj.CJID.Equals(cjId))
					{
						return cj;
					}
				}
				if(Gem300Manager.E94.SelectedControlJob!=null && Gem300Manager.E94.SelectedControlJob.CJID.Equals(cjId))
                {
					return Gem300Manager.E94.SelectedControlJob;
				}

                if (Gem300Manager.E94.ExecutingCJobList.Count > 0)
                {
					foreach (ControlJob cj in Gem300Manager.E94.ExecutingCJobList)
					{
						if (cj.CJID.Equals(cjId))
						{
							return cj;
						}
					}
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Not Find CJ");
			return null;
		}

		public ControlJob FindControlJobByCarrierId(string carrierId)
        {
			lock (m_cjList)
			{
				foreach (ControlJob cj in m_cjList)
				{
					if (cj.CarrierInputSpec.Contains(carrierId))
					{
						return cj;
					}
				}
				if (Gem300Manager.E94.SelectedControlJob != null && Gem300Manager.E94.SelectedControlJob.CarrierInputSpec.Contains(carrierId))
				{
					return Gem300Manager.E94.SelectedControlJob;
				}

				if (Gem300Manager.E94.ExecutingCJobList.Count > 0)
				{
					foreach (ControlJob cj in Gem300Manager.E94.ExecutingCJobList)
					{
						if (cj.CarrierInputSpec.Contains(carrierId))
						{
							return cj;
						}
					}
				}
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Not Find CJ");
			return null;
		}

		public void RemoveControlJob(ControlJob cj)
		{
			lock (m_cjList)
			{
				if (m_cjList.Contains(cj))
				{
					m_cjList.Remove(cj);
				}
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: RemoveCJ {cj.CJID}");
			}
			lock (m_cjExecutingList)
			{
				if (m_cjExecutingList.Contains(cj))
				{
					m_cjExecutingList.Remove(cj);
				}
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: RemoveCJ {cj.CJID}");
			}
			if(SelectedControlJob != null)
            {
				lock (SelectedControlJob)
				{
					if (SelectedControlJob.CJID == cj.CJID)
					{
						SelectedControlJob = null;
					}
				}
			}
			m_eq.OnControlJobDelete(cj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Finished");

		}
	}
}
