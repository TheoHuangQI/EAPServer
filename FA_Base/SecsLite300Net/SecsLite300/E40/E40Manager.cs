using System;
using System.Collections.Generic;
using FALogger;
using SecsLite;
using SecsLite300.E39;
using SecsLite300.E87;
using SecsLite300.E90;
using SecsLite300.E94;
using System.Linq;
using SecsLite.Model;
using System.Reflection;
using SecsLite.Gem;

namespace SecsLite300.E40
{
	public class E40Manager : IE39Module
	{
		private Dictionary<string, ProcessJob> m_pjDict = new Dictionary<string, ProcessJob>();

		private IE40Impl m_impl = null;

		private Gem300Equipment m_eq = null;
		public virtual string ClassName => GetType().Name;

		public bool Initialize(OssEquipment baseEQ)
		{
			ProcessJob.InitAttrNameList();
			if (!baseEQ.RegisterStreamFunctionHandler(16, 3, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 5, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 8, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 10, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 11, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 15, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 17, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 19, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 21, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 23, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 25, this))
			{
				return false;
			}
			if (!baseEQ.RegisterStreamFunctionHandler(16, 29, this))
			{
				return false;
			}
			m_impl = baseEQ as IE40Impl;
			m_eq = baseEQ as Gem300Equipment;
			lock (m_pjDict)
			{
				foreach (ProcessJob value in m_pjDict.Values)
				{
					value.Initialize();
				}
			}
			return true;
		}

		public void Uninitialize(OssEquipment baseEQ)
		{
			lock (m_pjDict)
			{
				foreach (ProcessJob value in m_pjDict.Values)
				{
					value.Release();
				}
			}
			baseEQ.UnRegisterStreamFunctionHandler();
		}

		public ControlJob FindControlJob(string cjId)
        {
			if((m_eq.GetCurrentCJobList().FirstOrDefault((e => e.CJID== cjId))) != null)
            {
				return (ControlJob)m_eq.GetCurrentCJobList().FirstOrDefault(e => e.CJID == cjId);
			}
			return null;
        }

		public int ServiceRequest()
		{
			return 0;
		}

		public object CreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, ref SecsItem itemReply, ref ERRCODE errCode, ref string errText)
		{
			return null;
		}

		public SecsErrorCode ProcessMessage(byte stream, byte func, SecsItem item, uint msgID)
		{
			byte b = stream;
			byte b2 = b;
			if (b2 == 16)
			{
				return func switch
				{
					5 => onProcessJobCommandRequest(item, msgID), 
					8 => onProcessJobAlertConfirm(item, msgID), 
					10 => onProcessJobEventConfirm(item, msgID), 
					11 => onProcessJobCreateEnhRequest(item, msgID), 
					15 => onProcessJobMultiCreateRequest(item, msgID), 
					17 => onProcessJobDequeueRequest(item, msgID), 
					19 => onProcessJobGetAllRequest(item, msgID), 
					21 => onProcessJobGetSpaceRequest(item, msgID), 
					23 => onProcessJobSetRecipeVariableRequest(item, msgID), 
					25 => onProcessJobSetStartMethodRequest(item, msgID), 
					29 => onProcessJobSetMtrlOrderRequest(item, msgID), 
					_ => SecsErrorCode.ErrorFunctionType, 
				};
			}
			return SecsErrorCode.ErrorStreamType;
		}

		public void RemoveProcessJob(ProcessJob pj)
		{
			lock (m_pjDict)
			{
				if (m_pjDict.ContainsKey(pj.ProcessJobID))
				{
					m_pjDict.Remove(pj.ProcessJobID);
				}
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: RemovePJ {pj.ProcessJobID}");
			}
			m_eq.OnProcessJobDelete(pj);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Finished");
		}

