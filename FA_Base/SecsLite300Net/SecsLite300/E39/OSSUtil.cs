using System;
using System.Collections.Generic;
using System.Reflection;
using FALogger;
using SecsLite;
using SecsLite.Model;

namespace SecsLite300.E39
{
	public static class OSSUtil
	{
		public static  string ClassName = "OSSUtil";

		public static bool GetAttrBool(List<KeyValuePair<string, SecsItem>> attrList, string key)
		{
			KeyValuePair<string, SecsItem> keyValuePair = attrList.Find((KeyValuePair<string, SecsItem> x) => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			if (keyValuePair.Key == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{key} Item Not Exist");
				throw new SecsKeyNotFoundException();
			}
			SecsItem value = keyValuePair.Value;
			if (value.Format != SecsFormat.Boolean)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{key} Item Format£º{value.Format.ToString()}, Required Format:Boolean");
				throw new SecsFormatMismatchException();
			}
			return (bool)value.Value;
		}

		public static List<SecsItem> GetAttrList(List<KeyValuePair<string, SecsItem>> attrList, string key)
		{
			KeyValuePair<string, SecsItem> keyValuePair = attrList.Find((KeyValuePair<string, SecsItem> x) => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			if (keyValuePair.Key == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{key} Item Not Exist");
				return null;
			}
			SecsItem value = keyValuePair.Value;
			if (value.Format != 0)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{key} Item Format£º{value.Format.ToString()}, Required Format:List");
				return null;
			}
			return value.ItemList;
		}

		public static byte GetAttrU1(List<KeyValuePair<string, SecsItem>> attrList, string key)
		{
			KeyValuePair<string, SecsItem> keyValuePair = attrList.Find((KeyValuePair<string, SecsItem> x) => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			if (keyValuePair.Key == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{key} Item Not Exist");
				throw new SecsKeyNotFoundException();
			}
			SecsItem value = keyValuePair.Value;
			if (value.Format != SecsFormat.U1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,{key} Item Format£º{value.Format.ToString()}, Required Format:U1");
				throw new SecsFormatMismatchException();
			}
			return (byte)value.Value;
		}

		public static string GetAttrObjID(List<KeyValuePair<string, SecsItem>> attrList)
		{
			KeyValuePair<string, SecsItem> keyValuePair = attrList.Find((KeyValuePair<string, SecsItem> x) => x.Key.ToLower() == "objid");
			if (keyValuePair.Key == null)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,objid == null");
				return string.Empty;
			}
			SecsItem value = keyValuePair.Value;
			if (value.Format != SecsFormat.Ascii)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,objid Value SecsFormat Is Not Ascii ");
				return string.Empty;
			}
			return value.Value as string;
		}
	}
}
