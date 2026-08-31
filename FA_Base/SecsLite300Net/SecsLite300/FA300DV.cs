using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;

namespace SecsLite300
{
	public static class FA300DV
	{
		public const string dvPJobID = "DV_PJOB_ID";

		public const string dvPJobState = "DV_PJOB_STATE";

		public const string dvPJobPauseEvent = "DV_PJOB_PAUSE_EVENT";

		public const string dvPJobMtlNameList = "DV_PJOB_MTL_NAME_LIST";

		public const string dvPJobMtlType = "DV_PJOB_MTL_TYPE";

		public const string dvPJobProcessStart = "DV_PJOB_PROC_START";

		public const string dvPJobPRRecipeMethod = "DV_PJOB_RECIPE_METHOD";

		public const string dvPJobRecID = "DV_PJOB_RECIPE_ID";

		public const string dvPJobRecVariableList = "DV_PJOB_RECIPE_VAR_LIST";

		public const string dvPJobPRMtlName = "DV_PJOB_MTL_NAME";

		public const string dvPJobRecipeVariable = "DV_PJOB_RECIPE_VAR";

		public const string dvPJobRecipeVarName = "DV_PJOB_RECIPE_VAR_NAME";

		public const string dvPJobRecipeVarValue = "DV_PJOB_RECIPE_VAR_VALUE";

		public const string dvCJobID = "DV_CJOB_ID";

		public const string dvCJobCarrierInputSpec = "DV_CJOB_CARRIER_INPUT_SPEC";

		public const string dvCJobCurrentPRJob = "DV_CJOB_CURRENT_PRJOB";

		public const string dvCJobDataCollectionPlan = "DV_CJOB_DATA_COLLECT_PLAN";

		public const string dvCJobMtrlOutByStatus = "DV_CJOB_MTL_OUT_BY_STATUS";

		public const string dvCJobMtrlOutSpec = "DV_CJOB_MTL_OUT_SPEC";

		public const string dvCJobPauseEvent = "DV_CJOB_PAUSE_EVENT";

		public const string dvCJobProcessingCtrlSpec = "DV_CJOB_PROC_CTRL_SPEC";

		public const string dvCJobProcessOrderMgmt = "DV_CJOB_PROC_ORDER_MGMT";

		public const string dvCJobPRJobStatusList = "DV_CJOB_PRJOB_STATUS_LIST";

		public const string dvCJobStartMethod = "DV_CJOB_START_METHOD";

		public const string dvCJobState = "DV_CJOB_STATE";

		public const string dvPortID = "DV_LOADPORT_ID";

		public const string dvPortAccessMode = "DV_LOADPORT_ACCESS_MODE";

		public const string dvPortTransferState = "DV_LOADPORT_TRANSFER_STATE";

		public const string dvPortReservationState = "DV_LOADPORT_RESERVATION_STATE";

		public const string dvSubstAcquiredID = "DV_SUBST_ACQUIRED_ID";

		public const string dvSubstBatchLocID = "DV_SUBST_BATCH_LOC_ID";

		public const string dvSubstLotID = "DV_SUBST_LOT_ID";

		public const string dvSubstMaterialStatus = "DV_SUBST_MATERIAL_STATUS";

		public const string dvSubstID = "DV_SUBST_ID";

		public const string dvSubstDestination = "DV_SUBST_DESTINATION";

		public const string dvSubstHistory = "DV_SUBST_HISTORY";

		public const string dvSubstIDStatus = "DV_SUBST_ID_STATUS";

		public const string dvSubstLocID = "DV_SUBST_LOC_ID";

		public const string dvSubstPosInBatch = "DV_SUBST_POS_IN_BATCH";

		public const string dvSubstProcState = "DV_SUBST_PROCESS_STATE";

		public const string dvSubstSource = "DV_SUBST_SOURCE";

		public const string dvSubstState = "DV_SUBST_TRANS_STATE";

		public const string dvSubstType = "DV_SUBST_TYPE";

		public const string dvSubstUsage = "DV_SUBST_USAGE";

		public const string dvSubstLocLocID = "DV_SUBSTLOC_ID";

		public const string dvSubstLocState = "DV_SUBSTLOC_STATE";

		public const string dvSubstLocSubstID = "DV_SUBSTLOC_SUBST_ID";

		public const string dvEPTState = "DV_EPT_STATE";

		public const string dvEPTPreviousState = "PreviousEPTState";

		public const string dvEPTStateTime = "DV_EPT_STATE_TIME";

		public const string dvEPTTaskName = "DV_EPT_TASK_NAME";

		public const string dvEPTTaskType = "DV_EPT_TASK_TYPE";

		public const string dvEPTPreviousTaskName = "DV_EPT_PREV_TASK_NAME";

		public const string dvEPTPreviousTaskType = "DV_EPT_PREV_TASK_TYPE";

		public const string dvEPTBlockedReason = "DV_EPT_BLOCKED_REASON";

		public const string dvEPTBlockedReasonText = "DV_EPT_BLOCKED_REASON_TEXT";

		private static Dictionary<uint, string> m_vid2nameDict;

		public static bool LoadDV(ref List<DVItem> dvdict)
		{
			if (m_vid2nameDict != null)
			{
				throw new Exception("DV was already loaded. ");
			}
			//string directoryName = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
			//directoryName = Directory.GetParent(directoryName).FullName;
			//directoryName += $"\\conf\\dv_def_300.csv";
		    string	directoryName = @"Z:Bin\Module_Gem\conf\dv_def_300.csv";
			if (!File.Exists(directoryName))
			{
				return false;
			}
			m_vid2nameDict = new Dictionary<uint, string>();
			using (StreamReader streamReader = File.OpenText(directoryName))
			{
				string empty = string.Empty;
				streamReader.ReadLine();
				while ((empty = streamReader.ReadLine()) != null)
				{
					empty = empty.Trim();
					if (empty.Length != 0 && empty[0] != '#')
					{
						string[] array = empty.Split(',');
						if (array.Length < 6)
						{
							throw new Exception("invalid DV def: " + empty + " ");
						}
						uint num = Convert.ToUInt32(array[0]);
						string text = array[1].Trim();
						m_vid2nameDict[num] = text;
						dvdict.Add(new DVItem
						{
							DVName = text,
							VID = num,
							Format= FormatParser(array[3])
						});
					}
				}
			}
			return true;
		}

		public static string GetDVName(uint vid)
		{
			if (m_vid2nameDict != null && m_vid2nameDict.ContainsKey(vid))
			{
				return m_vid2nameDict[vid];
			}
			return null;
		}

		private static SecsFormat FormatParser(string strFormat)
		{
			SecsFormat format = SecsFormat.Unknown;
			if (strFormat == "A")
			{
				format = SecsFormat.Ascii;
			}
			else if (strFormat == "L")
			{
				format = SecsFormat.List;
			}
			else if (strFormat == "B")
			{
				format = SecsFormat.Binary;
			}
			else if (strFormat == "BOOL")
            {
				format = SecsFormat.Boolean;
            }
			else
			{
				try
				{
					format = (SecsFormat)Enum.Parse(typeof(SecsFormat), strFormat);
				}
				catch (Exception)
				{
					throw new Exception($"invalid Format:{strFormat}");
				}
			}
			return format;
		}
	}
}
