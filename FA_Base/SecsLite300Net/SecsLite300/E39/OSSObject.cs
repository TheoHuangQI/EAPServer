using System;
using System.Collections.Generic;
using System.Reflection;
using FALogger;
using SecsLite;
using SecsLite.Model;

namespace SecsLite300.E39
{
	public class OSSObject : IDVProvider
	{
		private string m_objID = "";

		private string m_objType = "";

		private static Dictionary<string, OSSObject> m_objDict = new Dictionary<string, OSSObject>();

		private static readonly string attrNameObjID = "ObjID";

		private static readonly string attrNameObjType = "ObjType";

		public static  string ClassName = "OSSObject";

		public string ObjID
		{
			get
			{
				return m_objID;
			}
			set
			{
				m_objID = value;
			}
		}

		public string ObjType
		{
			get
			{
				return m_objType;
			}
			set
			{
				m_objType = value;
			}
		}

		public OSSObject(string id, string type)
		{
			m_objID = id;
			m_objType = type;
			lock (m_objDict)
			{
				m_objDict[id.ToLower()] = this;
			}
		}

		public virtual void Release()
		{
			lock (m_objDict)
			{
				m_objDict.Remove(m_objID.ToLower());
			}
		}

		public virtual SecsItem GetDV(uint vid)
		{
			string dVName = FA300DV.GetDVName(vid);
			if (dVName == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,vid={vid}");
				return null;
			}
			return GetAttribute(dVName);
		}

		public virtual SecsItem GetAttribute(string attrName)
		{
			if (AttrNameEqual(attrName, attrNameObjID))
			{
				return new SecsItem(m_objID);
			}
			if (AttrNameEqual(attrName, attrNameObjType))
			{
				return new SecsItem(m_objType);
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,attrName={attrName}");
			return null;
		}

		public virtual ERRCODE SetAttribute(string attrName, SecsItem itemValue, ref string errText)
		{
			if (AttrNameEqual(attrName, attrNameObjID) || AttrNameEqual(attrName, attrNameObjType))
			{
				return ERRCODE.ReadOnlyAttributeAccessDenied;
			}
			return ERRCODE.UnknownAttributeName;
		}

		public static bool AttrNameEqual(string name1, string name2)
		{
			return name1.Equals(name2, StringComparison.OrdinalIgnoreCase);
		}

		public static OSSObject FindObject(string id)
		{
			lock (m_objDict)
			{
				if (m_objDict.ContainsKey(id.ToLower()))
				{
					return m_objDict[id.ToLower()];
				}
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,ID={id}");
			return null;
		}

		public static List<OSSObject> GetObjectByType(string objType)
		{
			List<OSSObject> list = new List<OSSObject>();
			lock (m_objDict)
			{
				foreach (OSSObject value in m_objDict.Values)
				{
					if (value.ObjType.Equals(objType, StringComparison.OrdinalIgnoreCase))
					{
						list.Add(value);
					}
				}
			}
			return list;
		}
	}
}
