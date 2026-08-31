using System.Collections.Generic;
using System.Reflection;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;

namespace SecsLite300.E116
{
	public class E116Manager : IE39Module
	{
		public static string ClassName = "E116Manager";
		public bool Initialize(OssEquipment equipment)
		{
			if (!Gem300Manager.EquipmentObject.RegisterObjectTypeHandler("EPTTracker", this))
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:!Gem300Manager.EquipmentObject.RegisterObjectTypeHandler('EPTTracker', this)");
				return false;
			}
			return true;
		}

		public object CreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, ref SecsItem replyItem, ref ERRCODE errCode, ref string errText)
		{
			if (objType.ToLower().Equals("epttracker"))
			{
				string attrObjID = OSSUtil.GetAttrObjID(attrList);
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:eptID={attrObjID}");
				EPTModule module = new EPTModule(attrObjID);
				Gem300Manager.EquipmentObject.OnEPTModuleCreate(module);
			}
			return null;
		}

		public SecsErrorCode ProcessMessage(byte stream, byte func, SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: msgID={msgID}");
			return SecsErrorCode.Success;
		}

		public int ServiceRequest()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 0;
		}

		public void Uninitialize(OssEquipment equipment)
		{
			equipment.UnRegisterObjectTypeHandler();
		}
	}
}
