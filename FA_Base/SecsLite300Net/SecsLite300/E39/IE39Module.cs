using System.Collections.Generic;
using SecsLite;
using SecsLite.Model;

namespace SecsLite300.E39
{
	public interface IE39Module
	{
		bool Initialize(OssEquipment equipment);

		void Uninitialize(OssEquipment equipment);

		object CreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, ref SecsItem replyItem, ref ERRCODE errCode, ref string errText);

		int ServiceRequest();

		SecsErrorCode ProcessMessage(byte stream, byte func, SecsItem item, uint msgID);
	}
}