		private SecsErrorCode onProcessJobCreateRequest(SecsItem item, uint msgID)
		{
			if (item.Format != 0 || item.ItemList.Count != 5)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 5({item.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (!secsItem.IsUnsignedInterger())
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemDataID is UnsignedInterger");
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(secsItem.Value);
			SecsItem secsItem2 = item.ItemList[1];
			byte singleBinaryValue = secsItem2.GetSingleBinaryValue();
			if (!Enum.IsDefined(typeof(MaterialFormat), singleBinaryValue))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: MaterialFormat={singleBinaryValue}");
				return SecsErrorCode.ErrorIllegalData;
			}
			MaterialFormat materialFormat = (MaterialFormat)singleBinaryValue;
			SecsItem secsItem3 = item.ItemList[2];
			if (secsItem3.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemMIDList.Format={secsItem3.Format}");
				return SecsErrorCode.ErrorIllegalData;
			}
			for (int i = 0; i < secsItem3.ItemList.Count; i++)
			{
				SecsItem secsItem4 = secsItem3.ItemList[i];
				if (secsItem4.Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemMID.Format ={secsItem4.Format}");
					return SecsErrorCode.ErrorIllegalData;
				}
				string text = secsItem4.Value as string;
			}
			SecsItem secsItem5 = item.ItemList[3];
			if (secsItem5.Format != 0 || secsItem5.ItemList.Count != 3)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemRecipeInfoList.Format  != SecsFormat.List({secsItem5.Format}) || pjSpec.ItemList.Count != 3({secsItem5.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem6 = secsItem5.ItemList[0];
			if (secsItem6.Format != SecsFormat.U1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPRMethod.Format!=SecsFormat.U1({secsItem6.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			if (!Enum.IsDefined(typeof(ProcessRecipeMethod), (byte)secsItem6.Value))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: ProcessRecipeMethod is undefined");
				return SecsErrorCode.ErrorIllegalData;
			}
			ProcessRecipeMethod processRecipeMethod = (ProcessRecipeMethod)secsItem6.Value;
			SecsItem secsItem7 = secsItem5.ItemList[1];
			if (secsItem7.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPRSpec.Forma!=SecsFormat.Ascii({secsItem7.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string text2 = secsItem7.Value as string;
			SecsItem secsItem8 = secsItem5.ItemList[2];
			switch (processRecipeMethod)
			{
			case ProcessRecipeMethod.RecipeOnly:
				if (secsItem8.Format != 0 || secsItem8.ItemList.Count != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemParamList.Format  != SecsFormat.List({secsItem8.Format}) || itemParamList.ItemList.Count != 0({secsItem8.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				break;
			case ProcessRecipeMethod.RecipeWithVariableTunning:
			{
				if (secsItem8.Format != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemParamList.Format!=SecsFormat.List({secsItem8.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				List<RecipeParamInfo> list = new List<RecipeParamInfo>();
				for (int j = 0; j < secsItem8.ItemList.Count; j++)
				{
					SecsItem secsItem9 = secsItem8.ItemList[j];
					if (secsItem9.Format != 0)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemParamInfo.Format !=SecsFormat.List({secsItem9.Format})");
						return SecsErrorCode.ErrorIllegalData;
					}
					string name = secsItem9.ItemList[0].Value as string;
					list.Add(new RecipeParamInfo(name, secsItem9.ItemList[1]));
				}
				break;
			}
			}
			SecsItem secsItem10 = item.ItemList[4];
			if (secsItem10.Format != SecsFormat.Boolean)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemProcessStart.Format !=SecsFormat.Boolean({secsItem10.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			bool flag = Convert.ToBoolean(secsItem10.Value);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobCommandRequest(SecsItem item, uint msgID)
		{
			if (item.Format != 0 || item.ItemList.Count != 4)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: item.Format  != SecsFormat.List({item.Format}) || item.ItemList.Count  != 4({item.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (!secsItem.IsUnsignedInterger())
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemDataID is   UnsignedInterger");
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(secsItem.Value);
			SecsItem secsItem2 = item.ItemList[1];
			if (secsItem2.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPJ.Format!=SecsFormat.Ascii({secsItem2.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string text = secsItem2.Value as string;
			ProcessJob processJob = findProcessJob(text);
			if (processJob == null)
			{
				SecsItem secsItem3 = new SecsItem(SecsFormat.List);
				secsItem3.AppendItem(new SecsItem(text));
				SecsItem secsItem4 = new SecsItem(SecsFormat.List);
				secsItem3.AppendItem(secsItem4);
				secsItem4.AppendItem(new SecsItem(v: false));
				SecsItem secsItem5 = new SecsItem(SecsFormat.List);
				secsItem4.AppendItem(secsItem5);
				SecsItem secsItem6 = new SecsItem(SecsFormat.List);
				secsItem6.AppendItem(new SecsItem(3u));
				secsItem6.AppendItem(new SecsItem("PJ object not exist"));
				secsItem5.AppendItem(secsItem6);
				m_eq.ReplyMessage(16, 6, secsItem3, msgID);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: send ReplyMessage(16, 6, itemErrReply, msgID=={msgID})");
				return SecsErrorCode.Success;
			}
			SecsItem secsItem7 = item.ItemList[2];
			if (secsItem7.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemCmdName.Format!=SecsFormat.Ascii({secsItem7.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string pjCmdName = secsItem7.Value as string;
			SecsItem secsItem8 = item.ItemList[3];
			if (secsItem8.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemCPList.Formatt!=SecsFormat.List({secsItem8.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			List<KeyValuePair<string, SecsItem>> list = new List<KeyValuePair<string, SecsItem>>();
			for (int i = 0; i < secsItem8.ItemList.Count; i++)
			{
				SecsItem secsItem9 = secsItem8.ItemList[i];
				if (secsItem9.Format != 0 || secsItem9.ItemList.Count != 2)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemParam.Format != SecsFormat.List({secsItem9.Format}) || itemParam.ItemList.Count  != 2({secsItem9.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				SecsItem secsItem10 = secsItem9.ItemList[0];
				if (secsItem10.Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemParamName.Format!=SecsFormat.Ascii({secsItem10.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				string key = secsItem10.Value as string;
				SecsItem value = secsItem9.ItemList[1];
				list.Add(new KeyValuePair<string, SecsItem>(key, value));
			}
			bool v = true;
			List<KeyValuePair<ERRCODE, string>> errList = new List<KeyValuePair<ERRCODE, string>>();
			lock (m_pjDict)
			{
				v = runPJCommand(processJob, pjCmdName, list, ref errList);
			}
			SecsItem secsItem11 = new SecsItem(SecsFormat.List);
			secsItem11.AppendItem(new SecsItem(text));
			SecsItem secsItem12 = new SecsItem(SecsFormat.List);
			secsItem11.AppendItem(secsItem12);
			secsItem12.AppendItem(new SecsItem(v));
			SecsItem secsItem13 = new SecsItem(SecsFormat.List);
			secsItem12.AppendItem(secsItem13);
			if (errList != null)
			{
				for (int j = 0; j < errList.Count; j++)
				{
					KeyValuePair<ERRCODE, string> keyValuePair = errList[j];
					SecsItem secsItem14 = new SecsItem(SecsFormat.List);
					secsItem14.AppendItem(new SecsItem((uint)keyValuePair.Key));
					secsItem14.AppendItem(new SecsItem(keyValuePair.Value));
					secsItem13.AppendItem(secsItem14);
				}
			}
			m_eq.ReplyMessage(16, 6, secsItem11, msgID);
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: send ReplyMessage(16, 6, itemS16F6, msgID={msgID})");
			return SecsErrorCode.Success;
		}

		private bool runPJCommand(IProcessJob pj, string pjCmdName, List<KeyValuePair<string, SecsItem>> paramList, ref List<KeyValuePair<ERRCODE, string>> errList)
		{
			ProcessJob processJob = pj as ProcessJob;
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Command= {pjCmdName}");
			ERRCODE errCode = ERRCODE.NoError;
			string errText = "";
			if (pjCmdName.Equals("START", StringComparison.OrdinalIgnoreCase))
			{
				processJob.SwitchStatusToProcessing();				
				return m_impl.OnProcessJobStart(pj);
			}
			
			if (pjCmdName.Equals("PAUSE", StringComparison.OrdinalIgnoreCase))
			{
				if (pj.PRJobState != ProcessJobState.Processing)
				{
					errCode = ERRCODE.CommandNotValidForCurrentState;
					errText = $"Process job [{pj.ProcessJobID}] can't be pause because it is in {pj.PRJobState.ToString()} state now. ";
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdPJResume errCode={errCode}");
				if (errCode == ERRCODE.NoError)
				{
					m_impl.OnProcessJobPause(pj, ref errText);
				}
			}
			if (pjCmdName.Equals("RESUME", StringComparison.OrdinalIgnoreCase))
			{
				if (pj.PRJobState != ProcessJobState.Paused)
				{
					errCode = ERRCODE.CommandNotValidForCurrentState;
					errText = $"Process job [{pj.ProcessJobID}] can't be Resume because it is in {pj.PRJobState.ToString()} state now. ";
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdPJResume errCode={errCode}");
				if (errCode == ERRCODE.NoError)
                {
					m_impl.OnProcessJobResume(pj, ref errText);
				}
			}
			if (pjCmdName.Equals("STOP", StringComparison.OrdinalIgnoreCase))
			{
				if (pj.PRJobState != ProcessJobState.Processing && pj.PRJobState != ProcessJobState.Paused && pj.PRJobState != ProcessJobState.Queued)
				{
					errCode = ERRCODE.CommandNotValidForCurrentState;
					errText = $"Process job [{pj.ProcessJobID}] can't be stop because it is in {pj.PRJobState.ToString()} state now. ";
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdPJStop errCode={errCode}");
				if (errCode == ERRCODE.NoError)
                {
					if (pj.PRJobState == ProcessJobState.Queued)
					{
						processJob.SwitchQueuedToStop();
					}
					else
					{
						errCode = m_impl.OnProcessJobStop(pj, ref errText);
					}
				}
			}
			if (pjCmdName.Equals("ABORT", StringComparison.OrdinalIgnoreCase))
			{
                if (pj.PRJobState != ProcessJobState.Processing && pj.PRJobState != ProcessJobState.Stopping && pj.PRJobState != ProcessJobState.Paused && pj.PRJobState != ProcessJobState.Queued)
                {
					errCode = ERRCODE.CommandNotValidForCurrentState;
					errText = $"Process job [{pj.ProcessJobID}] can't be abort because it is in {pj.PRJobState.ToString()} state now. ";
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdPJAbort errCode={errCode}");
				if (errCode == ERRCODE.NoError)
                {
					if (pj.PRJobState == ProcessJobState.Queued)
					{
						processJob.SwitchQueuedToAbort();
					}
					else
					{
						errCode = m_impl.OnProcessJobAbort(pj, ref errText);
					}
				}
			}
			if (pjCmdName.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
			{
				if (pj.PRJobState != ProcessJobState.NoState && pj.PRJobState != ProcessJobState.Queued)
				{
					errCode = ERRCODE.CommandNotValidForCurrentState;
					errText = $"Process job [{pj.ProcessJobID}] can't be cancelled because it is in {pj.PRJobState.ToString()} state now. ";
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdPJCancel errCode={errCode}");
				if (errCode == ERRCODE.NoError)
				{
					processJob.SwitchQueuedToCancel();
					errCode = m_impl.OnProcessJobCancel(pj, ref errText);
				}
			}

			if (errCode == ERRCODE.NoError)
				return true;
			else
            {
				errList.Add(new KeyValuePair<ERRCODE, string>(errCode, string.IsNullOrEmpty(errText)?pjCmdName:errText));
				return false;
			}
		}
		

		private SecsErrorCode onProcessJobAlertConfirm(SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: msgID={msgID}");
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobEventConfirm(SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: msgID={msgID}");
			return SecsErrorCode.Success;
		}

		private SecsErrorCode s16f11Parse(SecsItem item, ref ProcessJob pjOb)
		{
			if (item.Format != 0 || item.ItemList.Count != 7)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: item.Format != SecsFormat.List({item.Format})||item.ItemList.Count != 7({item.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (!secsItem.IsUnsignedInterger())
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemDataID is UnsignedInterger");
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(secsItem.Value);
			SecsItem secsItem2 = item.ItemList[1];
			if (secsItem2.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPJobID.Format != SecsFormat.Ascii({secsItem2.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			string id = secsItem2.Value as string;
			SecsItem secsItem3 = item.ItemList[2];
			byte singleBinaryValue = secsItem3.GetSingleBinaryValue();
			if (!Enum.IsDefined(typeof(MaterialFormat), singleBinaryValue))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: MaterialFormat={singleBinaryValue}");
				return SecsErrorCode.ErrorIllegalData;
			}
			MaterialFormat materialFormat = (MaterialFormat)singleBinaryValue;
			ProcessJob processJob = new ProcessJob(id, this);
			processJob.PRMtlType = materialFormat;
			SecsItem secsItem4 = item.ItemList[3];
			if (secsItem4.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemContentList.Format != SecsFormat.List({secsItem4.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			List<MaterialInfo> MTLInfoList = null;
			if (materialFormat == MaterialFormat.QuantitiesInCarriers)
			{
				SecsErrorCode secsErrorCode = parseCarrierList(secsItem4, ref MTLInfoList);
				if (secsErrorCode != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: parseCarrierList pr != SecsErrorCode.Success({secsErrorCode})");
					return secsErrorCode;
				}
			}
			else if (materialFormat == MaterialFormat.QuantitiesInWafers)
			{
				SecsErrorCode secsErrorCode2 = parseSubstrateList(secsItem4, ref MTLInfoList);
				if (secsErrorCode2 != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: parseSubstrateList  pr != SecsErrorCode.Success({secsErrorCode2})");
					return secsErrorCode2;
				}
			}
			processJob.PRMtlNameList = MTLInfoList;
			SecsItem secsItem5 = item.ItemList[4];
			if (secsItem5.Format != 0 || secsItem5.ItemList.Count != 3)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemRecipeInfo.Format != SecsFormat.List({secsItem5.Format})||itemRecipeInfo.ItemList.Count != 3({secsItem5.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem6 = secsItem5.ItemList[0];
			if (secsItem6.Format != SecsFormat.U1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPRMethod.Format != SecsFormat.U1({secsItem6.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			if (!Enum.IsDefined(typeof(ProcessRecipeMethod), (byte)secsItem6.Value))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:!Enum.IsDefined(typeof(ProcessRecipeMethod),itemPRMethod.Value{secsItem6.Value})");
				return SecsErrorCode.ErrorIllegalData;
			}
			ProcessRecipeMethod processRecipeMethod = (ProcessRecipeMethod)secsItem6.Value;
			processJob.PRRecipeMethod = processRecipeMethod;
			string text2 = (processJob.RecipeID = secsItem5.ItemList[1].Value as string);
			SecsItem secsItem7 = secsItem5.ItemList[2];
			switch (processRecipeMethod)
			{
			case ProcessRecipeMethod.RecipeOnly:
				if (secsItem7.Format != 0 || secsItem7.ItemList.Count != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemParamList.Format != SecsFormat.List({secsItem7.Format})||itemParamList.ItemList.Count != 0({secsItem7.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				break;
			case ProcessRecipeMethod.RecipeWithVariableTunning:
			{
				if (secsItem7.Format != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemParamList.Format != SecsFormat.List({secsItem7.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				List<RecipeParamInfo> list = new List<RecipeParamInfo>();
				for (int i = 0; i < secsItem7.ItemList.Count; i++)
				{
					SecsItem secsItem8 = secsItem7.ItemList[i];
					if (secsItem8.Format != 0)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemParamInfo.Format != SecsFormat.List({secsItem8.Format})");
						return SecsErrorCode.ErrorIllegalData;
					}
					string name = secsItem8.ItemList[0].Value as string;
					list.Add(new RecipeParamInfo(name, secsItem8.ItemList[1]));
				}
				processJob.RecVariableList = list;
				break;
			}
			}
			if (item.ItemList[5].Format != SecsFormat.Boolean)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:item.ItemList[5].Format != SecsFormat.Boolean({item.ItemList[5].Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			processJob.PRProcessStart = (bool)item.ItemList[5].Value;
			SecsItem secsItem9 = item.ItemList[6];
			if (secsItem9.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPauseEventList.Format != SecsFormat.List({secsItem9.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			uint[] array = new uint[secsItem9.ItemList.Count];
			for (int j = 0; j < secsItem9.ItemList.Count; j++)
			{
				if (!secsItem9.ItemList[j].IsUnsignedInterger())
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPauseEventListItemList[{j}].Format IS UnsignInterger({secsItem9.ItemList[j]})");
					return SecsErrorCode.ErrorIllegalData;
				}
				uint num2 = (array[j] = (uint)secsItem9.ItemList[j].Value);
			}
			processJob.PauseEventArray = array;
			pjOb = processJob;
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: AssociateSubstateToProcessJob(pjOb={pjOb}))");
			AssociateSubstateToProcessJob(pjOb);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobCreateEnhRequest(SecsItem item, uint msgID)
		{
			ProcessJob pjOb = null;
			SecsErrorCode secsErrorCode = s16f11Parse(item, ref pjOb);
			if (secsErrorCode != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:err != SecsErrorCode.Success({secsErrorCode})");
				return secsErrorCode;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(new SecsItem(pjOb.ProcessJobID));
			SecsItem secsItem2 = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(secsItem2);
			ERRCODE errcode = ERRCODE.NoError;
			string errText = "";
			if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
			{
				errcode = ERRCODE.CommandNotValidForCurrentState;
				secsItem2.AppendItem(new SecsItem(v: false));
				SecsItem secsItem4 = new SecsItem(SecsFormat.List);
				secsItem4.AppendItem(new SecsItem((uint)errcode));
				secsItem4.AppendItem(new SecsItem("This command cannot be executed in the local state"));
				secsItem2.AppendItem(secsItem4);
			}
            else
            {
				if (m_pjDict.ContainsKey(pjOb.ProcessJobID))
				{
					errcode = ERRCODE.ObjectIdentifierInUse;
					secsItem2.AppendItem(new SecsItem(v: false));
					SecsItem secsItem3 = new SecsItem(SecsFormat.List);
					secsItem3.AppendItem(new SecsItem((uint)errcode));
					secsItem3.AppendItem(new SecsItem("Duplicate Process Job:" + pjOb.ProcessJobID));
					secsItem2.AppendItem(secsItem3);
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_pjDict.ContainsKey({pjOb.ProcessJobID})");
				}
				else
				{
					errcode = m_impl.OnProcessJobCreate(pjOb,ref errText);
					if (errcode == ERRCODE.NoError)
					{
						secsItem2.AppendItem(new SecsItem(v: true));
						secsItem2.AppendItem(new SecsItem(SecsFormat.List));
						m_pjDict[pjOb.ProcessJobID] = pjOb;
					}
					else
					{
						secsItem2.AppendItem(new SecsItem(v: false));
						SecsItem secsItem4 = new SecsItem(SecsFormat.List);
						secsItem4.AppendItem(new SecsItem((uint)errcode));
						secsItem4.AppendItem(new SecsItem(pjOb.ProcessJobID+" "+ errText));
						secsItem2.AppendItem(secsItem4);
					}
				}
			}
			m_eq.ReplyMessage(16, 12, secsItem, msgID);
			if (errcode == ERRCODE.NoError)
			{
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: result = PJCreateResult.Success )");
				pjOb.Initialize();
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode parseCarrierList(SecsItem itemCarrierList, ref List<MaterialInfo> MTLInfoList)
		{
			if (itemCarrierList.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemCarrierList.Format != SecsFormat.List({itemCarrierList.Format}) )");
				return SecsErrorCode.ErrorIllegalData;
			}
			MTLInfoList = new List<MaterialInfo>();
			int count = itemCarrierList.ItemList.Count;
			for (int i = 0; i < count; i++)
			{
				SecsItem secsItem = itemCarrierList.ItemList[i];
				if (secsItem.Format != 0 || secsItem.ItemList.Count != 2)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemCarrierInfo.Format != SecsFormat.List({secsItem.Format}) || itemCarrierInfo.ItemList.Count != 2({secsItem.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				MaterialInfo materialInfo = new MaterialInfo();
				if (secsItem.ItemList[0].Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemCarrierInfo.ItemList[0].Format != SecsFormat.Ascii({secsItem.ItemList[0].Format}) )");
					return SecsErrorCode.ErrorIllegalData;
				}
				string text = secsItem.ItemList[0].Value as string;
				materialInfo.CarrierID = text.Trim();
				SecsItem secsItem2 = secsItem.ItemList[1];
				if (secsItem2.Format != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemSlotIDList.Format != SecsFormat.List({secsItem2.Format}) )");
					return SecsErrorCode.ErrorIllegalData;
				}
				int count2 = secsItem2.ItemList.Count;
				materialInfo.SlotIDArray = new int[count2];
				for (int j = 0; j < count2; j++)
				{
					int num = Convert.ToInt32(secsItem2.ItemList[j].Value);
					materialInfo.SlotIDArray[j] = num;
				}
				MTLInfoList.Add(materialInfo);
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode parseSubstrateList(SecsItem itemSubstrateList, ref List<MaterialInfo> mtlList)
		{
			if (itemSubstrateList.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemSubstrateList.Format != SecsFormat.List({itemSubstrateList.Format}) )");
				return SecsErrorCode.ErrorIllegalData;
			}
			mtlList = new List<MaterialInfo>();
			int count = itemSubstrateList.ItemList.Count;
			for (int i = 0; i < count; i++)
			{
				SecsItem secsItem = itemSubstrateList.ItemList[i];
				if (secsItem.Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemMID.Format != SecsFormat.Ascii({secsItem.Format}) )");
					return SecsErrorCode.ErrorIllegalData;
				}
				string substrateID = secsItem.Value as string;
				mtlList.Add(new MaterialInfo
				{
					SubstrateID = substrateID
				});
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode s16f15Parse(SecsItem item, ref List<ProcessJob> pjList)
		{
			if (item.Format != 0 || item.ItemList.Count != 2)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 2({item.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (!secsItem.IsUnsignedInterger())
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemDataID is UnsignedInterger");
				return SecsErrorCode.ErrorIllegalData;
			}
			uint num = Convert.ToUInt32(secsItem.Value);
			SecsItem secsItem2 = item.ItemList[1];
			if (secsItem2.Format != 0 || secsItem2.ItemList.Count == 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPJList.Format != SecsFormat.List ({secsItem2.Format})|| itemPJList.ItemList.Count == 0({secsItem2.ItemList.Count})");
				return SecsErrorCode.ErrorIllegalData;
			}
			if (pjList == null)
			{
				pjList = new List<ProcessJob>();
			}
			for (int i = 0; i < secsItem2.ItemList.Count; i++)
			{
				SecsItem secsItem3 = secsItem2.ItemList[i];
				if (secsItem3.Format != 0 || secsItem3.ItemList.Count != 6)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPJ.Format != SecsFormat.List ({secsItem3.Format})|| itemPJ.ItemList.Count != 6({secsItem3.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				if (secsItem3.ItemList[0].Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPJ.ItemList[0].Format != SecsFormat.Ascii");
					return SecsErrorCode.ErrorIllegalData;
				}
				string id = secsItem3.ItemList[0].Value as string;
				ProcessJob processJob = new ProcessJob(id, this);
				byte singleBinaryValue = secsItem3.ItemList[1].GetSingleBinaryValue();
				if (!Enum.IsDefined(typeof(MaterialFormat), singleBinaryValue))
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:Enum.IsDefined(typeof(MaterialFormat), bv={singleBinaryValue})");
					return SecsErrorCode.ErrorIllegalData;
				}
				processJob.PRMtlType = (MaterialFormat)singleBinaryValue;
				if (processJob.PRMtlType == MaterialFormat.QuantitiesInCarriers)
				{
					SecsItem itemCarrierList = secsItem3.ItemList[2];
					List<MaterialInfo> MTLInfoList = null;
					SecsErrorCode secsErrorCode = parseCarrierList(itemCarrierList, ref MTLInfoList);
					if (secsErrorCode != 0)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: parseCarrierList __ec != SecsErrorCode.Success({secsErrorCode})");
						return secsErrorCode;
					}
					processJob.PRMtlNameList = MTLInfoList;
				}
				else
				{
					if (processJob.PRMtlType != MaterialFormat.QuantitiesInSubstrates)
					{
						return SecsErrorCode.ErrorIllegalData;
					}
					SecsItem itemSubstrateList = secsItem3.ItemList[2];
					List<MaterialInfo> mtlList = null;
					SecsErrorCode secsErrorCode2 = parseSubstrateList(itemSubstrateList, ref mtlList);
					if (secsErrorCode2 != 0)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: parseSubstrateList __ec != SecsErrorCode.Success({secsErrorCode2})");
						return secsErrorCode2;
					}
					processJob.PRMtlNameList = mtlList;
				}
				SecsItem secsItem4 = secsItem3.ItemList[3];
				if (secsItem4.Format != 0 || secsItem4.ItemList.Count != 3)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemRecipeInfo.Format != SecsFormat.List({secsItem4.Format}) || itemRecipeInfo.ItemList.Count != 3({secsItem4.ItemList.Count})");
					return SecsErrorCode.ErrorIllegalData;
				}
				SecsItem secsItem5 = secsItem4.ItemList[0];
				if (secsItem5.Format != SecsFormat.U1)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPRMethod.Format != SecsFormat.U1({secsItem5.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				if (!Enum.IsDefined(typeof(ProcessRecipeMethod), (byte)secsItem5.Value))
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:Enum.IsDefined(typeof(ProcessRecipeMethod),itemPRMethod.Value={secsItem5.Value})");
					return SecsErrorCode.ErrorIllegalData;
				}
				processJob.PRRecipeMethod = (ProcessRecipeMethod)secsItem5.Value;
				if (secsItem4.ItemList[1].Format != SecsFormat.Ascii)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemRecipeInfo.ItemList[1].Format != SecsFormat.Ascii({secsItem4.ItemList[1].Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				string text2 = (processJob.RecipeID = secsItem4.ItemList[1].Value as string);
				SecsItem secsItem6 = secsItem4.ItemList[2];
				if (processJob.PRRecipeMethod == ProcessRecipeMethod.RecipeOnly)
				{
					if (secsItem6.Format != 0 || secsItem6.ItemList.Count != 0)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemParamList.Format != SecsFormat.List({secsItem6.Format})|| itemParamList.ItemList.Count != 0({secsItem6.ItemList.Count})");
						return SecsErrorCode.ErrorIllegalData;
					}
				}
				else if (processJob.PRRecipeMethod == ProcessRecipeMethod.RecipeWithVariableTunning)
				{
					if (secsItem6.Format != 0 || secsItem6.ItemList.Count == 0)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemParamList.Format != SecsFormat.List({secsItem6.Format})|| itemParamList.ItemList.Count == 0({secsItem6.ItemList.Count})");
						return SecsErrorCode.ErrorIllegalData;
					}
					List<RecipeParamInfo> list = new List<RecipeParamInfo>();
					for (int j = 0; j < secsItem6.ItemList.Count; j++)
					{
						SecsItem secsItem7 = secsItem6.ItemList[j];
						if (secsItem7.Format != 0 || secsItem7.ItemList.Count != 2)
						{
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemParamInfo.Format != SecsFormat.List({secsItem7.Format})|| itemParamInfo.ItemList.Count != 2({secsItem7.ItemList.Count})");
							return SecsErrorCode.ErrorIllegalData;
						}
						string name = secsItem7.ItemList[0].Value as string;
						list.Add(new RecipeParamInfo(name, secsItem7.ItemList[1]));
					}
					processJob.RecVariableList = list;
				}
				if (secsItem3.ItemList[4].Format != SecsFormat.Boolean)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:PRPROCESSSTART != SecsFormat.Boolean({secsItem3.ItemList[4].Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				processJob.PRProcessStart = (bool)secsItem3.ItemList[4].Value;
				SecsItem secsItem8 = secsItem3.ItemList[5];
				if (secsItem8.Format != 0)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:PRPAUSEEVENT != SecsFormat.List({secsItem8.Format})");
					return SecsErrorCode.ErrorIllegalData;
				}
				uint[] array = new uint[secsItem8.ItemList.Count];
				for (int k = 0; k < secsItem8.ItemList.Count; k++)
				{
					if (!secsItem8.ItemList[k].IsUnsignedInterger())
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPauseEventList.ItemList[k] is UnsignInteger ");
						return SecsErrorCode.ErrorIllegalData;
					}
					uint num2 = (array[k] = (uint)secsItem8.ItemList[k].Value);
				}
				processJob.PauseEventArray = array;
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: AssociateSubstateToProcessJob(processJobOb={processJob})");
				AssociateSubstateToProcessJob(processJob);
				pjList.Add(processJob);
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobMultiCreateRequest(SecsItem item, uint msgID)
		{
			List<ProcessJob> pjList = null;
			SecsErrorCode secsErrorCode = s16f15Parse(item, ref pjList);
			if (secsErrorCode != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error parseResult ={secsErrorCode})");
				return secsErrorCode;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			SecsItem secsItem2 = new SecsItem(SecsFormat.List);
			SecsItem secsItem3 = new SecsItem(SecsFormat.Boolean);
			secsItem3.Value = true;
			SecsItem secsItem4 = new SecsItem(SecsFormat.List);
			SecsItem secsItem5 = new SecsItem(SecsFormat.List);
			secsItem5.AppendItem(secsItem3);
			secsItem5.AppendItem(secsItem4);
			secsItem.AppendItem(secsItem2);
			secsItem.AppendItem(secsItem5);
			ERRCODE errcode = ERRCODE.NoError;
			string errText = "";
			List<ProcessJob> list = new List<ProcessJob>();
			if (m_eq.ControlState != FSMControlState.State.OnlineRemote)
			{
				errcode = ERRCODE.CommandNotValidForCurrentState;
				foreach(var pj in pjList)
                {
					secsItem2.AppendItem(new SecsItem(pj.ProcessJobID));
				}
				SecsItem secsItem6 = new SecsItem(SecsFormat.List);
				secsItem6.AppendItem(new SecsItem((uint)errcode));
				secsItem6.AppendItem(new SecsItem("This command cannot be executed in the local state"));
			}
            else
            {
				lock (m_pjDict)
				{
					for (int i = 0; i < pjList.Count; i++)
					{
						ProcessJob processJob = pjList[i];
						secsItem2.AppendItem(new SecsItem(processJob.ProcessJobID));
						if (m_pjDict.ContainsKey(processJob.ProcessJobID))
						{
							errcode = ERRCODE.ObjectIdentifierInUse;
							FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_pjDict.ContainsKey({processJob.ProcessJobID})");
							secsItem3.Value = false;
							SecsItem secsItem6 = new SecsItem(SecsFormat.List);
							secsItem6.AppendItem(new SecsItem((uint)errcode));
							secsItem6.AppendItem(new SecsItem("Duplicate Process Job:" + processJob.ProcessJobID));
							secsItem4.AppendItem(secsItem6);
						}
                        else
                        {
							var res = m_impl.OnProcessJobCreate(processJob,ref errText);
							if (res == ERRCODE.NoError)
							{
								FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: OnProcessJobCreate result = PJCreateResult.Success");
								m_pjDict[processJob.ProcessJobID] = processJob;
								list.Add(processJob);
							}
							else
							{
								FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: OnProcessJobCreate result != PJCreateResult.Success");
								secsItem3.Value = false;
								SecsItem secsItem6 = new SecsItem(SecsFormat.List);
								secsItem6.AppendItem(new SecsItem((uint)res));
								secsItem6.AppendItem(new SecsItem(processJob.ProcessJobID+ " "+ errText));
								secsItem4.AppendItem(secsItem6);
							}
						}
						
					}
				}
			}
			m_eq.ReplyMessage(16, 16, secsItem, msgID);
			foreach (ProcessJob item2 in list)
			{
				item2.Initialize();
			}
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobDequeueRequest(SecsItem item, uint msgID)
		{
			if (item == null || item.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: item = null || item.Format != SecsFormat.List");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			SecsItem secsItem2 = new SecsItem(SecsFormat.List);
			SecsItem secsItem3 = new SecsItem(SecsFormat.List);
			secsItem.AppendItem(secsItem2);
			secsItem.AppendItem(secsItem3);
			SecsItem secsItem4 = new SecsItem(v: true);
			SecsItem secsItem5 = new SecsItem(SecsFormat.List);
			secsItem3.AppendItem(secsItem4);
			secsItem3.AppendItem(secsItem5);
			lock (m_pjDict)
			{
				foreach (SecsItem item2 in item.ItemList)
				{
					string text = item2.Value as string;
					ProcessJob processJob = findProcessJob(text);
					ERRCODE errCode = ERRCODE.NoError;
					if (processJob == null)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PJ {text} IS NULL");
						errCode = ERRCODE.UnknownObjectInSpecifier;
						secsItem4.Value = false;
						SecsItem secsItem6 = new SecsItem(SecsFormat.List);
						secsItem6.AppendItem(new SecsItem((uint)errCode));
						secsItem6.AppendItem(new SecsItem(text));
						secsItem5.AppendItem(secsItem6);
					}
                    else
                    {
						if (processJob.PRJobState != ProcessJobState.NoState && processJob.PRJobState != ProcessJobState.Queued)
						{
							errCode = ERRCODE.CommandNotValidForCurrentState;
						}
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:cmdPJCancel errCode={errCode}");
						if (errCode == ERRCODE.NoError)
						{
							string errText = "";
							errCode = m_impl.OnProcessJobCancel(processJob,ref errText);
                            if (errCode == ERRCODE.NoError)
                            {
								processJob.SwitchQueuedToCancel();
								secsItem2.AppendItem(new SecsItem(text));
							}
                            else
                            {
								secsItem4.Value = false;
								SecsItem secsItem6 = new SecsItem(SecsFormat.List);
								secsItem6.AppendItem(new SecsItem((uint)errCode));
								secsItem6.AppendItem(new SecsItem(text));
								secsItem5.AppendItem(secsItem6);
							}
						}
                        else
                        {
							secsItem4.Value = false;
							SecsItem secsItem6 = new SecsItem(SecsFormat.List);
							secsItem6.AppendItem(new SecsItem((uint)errCode));
							secsItem6.AppendItem(new SecsItem(text));
							secsItem5.AppendItem(secsItem6);
						}
					}
				}
			}
			m_eq.ReplyMessage(16, 18, secsItem, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobGetAllRequest(SecsItem item, uint msgID)
		{
			if (item != null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: item==null");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = new SecsItem(SecsFormat.List);
			lock (m_pjDict)
			{
				foreach (ProcessJob value in m_pjDict.Values)
				{
					if (value.PRJobState != ProcessJobState.ProcessComplete)
					{
						SecsItem secsItem2 = new SecsItem(SecsFormat.List);
						secsItem2.AppendItem(new SecsItem(value.ObjID));
						secsItem2.AppendItem(new SecsItem((byte)value.PRJobState));
						secsItem.AppendItem(secsItem2);
					}
				}
			}
			m_eq.ReplyMessage(16, 20, secsItem, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobGetSpaceRequest(SecsItem item, uint msgID)
		{
			if (item != null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error :item = null");
				return SecsErrorCode.ErrorIllegalData;
			}
			ushort v = 50;
			if (m_pjDict != null && m_pjDict.Count <= 50)
			{
				v = Convert.ToUInt16(50 - m_pjDict.Count);
			}
			else if (m_pjDict != null && m_pjDict.Count > 50)
			{
				v = 0;
			}
			SecsItem item2 = new SecsItem(v);
			m_eq.ReplyMessage(16, 22, item2, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobSetRecipeVariableRequest(SecsItem item, uint msgID)
		{
			throw new NotImplementedException();
		}

		private SecsErrorCode onProcessJobSetStartMethodRequest(SecsItem item, uint msgID)
		{
			if (item == null || item.Format != 0 || item.ItemList.Count != 2)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:item == null || item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 2({item.ItemList.Count}) ");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem = item.ItemList[0];
			if (secsItem.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error: itemPJIDList.Format != SecsFormat.List({secsItem.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			SecsItem secsItem2 = item.ItemList[1];
			if (secsItem.Format != SecsFormat.Boolean)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPJIDList.Format != SecsFormat.Boolean({secsItem.Format})");
				return SecsErrorCode.ErrorIllegalData;
			}
			bool pRProcessStart = (bool)secsItem2.Value;
			SecsItem secsItem3 = new SecsItem(SecsFormat.List);
			SecsItem secsItem4 = new SecsItem(SecsFormat.List);
			SecsItem secsItem5 = new SecsItem(SecsFormat.List);
			secsItem3.AppendItem(secsItem4);
			secsItem3.AppendItem(secsItem5);
			SecsItem secsItem6 = new SecsItem(SecsFormat.List);
			secsItem5.AppendItem(new SecsItem(v: false));
			secsItem5.AppendItem(secsItem6);
			lock (m_pjDict)
			{
				for (int i = 0; i < secsItem.ItemList.Count; i++)
				{
					SecsItem secsItem7 = secsItem.ItemList[i];
					if (secsItem7.Format != SecsFormat.Ascii)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: error:itemPJID.Format != SecsFormat.Ascii({secsItem7.Format})");
						return SecsErrorCode.ErrorIllegalData;
					}
					string text = secsItem7.Value as string;
					if (m_pjDict.ContainsKey(text))
					{
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_pjDict ContainsKey { text}");
						m_pjDict[text].PRProcessStart = pRProcessStart;
						secsItem4.AppendItem(new SecsItem(text));
					}
					else
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_pjDict is not ContainsKey { text}");
						SecsItem secsItem8 = new SecsItem(SecsFormat.List);
						secsItem8.AppendItem(new SecsItem(3u));
						secsItem8.AppendItem(new SecsItem(text));
						secsItem6.AppendItem(secsItem8);
					}
				}
			}
			m_eq.ReplyMessage(16, 26, secsItem3, msgID);
			return SecsErrorCode.Success;
		}

		private SecsErrorCode onProcessJobSetMtrlOrderRequest(SecsItem item, uint msgID)
		{
			throw new NotImplementedException();
		}

		public ProcessJob GetProcessJob(string pjId)
		{
			return findProcessJob(pjId);
		}

		private ProcessJob findProcessJob(string pjid)
		{
			lock (m_pjDict)
			{
				if (m_pjDict.ContainsKey(pjid))
				{
					FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: m_pjDict ContainsKey { pjid}");
					return m_pjDict[pjid];
				}
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PJ is not find pjid { pjid}");
			return null;
		}

		public void AssociateSubstateToProcessJob(IProcessJob pj)
		{
			if (pj.PRMtlNameList == null) return;
			foreach (MaterialInfo mi in pj.PRMtlNameList)
			{
				Carrier carrier = Gem300Manager.E87.FindCarrier(mi.CarrierID);
				if (carrier == null)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: carrierOb is null pjid= { pj.ProcessJobID}");
					continue;
				}
				int i;
				for (i = 0; i < mi.SlotIDArray.Length; i++)
				{
					if (carrier.SubstList == null)
					{
						return;
					}
					ISubstrate substrate = carrier.SubstList.Find((ISubstrate x) => (x as Substrate).SourceSlot == mi.SlotIDArray[i] - 1);
					if (substrate == null)
					{
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: subst is null  pjid= { pj.ProcessJobID}");
						continue;
					}
					pj.Substrates.Add(substrate);
					substrate.ProcessJobID = pj.ProcessJobID;
				}
			}
		}

		public IProcessJob AssociateSubstateToProcessJob(string carrierID, int substSlotIndex, ISubstrate subst)
		{
			lock (m_pjDict)
			{
				foreach (ProcessJob value in m_pjDict.Values)
				{
					foreach (MaterialInfo pRMtlName in ((IProcessJob)value).PRMtlNameList)
					{
						if (!pRMtlName.CarrierID.Equals(carrierID))
						{
							continue;
						}
						for (int i = 0; i < pRMtlName.SlotIDArray.Length; i++)
						{
							if (pRMtlName.SlotIDArray[i] == substSlotIndex)
							{
								((IProcessJob)value).Substrates.Add(subst);
								subst.ProcessJobID = ((IProcessJob)value).ProcessJobID;
								return value;
							}
						}
					}
				}
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PJ is not not Associate with Substrate carrierID={carrierID} substSlotIndex={substSlotIndex} ");
			return null;
		}

		public IProcessJob FindProcessJobByCarrierID(string carrierId)
		{
			lock (m_pjDict)
			{
				foreach (ProcessJob value in m_pjDict.Values)
				{
					foreach (MaterialInfo pRMtlName in ((IProcessJob)value).PRMtlNameList)
					{
						if (pRMtlName.CarrierID.Equals(carrierId))
						{
							return value;
						}
					}
				}
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: PJ is not find by carrierid== { carrierId}");
			return null;
		}
	}
}
