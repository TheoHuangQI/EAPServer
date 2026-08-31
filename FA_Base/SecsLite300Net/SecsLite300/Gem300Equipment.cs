using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using FALogger;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;
using SecsLite300.E116;
using SecsLite300.E39;
using SecsLite300.E40;
using SecsLite300.E87;
using SecsLite300.E90;
using SecsLite300.E94;

namespace SecsLite300
{
	public class Gem300Equipment : OssEquipment, IE40Impl, IE94Impl, IE87Impl, IE90Impl, IE116Impl
	{

		public virtual string ClassName => GetType().Name;

		private ManualResetEvent m_releaseEvent = new ManualResetEvent(initialState: false);

		private Thread m_jobCheckThread = null;

		private List<SubstrateLocation> m_substLocList = new List<SubstrateLocation>();

		public CarrierHoldControlMode CarrierHoldControl { get; set; }

		public int LoadPortCount => Gem300Manager.E87.LoadPortCount;

		public List<ICarrier> CarrierList => Gem300Manager.E87.CarrierList;

		public GemResult StartGEM300(HsmsConfig cfg)
		{
			Gem300Manager.Init(this);
			m_releaseEvent.Reset();
			m_jobCheckThread = new Thread(__jobCheckingThread);
			m_jobCheckThread.Start(this);
			return StartGEM(cfg);
		}

		public GemResult StopGEM300()
		{
			GemResult gemResult = StopGEM();
			if (gemResult != 0)
			{
				return gemResult;
			}
			m_releaseEvent.Set(); //停止线程
			Gem300Manager.Uninit(this);
			foreach (SubstrateLocation substLoc in m_substLocList)
			{
				substLoc.Release();
			}
			return GemResult.Success;
		}

		public override SecsErrorCode OnReceiveMessage(byte stream, byte func, SecsItem item, uint messageId, bool IsEapSend = true)
		{
			return base.OnReceiveMessage(stream, func, item, messageId, IsEapSend);
		}

		public override bool LoadInternalEC()
		{
			ECItem namedECItem = GetNamedECItem(FA300EC.CarrierHoldControl);
			if (namedECItem != null && namedECItem.DefaultValue != "")
			{
				CarrierHoldControl = (CarrierHoldControlMode)Convert.ToInt32(namedECItem.DefaultValue);
				return true;
			}
			return false;
		}

		private void checkJobs()
		{
			while (!m_releaseEvent.WaitOne(100))
			{
				lock (Gem300Manager.E94)
				{
					if (Gem300Manager.E94.SelectedControlJob == null)
					{
						checkJobs_Queued();
					}
					checkJobs_Selected();
					checkJobs_Executing();
				}
			}
		}

