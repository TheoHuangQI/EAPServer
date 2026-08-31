using System;
using System.Collections.Generic;
using System.Reflection;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;

namespace SecsLite300.E90
{
	public class SubstrateLocation : OSSObject, ISubstrateLocation
	{
		private static readonly string attrSubstLocState = "SubstLocState";

		private static readonly string attrSubstID = "SubstLocStateID";

		private static readonly string attrDisableEvents = "DisableEvents";

		private static List<string> m_attrNameList = null;

		private FSMSubstrateLocation m_fsm = null;


		public string LocationID => base.ObjID;

		public string SubstID { get; set; }

		public SubstLocState State => (m_fsm == null) ? SubstLocState.Unoccupied : ((SubstLocState)m_fsm.CurrentState);

		public bool DisableEvents { get; set; }

		public virtual string ClassName => GetType().Name;

		public SubstrateLocation(string id)
			: base(id, "SubstLoc")
		{
			DisableEvents = false;
			m_fsm = new FSMSubstrateLocation(this);
			m_fsm.Name = base.ObjID + "(subst_loc)";
			m_fsm.Start();
		}

		public void Initialize()
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
			if (OSSObject.AttrNameEqual(attrName, attrSubstLocState))
			{
				return new SecsItem((byte)State);
			}
			if (OSSObject.AttrNameEqual(attrName, attrSubstID))
			{
				return new SecsItem(SubstID);
			}
			if (OSSObject.AttrNameEqual(attrName, attrDisableEvents))
			{
				return new SecsItem(DisableEvents);
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName not find attrName={attrName}");
			return null;
		}

		public override ERRCODE SetAttribute(string attrName, SecsItem itemValue, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName={attrName}");
			ERRCODE eRRCODE = base.SetAttribute(attrName, itemValue, ref errText);
			if (eRRCODE != ERRCODE.UnknownAttributeName)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error errCode={eRRCODE}");
				return eRRCODE;
			}
			string text = m_attrNameList.Find((string x) => OSSObject.AttrNameEqual(x, attrName));
			if (text == null)
			{
				return ERRCODE.UnknownAttributeName;
			}
			if (OSSObject.AttrNameEqual(attrName, attrDisableEvents))
			{
				if (itemValue.Format != SecsFormat.Boolean)
				{
					return ERRCODE.InvalidAttributeValue;
				}
				DisableEvents = (bool)itemValue.Value;
				return ERRCODE.NoError;
			}
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: attrName={attrName}");
			return ERRCODE.ReadOnlyAttributeAccessDenied;
		}

		public static void InitAttrNameList()
		{
			if (m_attrNameList != null)
			{
				return;
			}
			m_attrNameList = new List<string>();
			m_attrNameList.Add(attrDisableEvents);
			m_attrNameList.Add(attrSubstID);
			m_attrNameList.Add(attrSubstLocState);
		}
	}
}
