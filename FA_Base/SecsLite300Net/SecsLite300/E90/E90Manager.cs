using System.Collections.Generic;
using System.Reflection;
using FALogger;
using SecsLite;
using SecsLite.Model;
using SecsLite300.E39;

namespace SecsLite300.E90
{
	public class E90Manager : IE39Module
	{
		public virtual string ClassName => GetType().Name;
		public bool Initialize(OssEquipment equipment)
		{
			Substrate.InitAttrNameList();
			SubstrateLocation.InitAttrNameList();
			return equipment.RegisterObjectTypeHandler("Substrate", this);
		}

		public bool DefineSubstrateLocation(string locID, uint vidId, uint vidLocName, uint vidSubstID, uint vidLocState, uint vidDisableEvent)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return true;
		}

		public int ServiceRequest()
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return 1;
		}

		public object CreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, ref SecsItem replyItem, ref ERRCODE errCode, ref string errText)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return null;
		}

		public SecsErrorCode ProcessMessage(byte stream, byte func, SecsItem item, uint msgID)
		{
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Start");
			return SecsErrorCode.Success;
		}

		public void Uninitialize(OssEquipment equipment)
		{
			equipment.UnRegisterObjectTypeHandler();
		}
	}
}