		private void checkJobs_Queued()
		{
			try
			{
				lock (Gem300Manager.E94.CJobList)
				{
					if (Gem300Manager.E94.CJobList == null || Gem300Manager.E94.CJobList.Count <= 0)
					{
						return;
					}
					for (int i = 0; i != Gem300Manager.E94.CJobList.Count(); i++)
					{
						ControlJob controlJob = Gem300Manager.E94.CJobList[i];
						if (controlJob == null)
						{
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, (checkJobs_Queued  headCJob is null");
							continue;
						}
						switch (OnControlJobChosen(controlJob, Gem300Manager.E94.SelectedControlJob))
						{
							case CJSelectResult.Accepted:
								FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: (checkJobs_Queued Result is Accept)");
								Gem300Manager.E94.CJobList.RemoveAt(i);
								Gem300Manager.E94.SelectedControlJob = controlJob;
								controlJob.SwitchStatusToSelected();
								FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: (SwitchStatusToSelected Success)");
								return;
							case CJSelectResult.Rejected:
								controlJob.LastSelectChoice = CJSelectResult.Rejected;
								break;
							case CJSelectResult.Deferred:
								controlJob.LastSelectChoice = CJSelectResult.Deferred;
								controlJob.LastDeferredTime = DateTime.Now;
								break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{ex.Message}\r\n{ex.StackTrace}");
			}
		}

		private void checkJobs_Selected()
		{
			try
			{
				if (Gem300Manager.E94.SelectedControlJob == null)
				{
					return;
				}
				ControlJob selectedControlJob = Gem300Manager.E94.SelectedControlJob;
				if (selectedControlJob.CurrentPRJob.Count <= 0)
				{
					return;
				}
				ProcessJob pjOb = selectedControlJob.CurrentPRJob[0] as ProcessJob;
				if (!Gem300Manager.IsPJobMaterialPresent(pjOb))
				{
					return;
				}
				if (selectedControlJob.StartMethod)
				{
					CJSelectionMode cJSelectionMode = OnControlJobProcess(selectedControlJob);
					if (cJSelectionMode == CJSelectionMode.Accepted)
					{
						selectedControlJob.SwitchStatusToExecuting();
					}
					else
					{
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: 1 Mode is {cJSelectionMode}");
					}
				}
				else
				{
					CJSelectionMode cJSelectionMode2 = OnControlJobWait(selectedControlJob);
					if (cJSelectionMode2 == CJSelectionMode.Accepted)
					{
						selectedControlJob.SwitchStatusToWaitForStart();
						//目前调度不支持，但是仍然会开始
						Thread.Sleep(100);
						CJSelectionMode cJSelectionMode = OnControlJobProcess(selectedControlJob);
						if (cJSelectionMode == CJSelectionMode.Accepted)
						{
							selectedControlJob.SwitchStatusToExecuting();
						}
					}
					else
					{
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: 2 Mode is {cJSelectionMode2}");
					}
				}
				Gem300Manager.E94.SelectedControlJob = null;
				Gem300Manager.E94.ExecutingCJobList.Add(selectedControlJob);
				Gem300Manager.EquipmentObject.PostNamedEvent(FA300Event.SelectedCjSetEmptied, null,selectedControlJob);
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{ex.Message}\r\n{ex.StackTrace}");
			}
		}

		private void checkJobs_Executing()
		{
			try
			{
				foreach (ControlJob executingCJob in Gem300Manager.E94.ExecutingCJobList)
				{
					if (executingCJob.CJobState == ControlJobState.Executing)
					{
						bool flag = true;
						for (int i = 0; i < executingCJob.CurrentPRJob.Count; i++)
						{
							ProcessJob processJob = executingCJob.CurrentPRJob[i] as ProcessJob;
							if (processJob.PRJobState != ProcessJobState.ProcessComplete && processJob.PRJobState != ProcessJobState.JobDone)
							{
								flag = false;
								string errText = "";
								if ((processJob.PRJobState == ProcessJobState.Queued || processJob.PRJobState == ProcessJobState.SettingUp) && Gem300Manager.IsPJobMaterialPresent(processJob) && OnProcessJobSetup(processJob, ref errText) == ERRCODE.NoError)
								{
									processJob.SwitchStatusToSetup();
									Thread.Sleep(500);
									processJob.PRJobSetupDone();
								}
							}
						}
						if (flag)
						{
							Thread.Sleep(1000);//延迟200ms，防止CJ Complete比 PJ Complete 先报
							executingCJob.SwitchStatusToComplete();
							OnControlJobDone(executingCJob, CJCompleteType.Completed);
						}
					}
					else
					{
						if (executingCJob.CJobState != ControlJobState.Completed)
						{
							continue;
						}
						bool flag2 = true;
						foreach (string item in executingCJob.CarrierInputSpec)
						{
							Carrier carrier = Gem300Manager.E87.FindCarrier(item);
							if (carrier != null)
							{
								flag2 = false;
								break;
							}
						}
						if (flag2)
						{
							Console.WriteLine("Material of CJ [" + executingCJob.CJID + "] has been removed. ");
							executingCJob.SwitchStatusToRemoved();
							Thread.Sleep(500);
							Gem300Manager.E94.ExecutingCJobList.Remove(executingCJob);
							OnControlJobDelete(executingCJob);
							executingCJob.Release();
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{ex.Message}\r\n{ex.StackTrace}");
			}
		}

		private static void __jobCheckingThread(object arg)
		{
			Gem300Equipment gem300Equipment = arg as Gem300Equipment;
			gem300Equipment.checkJobs();
		}

		public void InitLoadport(int lpCount)
		{
			Gem300Manager.E87.e87InitLoadport(lpCount);
		}

		public void InitSubstLocations()
		{
			string directoryName = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
			directoryName = Directory.GetParent(directoryName).FullName;
			string filename = directoryName + "\\conf\\gem300.xml";
			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.Load(filename);
				XmlNodeList xmlNodeList = xmlDocument.SelectNodes("/GEM300/SUBST_LOC/LOC");
				foreach (XmlNode item2 in xmlNodeList)
				{
					string id = SecsUtil.XmlGetNodeAttrVal(item2, "name");
					SubstrateLocation item = new SubstrateLocation(id);
					m_substLocList.Add(item);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{ex.Message}\r\n{ex.StackTrace}");
			}
		}

		public ISubstrateLocation GetSubstrateLocation(int locId)
		{
			if (locId < 0 || locId >= m_substLocList.Count)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, locId < 0 （{locId}）|| locId >= m_substLocList.Count（{m_substLocList.Count}）");
				return null;
			}
			return m_substLocList[locId];
		}

		public virtual void OnProcessJobStatusChange(IProcessJob pj)
        {

        }

		public virtual void OnControlJobStatusChange(IControlJob cj)
		{

		}

		public virtual ERRCODE OnProcessJobAbort(IProcessJob pj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual ERRCODE OnProcessJobCreate(IProcessJob pj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual ushort OnGetProcessJobSpace()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}	

		public virtual ERRCODE OnProcessJobCancel(IProcessJob pj,ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual void OnProcessJobDelete(IProcessJob pj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual ERRCODE OnProcessJobPause(IProcessJob pj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual void OnProcessJobProcessing(IProcessJob pj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual ERRCODE OnProcessJobResume(IProcessJob pj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual bool OnProcessJobStart(IProcessJob pj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return true;
		}

		public virtual ERRCODE OnProcessJobStop(IProcessJob pj,ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual ERRCODE OnProcessJobSetup(IProcessJob pj,ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual void OnProcessJobCarrierArrived(IProcessJob pj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnProcessJobWaitForStart(IProcessJob pj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual ERRCODE OnControlJobCreate(IControlJob cj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual ERRCODE OnControlJobAbort(IControlJob cj,ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual ERRCODE OnControlJobCancel(IControlJob cj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual CJSelectResult OnControlJobChosen(IControlJob job, IControlJob currentJob)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return CJSelectResult.Accepted;
		}

		public virtual int OnControlJobDelete(IControlJob cj)
		{
			Console.WriteLine("CJ [" + cj.CJID + "] deleted. ");
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnControlJobDone(IControlJob cj, CJCompleteType type)
		{
			Console.WriteLine("CJ [" + cj.CJID + "] is done. (" + type.ToString() + ")");
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual ERRCODE OnControlJobPause(IControlJob cj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual CJSelectionMode OnControlJobProcess(IControlJob cj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return CJSelectionMode.Accepted;
		}

		public virtual ERRCODE OnControlJobResume(IControlJob cj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return ERRCODE.NoError;
		}

		public virtual void OnControlJobSelected(IControlJob cj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual int OnControlJobStart(IControlJob cj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual ERRCODE OnControlJobStop(IControlJob cj, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual CJSelectionMode OnControlJobWait(IControlJob cj)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return CJSelectionMode.Accepted;
		}

		public virtual int OnCarrierTagRead(ICarrier carrier, int location, string carrierSpec, ref string data)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCarrierTagWrite(ICarrier carrier, int location, string carrierSpec, string dataSeg, string data)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual void OnIdVerifyFailed(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual CAACK OnCancelCarrier(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return CAACK.Acknowledge;
		}

		public virtual CAACK OnCarrierTagReleased(ICarrier carrier, ILocation location)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return CAACK.Acknowledge;
		}

		public virtual int OnCarrierOut(ICarrier carrier, ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCancelBind(string carrierId, ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCarrierBindOrNotify(ILoadPort loadPort, int capacity, string carrierId)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCancelCarrierNotification(string carrierId)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCancelTransfer(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual void OnCarrierActive(ICarrier carrier, OnOffState onOffState)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual int OnCarrierCreate(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCarrierDelete(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCarrierIn(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnCarrierOutEntry(ICarrier carrier, ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual void OnCarrierReCreate(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnCarrierUnlocked(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnIdVerifyOK(ICarrier carrier, string carrierId)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnLoadFromAMHS(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnLoadFromOperator(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnLoadPortActive(ILoadPort loadPort, OnOffState onOffState)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual bool OnLoadPortAccessModeChanged(ILoadPort port, LoadPortAccessMode newMode)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return false;
		}

		public virtual int OnLocationCreate(ILocation location)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnLocationDelete(ILocation location)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnPortCreate(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual int OnPortDelete(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual void OnSetReservationVisibleSignal(ILoadPort loadPort, OnOffState state)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnSlotMapVerifyFailed(ICarrier carrier)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual void OnSlotMapVerifyOK(ICarrier carrier, ref SlotState[] slotMap, int capacity, ref List<ISubstrate> substrateMapList, ref ISubstrateLocation[] substLocMap)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public virtual int OnUnloadToAMHS(ILoadPort loadPort, UnclampMethod unclampMethod)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public virtual void OnUnloadToOperator(ILoadPort loadPort)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
		}

		public List<IControlJob> GetCurrentCJobList()
		{
			List<IControlJob> list = new List<IControlJob>();
			lock (Gem300Manager.E94)
			{
				foreach (ControlJob executingCJob in Gem300Manager.E94.ExecutingCJobList)
				{
					list.Add(executingCJob);
				}
				if (Gem300Manager.E94.SelectedControlJob != null)
				{
					list.Add(Gem300Manager.E94.SelectedControlJob);
				}
				foreach (ControlJob cJob in Gem300Manager.E94.CJobList)
				{
					list.Add(cJob);
				}
			}
			return list;
		}
	
		public ILoadPort GetLoadPort(byte ptn)
		{
			return Gem300Manager.E87.GetLoadPort(ptn);
		}

		public IProcessJob GetProcessJob(string pjID)
		{
			return Gem300Manager.E40.GetProcessJob(pjID);
		}

		public ICarrier GetCarrier(string carrierId)
		{
			return Gem300Manager.E87.FindCarrier(carrierId);
		}

		public int OnEPTModuleCreate(IEPTModule module)
		{
			return 0;
		}

		public int OnEPTModuleDelete(IEPTModule module)
		{
			throw new NotImplementedException();
		}

		public void CtrlJobChoose()
		{
			Gem300Manager.E94.CtrlJobChoose();
		}

		public bool ChangeLoadPortAccessMode(List<byte> portid, LoadPortAccessMode newstate)
		{
			SecsItem itemErrList = null;
			return Gem300Manager.E87.ChangeLoadPortAccessMode(portid, newstate, ref itemErrList);
		}
	}
}
