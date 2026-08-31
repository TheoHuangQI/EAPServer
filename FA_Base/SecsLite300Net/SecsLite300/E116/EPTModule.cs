using System;
using System.Collections.Generic;
using System.Reflection;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;

namespace SecsLite300.E116
{
	internal class EPTModule : OSSObject, IEPTModule
	{
		private static readonly string attrBlockedReason = "BlockedReason";

		private static readonly string attrBlockedReasonText = "BlockedReasonText";

		private static readonly string attrDisableEventOnTransition = "DisableEventOnTransition";

		private static readonly string attrEPTElementType = "EPTElementType";

		private static readonly string attrEPTState = "EPTState";

		private static readonly string attrEPTStateTime = "EPTStateTime";

		private static readonly string attrEPTElementName = "EPTElementName";

		private static readonly string attrPreviousEPTState = "PreviousEPTState";

		private static readonly string attrPreviousTaskName = "PreviousTaskName";

		private static readonly string attrPreviousTaskType = "PreviousTaskType";

		private static readonly string attrTaskName = "TaskName";

		private static readonly string attrTaskType = "TaskType";

		private static readonly string attrTransitionTimestamp = "TransitionTimestamp";

		private static readonly string attrTransition = "Transition";

		private static readonly string attrTrackerEventID = "TrackerEventID";

		private static List<string> m_attrNameList = null;

		private FSMEquipmentPerfTracking m_fsm = null;

		public EPTTaskType TaskType { get; set; }

		public TransitionType Transition { get; set; }

		public string TransitionTimestamp { get; set; }

		public string BlockedReasonText { get; set; }

		public BlockType BlockedReason { get; set; }

		public string PreviousTaskName { get; set; }

		public string TaskName { get; set; }

		public EPTTaskType PreviousTaskType { get; set; }

		public uint TrackerEventID { get; set; }

		public TransitionType[] DisableEventOnTransition { get; set; }

		public PerformanceTrackingState EPTState { get; set; }

		public uint EPTStateTime { get; set; }

		public PerformanceTrackingElementType EPTElementType { get; set; }

		public string EPTElementName { get; set; }

		public uint EPTStateTimeStart { get; set; }

		public PerformanceTrackingState PreviousEPTState { get; set; }
		public static string ClassName = "EPTModule";
		public EPTModule(string id)
			: base(id, "EPTTracker")
		{
			m_fsm = new FSMEquipmentPerfTracking(this);
			m_fsm.Start();
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
			m_attrNameList.Add(attrBlockedReason);
			m_attrNameList.Add(attrBlockedReasonText);
			m_attrNameList.Add(attrDisableEventOnTransition);
			m_attrNameList.Add(attrEPTElementType);
			m_attrNameList.Add(attrEPTState);
			m_attrNameList.Add(attrEPTStateTime);
			m_attrNameList.Add(attrEPTElementName);
			m_attrNameList.Add(attrPreviousEPTState);
			m_attrNameList.Add(attrPreviousTaskName);
			m_attrNameList.Add(attrPreviousTaskType);
			m_attrNameList.Add(attrTaskName);
			m_attrNameList.Add(attrTaskType);
			m_attrNameList.Add(attrTransitionTimestamp);
			m_attrNameList.Add(attrTransition);
			m_attrNameList.Add(attrTrackerEventID);
		}

		public override SecsItem GetAttribute(string attrName)
		{
			SecsItem attribute = base.GetAttribute(attrName);
			if (attribute != null)
			{
				return attribute;
			}
			if (OSSObject.AttrNameEqual(attrName, attrBlockedReason))
			{
				return new SecsItem((byte)BlockedReason);
			}
			if (OSSObject.AttrNameEqual(attrName, attrBlockedReasonText))
			{
				return new SecsItem(BlockedReasonText);
			}
			if (OSSObject.AttrNameEqual(attrName, attrEPTElementType))
			{
				return new SecsItem((byte)EPTElementType);
			}
			if (OSSObject.AttrNameEqual(attrName, attrEPTState))
			{
				return new SecsItem((byte)EPTState);
			}
			if (OSSObject.AttrNameEqual(attrName, attrEPTStateTime))
			{
				return new SecsItem(EPTStateTime);
			}
			if (OSSObject.AttrNameEqual(attrName, attrEPTElementName))
			{
				return new SecsItem(EPTElementName);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPreviousEPTState))
			{
				return new SecsItem((byte)PreviousEPTState);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPreviousTaskName))
			{
				return new SecsItem(PreviousTaskName);
			}
			if (OSSObject.AttrNameEqual(attrName, attrPreviousTaskType))
			{
				return new SecsItem((byte)PreviousTaskType);
			}
			if (OSSObject.AttrNameEqual(attrName, attrTaskName))
			{
				return new SecsItem(TaskName);
			}
			if (OSSObject.AttrNameEqual(attrName, attrTaskType))
			{
				return new SecsItem((byte)TaskType);
			}
			if (OSSObject.AttrNameEqual(attrName, attrTransitionTimestamp))
			{
				return new SecsItem(TransitionTimestamp);
			}
			if (OSSObject.AttrNameEqual(attrName, attrTransition))
			{
				return new SecsItem((byte)Transition);
			}
			if (OSSObject.AttrNameEqual(attrName, attrTrackerEventID))
			{
				return new SecsItem(TrackerEventID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrDisableEventOnTransition))
			{
				SecsItem secsItem = new SecsItem(SecsFormat.List);
				if (DisableEventOnTransition != null && DisableEventOnTransition.Length>0)
				{
					for (int i = 0; i < DisableEventOnTransition.Length; i++)
					{
						secsItem.AppendItem(new SecsItem((byte)DisableEventOnTransition[i]));
					}
				}
				return secsItem;
			}
			return null;
		}

		public override ERRCODE SetAttribute(string attrName, SecsItem itemValue, ref string errText)
		{
			ERRCODE eRRCODE = base.SetAttribute(attrName, itemValue, ref errText);
			if (eRRCODE != ERRCODE.UnknownAttributeName)
			{
				return eRRCODE;
			}
			string text = m_attrNameList.Find((string x) => OSSObject.AttrNameEqual(x, attrName));
			if (text == null)
			{
				return ERRCODE.UnknownAttributeName;
			}
			if (OSSObject.AttrNameEqual(attrName, attrDisableEventOnTransition))
			{
				if (itemValue.Format != 0 || itemValue.ItemList.Count > 9)
				{
					return ERRCODE.InvalidParameter;
				}
				TransitionType[] array = new TransitionType[itemValue.ItemList.Count];
				for (int i = 0; i < itemValue.ItemList.Count; i++)
				{
					byte b = Convert.ToByte(itemValue.ItemList[i].Value);
					if (b < 1 || b > 9)
					{
						return ERRCODE.InvalidParameter;
					}
					array[i] = (TransitionType)b;
				}
				DisableEventOnTransition = array;
				return ERRCODE.NoError;
			}
			return ERRCODE.ReadOnlyAttributeAccessDenied;
		}

		public int SetBlocked(BlockType type, string desc)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public int SetBusy(EPTTaskType taskType, string taskName)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public int SetIdle()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}
	}
}
