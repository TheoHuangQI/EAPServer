using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FALogger;
using SecsLite;
using SecsLite.Gem;
using SecsLite.Model;
using SecsLite300.E94;

namespace SecsLite300.E39
{
    public class OssEquipment : GemEquipment
    {
        private Dictionary<string, IE39Module> m_objTypeDict = new Dictionary<string, IE39Module>();

        private Dictionary<ushort, IE39Module> m_sfHandlerDict = new Dictionary<ushort, IE39Module>();

        public virtual string ClassName => GetType().Name;

        private bool e39ProcessMessage(byte stream, byte func, SecsItem item, uint messageId, ref SecsErrorCode errCode)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Receive stream={stream},func={func}");
            byte b = stream;
            byte b2 = b;
            if (b2 == 14)
            {
                switch (func)
                {
                    case 1:
                        errCode = onGetAttrRequest(item, messageId);
                        break;
                    case 3:
                        errCode = onSetAttrRequest(item, messageId);
                        break;
                    case 5:
                        errCode = onGetTypeRequest(item, messageId);
                        break;
                    case 7:
                        errCode = onGetAttrNameRequest(item, messageId);
                        break;
                    case 9:
                        errCode = onCreateObjectRequest(item, messageId);
                        break;
                    case 11:
                        errCode = onDeleteObjectRequest(item, messageId);
                        break;
                    case 13:
                        errCode = onObjectAttachRequest(item, messageId);
                        break;
                    case 15:
                        errCode = onAttachedObjectActionRequest(item, messageId);
                        break;
                    case 17:
                        errCode = onSupervisedObjectActionRequest(item, messageId);
                        break;
                    case 19:
                        errCode = onGenericServiceRequest(item, messageId);
                        break;
                    case 23:
                        errCode = SecsErrorCode.ErrorFunctionType;
                        break;
                    case 25:
                        errCode = onGetServiceNameRequest(item, messageId);
                        break;
                    case 27:
                        errCode = onGetServiceParameterNameRequest(item, messageId);
                        break;
                    default:
                        errCode = SecsErrorCode.ErrorFunctionType;
                        break;
                }
                return true;
            }
            return false;
        }

        public override SecsErrorCode OnReceiveMessage(byte stream, byte func, SecsItem item, uint messageId, bool IsEapSend = true)
        {
            SecsErrorCode secsErrorCode = base.OnReceiveMessage(stream, func, item, messageId, IsEapSend);
            if (secsErrorCode != SecsErrorCode.ErrorStreamType && secsErrorCode != SecsErrorCode.ErrorFunctionType)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error,retCode={secsErrorCode}");
                return secsErrorCode;
            }
            secsErrorCode = SecsErrorCode.Success;
            try
            {
                bool res = e39ProcessMessage(stream, func, item, messageId, ref secsErrorCode);
                if (res)
                {
                    return secsErrorCode;
                }
            }
            catch (Exception ex)
            {
                secsErrorCode = SecsErrorCode.ErrorIllegalData;
                Console.WriteLine($"S{stream},F{func} process error, e39ProcessMessage");
                Console.WriteLine(ex.Message);
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: S{stream},F{func} process error,e39ProcessMessage");
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {ex.Message}\r\n{ex.StackTrace}");
                return secsErrorCode;
            }
            ushort num = makeSFKey(stream, func);
            if (!m_sfHandlerDict.ContainsKey(num))
            {
                switch (stream)
                {
                    case 3:
                        switch (func)
                        {

                            case 17:
                                break;
                            case 27:
                                break;
                            case 29:
                                break;
                            default:
                                return SecsErrorCode.ErrorFunctionType;
                        }
                        break;
                    case 16:
                        switch (func)
                        {
                            case 11:
                                break;
                            case 15:
                                break;
                            case 19:
                                break;
                            case 21:
                                break;
                            case 27:
                                break;
                            default:
                                return SecsErrorCode.ErrorFunctionType;
                        }
                        break;
                    default:
                        return SecsErrorCode.ErrorStreamType;
                }

            }
            IE39Module iE39Module = m_sfHandlerDict[num];
            try
            {
                secsErrorCode = iE39Module.ProcessMessage(stream, func, item, messageId);
            }
            catch (Exception ex)
            {
                secsErrorCode = SecsErrorCode.ErrorIllegalData;
                Console.WriteLine($"S{stream},F{func} process error, {iE39Module.ToString()}");
                Console.WriteLine(ex.Message);
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: S{stream},F{func} process error,{iE39Module}");
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {ex.Message}\r\n{ex.StackTrace}");
            }
            return secsErrorCode;
        }

        protected bool LoadModule(IE39Module module)
        {
            return module.Initialize(this);
        }

        private SecsErrorCode onGetAttrRequest(SecsItem item, uint messageId)
        {
            if (item.Format != 0 || item.ItemList.Count != 5)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            string objSpec = item.ItemList[0].Value as string;
            if (item.ItemList[1].Format != SecsFormat.Ascii)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            string objType = item.ItemList[1].Value as string;
            if (item.ItemList[2].Format != 0)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            List<string> list = new List<string>();
            for (int i = 0; i < item.ItemList[2].ItemList.Count; i++)
            {
                SecsItem secsItem = item.ItemList[2].ItemList[i];
                if (secsItem.Format != SecsFormat.Ascii)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                list.Add(secsItem.Value as string);
            }
            if (item.ItemList[2].ItemList.Count == 0 && item.ItemList[1].Value.ToString().ToUpper() == "CONTROLJOB")
            {
                List<IControlJob> currentCJobList = Gem300Manager.EquipmentObject.GetCurrentCJobList();
                for (int j = 0; j != currentCJobList.Count(); j++)
                {
                    list.Add(currentCJobList[j].CJID);
                }
            }
            SecsItem secsItem2 = item.ItemList[3];
            if (secsItem2.Format != 0)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            List<GetAttrFilter> list2 = new List<GetAttrFilter>();
            for (int k = 0; k < secsItem2.ItemList.Count; k++)
            {
                SecsItem secsItem3 = secsItem2.ItemList[k];
                if (secsItem3.Format != 0 || secsItem3.ItemList.Count != 3)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                SecsItem secsItem4 = secsItem3.ItemList[0];
                if (secsItem4.Format != SecsFormat.Ascii)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                string attrId = secsItem4.Value as string;
                SecsItem attrData = secsItem3.ItemList[1];
                SecsItem secsItem5 = secsItem3.ItemList[2];
                if (secsItem5.Format != SecsFormat.U1)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                ATTRRELN relation = (ATTRRELN)secsItem5.Value;
                list2.Add(new GetAttrFilter
                {
                    AttrId = attrId,
                    Relation = relation,
                    AttrData = attrData
                });
            }
            SecsItem secsItem6 = item.ItemList[4];
            if (secsItem6.Format != 0)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            List<string> list3 = new List<string>();
            for (int l = 0; l < secsItem6.ItemList.Count; l++)
            {
                SecsItem secsItem7 = secsItem6.ItemList[l];
                if (secsItem7.Format != SecsFormat.Ascii)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                string item2 = secsItem7.Value as string;
                list3.Add(item2);
            }
            if (list3.Count() == 0)
            {
                list3.Add("ObjType");
                list3.Add("ObjID");
                list3.Add("CarrierInputSpec");
                list3.Add("CurrentPRJob");
                list3.Add("DataCollectionPlan");
                list3.Add("MtrlOutByStatus");
                list3.Add("MtrlOutSpec");
                list3.Add("PauseEvent");
                list3.Add("PRJobStatusList");
                list3.Add("ProcessingCtrlSpec");
                list3.Add("ProcessOrderMgmt");
                list3.Add("StartMethod");
                list3.Add("CtrlJobState");
            }
            SecsItem item3 = doGetObjectAttr(objSpec, objType, list, list2, list3);
            ReplyMessage(14, 2, item3, messageId);
            return SecsErrorCode.Success;
        }

        private SecsItem doGetObjectAttr(string objSpec, string objType, List<string> objIDList, List<GetAttrFilter> attrFilterList, List<string> attrIdList)
        {
            SecsItem secsItem = new SecsItem(SecsFormat.List);
            SecsItem secsItem2 = new SecsItem(SecsFormat.List);
            SecsItem secsItem3 = new SecsItem(SecsFormat.List);
            SecsItem secsItem4 = new SecsItem(SecsFormat.U1);
            secsItem4.Value = (byte)0;
            SecsItem secsItem5 = new SecsItem(SecsFormat.List);
            secsItem3.AppendItem(secsItem4);
            secsItem3.AppendItem(secsItem5);
            secsItem.AppendItem(secsItem2);
            secsItem.AppendItem(secsItem3);
            if (objIDList.Count == 0 || attrIdList.Count == 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ReError, {secsItem4.Value},objIDList.Count == 0({objIDList.Count}) || attrIdList.Count == 0({attrIdList.Count})");
                return secsItem;
            }
            for (int i = 0; i < objIDList.Count; i++)
            {
                string text = objIDList[i];
                OSSObject oSSObject = OSSObject.FindObject(text);
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: theOb={oSSObject == null}");
                if (oSSObject == null)
                {
                    secsItem4.Value = (byte)1;
                    SecsItem secsItem6 = new SecsItem(SecsFormat.List);
                    secsItem6.AppendItem(new SecsItem(3u));
                    secsItem6.AppendItem(new SecsItem("object [" + text + "] does not exist. "));
                    secsItem5.AppendItem(secsItem6);
                    continue;
                }
                SecsItem secsItem7 = new SecsItem(SecsFormat.List);
                secsItem7.AppendItem(new SecsItem(text));
                SecsItem secsItem8 = new SecsItem(SecsFormat.List);
                secsItem7.AppendItem(secsItem8);
                bool flag = true;
                for (int j = 0; j < attrIdList.Count; j++)
                {
                    string text2 = attrIdList[j];
                    SecsItem secsItem9 = new SecsItem(SecsFormat.List);
                    secsItem9.AppendItem(new SecsItem(text2));
                    SecsItem attribute = oSSObject.GetAttribute(text2);
                    if (attribute == null)
                    {
                        SecsItem secsItem10 = new SecsItem(SecsFormat.List);
                        secsItem10.AppendItem(new SecsItem(4u));
                        secsItem10.AppendItem(new SecsItem("attribute [" + text2 + "] of object [" + text + "] does not exist. "));
                        secsItem5.AppendItem(secsItem10);
                        flag = false;
                        break;
                    }
                    secsItem9.AppendItem(attribute);
                    secsItem8.AppendItem(secsItem9);
                }
                if (flag)
                {
                    secsItem2.AppendItem(secsItem7);
                }
            }
            return secsItem;
        }

        private SecsItem doSetObjectAttr(string objSpec, string objType, List<string> objIDList, List<SetAttrData> attrSetList)
        {
            SecsItem secsItem = new SecsItem(SecsFormat.List);
            SecsItem secsItem2 = new SecsItem(SecsFormat.List);
            SecsItem secsItem3 = new SecsItem(SecsFormat.List);
            SecsItem secsItem4 = new SecsItem(SecsFormat.U1);
            secsItem4.Value = (byte)0;
            SecsItem item = new SecsItem(SecsFormat.List);
            secsItem3.AppendItem(secsItem4);
            secsItem3.AppendItem(item);
            secsItem.AppendItem(secsItem2);
            secsItem.AppendItem(secsItem3);
            if (objIDList == null)
            {
                List<OSSObject> objectByType = OSSObject.GetObjectByType(objType);
                foreach (OSSObject item3 in objectByType)
                {
                    SecsItem secsItem5 = new SecsItem(SecsFormat.List);
                    secsItem5.AppendItem(new SecsItem(item3.ObjID));
                    SecsItem item2 = new SecsItem(SecsFormat.List);
                    secsItem5.AppendItem(item2);
                    for (int i = 0; i < attrSetList.Count; i++)
                    {
                        SetAttrData setAttrData = attrSetList[i];
                    }
                    secsItem2.AppendItem(secsItem5);
                }
            }
            return secsItem;
        }

        private SecsErrorCode onSetAttrRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 4)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List || item.ItemList.Count != 4({item.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objSpec = item.ItemList[0].Value as string;
            if (item.ItemList[1].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[1].Format != SecsFormat.Ascii({item.ItemList[1].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objType = item.ItemList[1].Value as string;
            if (item.ItemList[2].Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[2].Format != SecsFormat.List({item.ItemList[2].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<string> list = new List<string>();
            for (int i = 0; i < item.ItemList[2].ItemList.Count; i++)
            {
                SecsItem secsItem = item.ItemList[2].ItemList[i];
                if (secsItem.Format != SecsFormat.Ascii)
                {
                    FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},itemObjID.Format != SecsFormat.Ascii({secsItem.Format})");
                    return SecsErrorCode.ErrorIllegalData;
                }
                list.Add(secsItem.Value as string);
            }
            if (item.ItemList[3].Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[3].Format != SecsFormat.List({item.ItemList[3].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<SetAttrData> list2 = new List<SetAttrData>();
            List<SecsItem> itemList = item.ItemList[3].ItemList;
            for (int j = 0; j < itemList.Count; j++)
            {
                SecsItem secsItem2 = itemList[j];
                if (secsItem2.Format != 0 || secsItem2.ItemList.Count != 2)
                {
                    FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},attr.Format != SecsFormat.List({secsItem2.Format}) || attr.ItemList.Count != 2({secsItem2.ItemList.Count})");
                    return SecsErrorCode.ErrorIllegalData;
                }
                string attrId = secsItem2.ItemList[0].Value as string;
                SecsItem attrData = secsItem2.ItemList[1];
                list2.Add(new SetAttrData
                {
                    AttrId = attrId,
                    AttrData = attrData
                });
            }
            SecsItem item2 = doSetObjectAttr(objSpec, objType, list, list2);
            ReplyMessage(14, 4, item2, messageId);
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onGetTypeRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.Ascii({item.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text = item.ItemList[0].Value as string;
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onGetAttrNameRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 2)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 2({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objSpec = item.ItemList[0].Value as string;
            SecsItem secsItem = item.ItemList[1];
            if (secsItem.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemTypeArray.Format != SecsFormat.List({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<string> list = new List<string>();
            for (int i = 0; i < secsItem.ItemList.Count; i++)
            {
                SecsItem secsItem2 = secsItem.ItemList[i];
                if (secsItem2.Format != SecsFormat.Ascii)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                list.Add(secsItem2.Value as string);
            }
            SecsItem item3 = doGetAttrName(objSpec, list);
            ReplyMessage(14, 8, item3, messageId);
            return SecsErrorCode.Success;
        }

        private SecsItem doGetAttrName(string objSpec, List<string> objTypeList)
        {
            SecsItem secsItem = new SecsItem(SecsFormat.List);
            SecsItem secsItem2 = new SecsItem(SecsFormat.List);
            SecsItem secsItem3 = new SecsItem(SecsFormat.List);
            SecsItem secsItem4 = new SecsItem(SecsFormat.U1);
            secsItem4.Value = (byte)0;
            SecsItem secsItem5 = new SecsItem(SecsFormat.List);
            secsItem3.AppendItem(secsItem4);
            secsItem3.AppendItem(secsItem5);
            secsItem.AppendItem(secsItem2);
            secsItem.AppendItem(secsItem3);
            if (objTypeList.Count == 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ReError, {secsItem4.Value},objTypeList.Count == 0({objTypeList.Count})");
                return secsItem;
            }
            for (int i = 0; i < objTypeList.Count; i++)
            {
                string text = objTypeList[i];
                SecsItem secsItem7 = new SecsItem(SecsFormat.List);
                secsItem7.AppendItem(new SecsItem(text));
                SecsItem secsItem8 = new SecsItem(SecsFormat.List);
                secsItem7.AppendItem(secsItem8);
                if (text.ToUpper() == "CONTROLJOB")
                {
                    List<IControlJob> currentCJobList = Gem300Manager.EquipmentObject.GetCurrentCJobList();
                    for (int j = 0; j != currentCJobList.Count(); j++)
                    {
                        secsItem8.AppendItem(new SecsItem(currentCJobList[j].CJID));
                    }
                }
                secsItem2.AppendItem(secsItem7);
            }
            return secsItem;
        }

        private SecsErrorCode onCreateObjectRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 3)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 3({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objSpec = item.ItemList[0].Value as string;
            if (item.ItemList[1].Format != SecsFormat.Ascii && !item.ItemList[1].IsUnsignedInterger())
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[1].Format != SecsFormat.Ascii({item.ItemList[1].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objType = item.ItemList[1].Value as string;
            SecsItem secsItem = item.ItemList[2];
            if (secsItem.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},attrArray.Format != SecsFormat.List({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<KeyValuePair<string, SecsItem>> list = parseAttrList(secsItem);
            if (list == null || list.Count == 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},attrList == null");
                return SecsErrorCode.ErrorIllegalData;
            }
            Task.Run(() => {
                SecsItem item2 = doCreateObject(objType, objSpec, list, secsItem);
                if (item2 != null)
                {
                    ReplyMessage(14, 10, item2, messageId);
                }
            });
            return SecsErrorCode.Success;
        }

        private List<KeyValuePair<string, SecsItem>> parseAttrList(SecsItem itemAttrList)
        {
            List<KeyValuePair<string, SecsItem>> list = new List<KeyValuePair<string, SecsItem>>();
            for (int i = 0; i < itemAttrList.ItemList.Count; i++)
            {
                SecsItem secsItem = itemAttrList.ItemList[i];
                if (secsItem.Format != 0 || secsItem.ItemList.Count != 2)
                {
                    return null;
                }
                string key = secsItem.ItemList[0].Value as string;
                SecsItem value = secsItem.ItemList[1];
                list.Add(new KeyValuePair<string, SecsItem>(key, value));
            }
            return list;
        }

        private SecsItem doCreateObject(string objType, string objSpec, List<KeyValuePair<string, SecsItem>> attrList, SecsItem item)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:objType={objType},objSpec={objSpec}");
            SecsItem replyItem = null;
            string text = objType.ToLower();
            IE39Module iE39Module = m_objTypeDict[text];
            object obj = null;
            ERRCODE errCode = ERRCODE.NoError;
            string errText = string.Empty;
            if (m_objTypeDict.ContainsKey(text))
            {
                obj = iE39Module.CreateObject(objType, objSpec, attrList, ref replyItem, ref errCode, ref errText);
                if (errCode == 0 && replyItem?.ItemList?.Count > 1)
                {
                    replyItem.ItemList[1] = item;
                }
            }
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:obj={obj == null}");
            if (obj == null && errCode != ERRCODE.VerificationTimeOut)  //14 代表请求超时，这个时候不必再回S14F10)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},obj == null,Key={text}");
                replyItem = new SecsItem(SecsFormat.List);
                replyItem.AppendItem(new SecsItem(""));
                replyItem.AppendItem(new SecsItem(SecsFormat.List));
                SecsItem secsItem = new SecsItem(SecsFormat.List);
                byte v = 1;
                secsItem.AppendItem(new SecsItem(v));
                SecsItem secsItem2 = new SecsItem(SecsFormat.List);
                SecsItem secsItem3 = new SecsItem(SecsFormat.List);
                secsItem3.AppendItem(new SecsItem((uint)errCode));
                secsItem3.AppendItem(new SecsItem(errText));
                secsItem2.AppendItem(secsItem3);
                secsItem.AppendItem(secsItem2);
                replyItem.AppendItem(secsItem);
                return replyItem;
            }
            return replyItem;
        }

        private SecsErrorCode onDeleteObjectRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 2)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 2({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objSpec = item.ItemList[0].Value as string;
            SecsItem secsItem = item.ItemList[1];
            if (secsItem.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemAttrList.Format != SecsFormat.List({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<KeyValuePair<string, SecsItem>> list = parseAttrList(secsItem);
            if (list == null)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},attrList == null");
                return SecsErrorCode.ErrorIllegalData;
            }
            SecsItem item2 = doDeleteObject(objSpec, list);
            ReplyMessage(14, 12, item2, messageId);
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: ReplyMessage messageId={messageId}");
            return SecsErrorCode.Success;
        }

        private SecsItem doDeleteObject(string objSpec, List<KeyValuePair<string, SecsItem>> attrList)
        {
            return null;
        }

        private SecsErrorCode onObjectAttachRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 2)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 2({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text = item.ItemList[0].Value as string;
            SecsItem secsItem = item.ItemList[1];
            if (secsItem.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Error, {SecsErrorCode.ErrorIllegalData},attrArray.Format != SecsFormat.List({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            Dictionary<string, SecsItem> dictionary = new Dictionary<string, SecsItem>();
            for (int i = 0; i < secsItem.ItemList.Count; i++)
            {
                SecsItem secsItem2 = secsItem.ItemList[i];
                if (secsItem2.Format != 0 || secsItem2.ItemList.Count != 2)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                string key = secsItem2.ItemList[0].Value as string;
                SecsItem secsItem4 = (dictionary[key] = secsItem2.ItemList[1]);
            }
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onAttachedObjectActionRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 4)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format})||item.ItemList.Count != 4({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text = item.ItemList[0].Value as string;
            if (item.ItemList[1].Format != SecsFormat.U1)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[1].Format != SecsFormat.U1({item.ItemList[1].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            byte b = (byte)item.ItemList[1].Value;
            if (!Enum.IsDefined(typeof(ObjectCommand), b))
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},Enum.IsDefined(typeof(ObjectCommand), v)({b})");
                return SecsErrorCode.ErrorIllegalData;
            }
            ObjectCommand objectCommand = (ObjectCommand)b;
            if (item.ItemList[2].Format != SecsFormat.U4)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[2].Format != SecsFormat.U4({item.ItemList[2].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            uint num = (uint)item.ItemList[2].Value;
            SecsItem secsItem = item.ItemList[3];
            if (secsItem.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},attrArray.Format != SecsFormat.List({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            Dictionary<string, SecsItem> dictionary = new Dictionary<string, SecsItem>();
            for (int i = 0; i < secsItem.ItemList.Count; i++)
            {
                SecsItem secsItem2 = secsItem.ItemList[i];
                if (secsItem2.Format != 0 || secsItem2.ItemList.Count != 2)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                string key = secsItem2.ItemList[0].Value as string;
                SecsItem secsItem4 = (dictionary[key] = secsItem2.ItemList[1]);
            }
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onSupervisedObjectActionRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 4)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format})|| item.ItemList.Count != 4({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            if (item.ItemList[0].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[0].Format != SecsFormat.Ascii({item.ItemList[0].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text = item.ItemList[0].Value as string;
            if (item.ItemList[1].Format != SecsFormat.U1)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[1].Format != SecsFormat.U1({item.ItemList[1].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            byte b = (byte)item.ItemList[1].Value;
            if (!Enum.IsDefined(typeof(ObjectCommand), b))
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},Enum.IsDefined(typeof(ObjectCommand), v)({b})");
                return SecsErrorCode.ErrorIllegalData;
            }
            ObjectCommand objectCommand = (ObjectCommand)b;
            if (item.ItemList[2].Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.ItemList[2].Format != SecsFormat.Ascii({item.ItemList[2].Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text2 = item.ItemList[2].Value as string;
            SecsItem secsItem = item.ItemList[3];
            if (secsItem.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},attrArray.Format != SecsFormat.List({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            Dictionary<string, SecsItem> dictionary = new Dictionary<string, SecsItem>();
            for (int i = 0; i < secsItem.ItemList.Count; i++)
            {
                SecsItem secsItem2 = secsItem.ItemList[i];
                if (secsItem2.Format != 0 || secsItem2.ItemList.Count != 2)
                {
                    FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},attr.Format != SecsFormat.List({secsItem2.Format}) || attr.ItemList.Count != 2({secsItem2.ItemList.Count})");
                    return SecsErrorCode.ErrorIllegalData;
                }
                string key = secsItem2.ItemList[0].Value as string;
                SecsItem secsItem4 = (dictionary[key] = secsItem2.ItemList[1]);
            }
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Finished messageId={messageId}");
            return SecsErrorCode.Success;
        }

        private SVCACK doServiceRequest(uint opID, string objSpec, string svcName, List<KeyValuePair<string, SecsItem>> paramList, ref uint linkId, ref List<KeyValuePair<string, SecsItem>> paramListReturned, ref List<KeyValuePair<ERRCODE, string>> errorList)
        {
            return SVCACK.ServiceNotExist;
        }

        private SecsErrorCode onGenericServiceRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 5)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},item.Format != SecsFormat.List({item.Format})|| item.ItemList.Count != 5({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            SecsItem secsItem = item.ItemList[0];
            if (!secsItem.IsUnsignedInterger())
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemDataID.IsUnsignedInterger()({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            uint num = Convert.ToUInt32(secsItem.Value);
            SecsItem secsItem2 = item.ItemList[1];
            if (!secsItem2.IsUnsignedInterger())
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemOpID.IsUnsignedInterger()({secsItem2.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            uint opID = Convert.ToUInt32(secsItem2.Value);
            SecsItem secsItem3 = item.ItemList[2];
            if (secsItem3.Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemObjSpec.Format != SecsFormat.Ascii({secsItem3.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string objSpec = secsItem3.Value as string;
            SecsItem secsItem4 = item.ItemList[3];
            if (secsItem4.Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemSvcName.Format != SecsFormat.Ascii({secsItem4.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string svcName = secsItem4.Value as string;
            SecsItem secsItem5 = item.ItemList[4];
            if (secsItem5.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},itemParamList.Format != SecsFormat.List({secsItem5.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<KeyValuePair<string, SecsItem>> list = parseAttrList(secsItem5);
            if (list == null)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, {SecsErrorCode.ErrorIllegalData},paramList == null");
                return SecsErrorCode.ErrorIllegalData;
            }
            uint linkId = 0u;
            List<KeyValuePair<string, SecsItem>> paramListReturned = null;
            List<KeyValuePair<ERRCODE, string>> errorList = null;
            SVCACK v = doServiceRequest(opID, objSpec, svcName, list, ref linkId, ref paramListReturned, ref errorList);
            SecsItem secsItem6 = new SecsItem(SecsFormat.List);
            secsItem6.AppendItem(new SecsItem((byte)v));
            secsItem6.AppendItem(new SecsItem(linkId));
            SecsItem secsItem7 = new SecsItem(SecsFormat.List);
            if (paramListReturned != null)
            {
                foreach (KeyValuePair<string, SecsItem> item3 in paramListReturned)
                {
                    SecsItem secsItem8 = new SecsItem(SecsFormat.List);
                    secsItem8.AppendItem(new SecsItem(item3.Key));
                    secsItem8.AppendItem(item3.Value);
                    secsItem7.AppendItem(secsItem8);
                }
            }
            secsItem6.AppendItem(secsItem7);
            SecsItem secsItem9 = new SecsItem(SecsFormat.List);
            secsItem9.AppendItem(new SecsItem((byte)v));
            SecsItem item2 = new SecsItem(SecsFormat.List);
            secsItem9.AppendItem(item2);
            if (errorList != null)
            {
                foreach (KeyValuePair<ERRCODE, string> item4 in errorList)
                {
                    SecsItem secsItem10 = new SecsItem(SecsFormat.List);
                    secsItem10.AppendItem(new SecsItem((byte)item4.Key));
                    secsItem10.AppendItem(new SecsItem(item4.Value));
                }
            }
            secsItem6.AppendItem(secsItem9);
            ReplyMessage(14, 20, secsItem6, messageId);
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onGenericServiceCompletionInformation(SecsItem item, uint messageId)
        {
            if (item.Format != 0 || item.ItemList.Count != 5)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            SecsItem secsItem = item.ItemList[0];
            if (!secsItem.IsUnsignedInterger())
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            uint num = Convert.ToUInt32(secsItem.Value);
            SecsItem secsItem2 = item.ItemList[1];
            if (!secsItem2.IsUnsignedInterger())
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            uint num2 = Convert.ToUInt32(secsItem2.Value);
            SecsItem secsItem3 = item.ItemList[2];
            if (secsItem3.Format != SecsFormat.U4)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            uint num3 = (uint)secsItem3.Value;
            SecsItem secsItem4 = item.ItemList[3];
            if (secsItem4.Format != 0)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            Dictionary<string, SecsItem> dictionary = new Dictionary<string, SecsItem>();
            for (int i = 0; i < secsItem4.ItemList.Count; i++)
            {
                SecsItem secsItem5 = secsItem4.ItemList[i];
                if (secsItem5.Format != 0 || secsItem5.ItemList.Count != 2)
                {
                    return SecsErrorCode.ErrorIllegalData;
                }
                string key = secsItem5.ItemList[0].Value as string;
                SecsItem secsItem7 = (dictionary[key] = secsItem5.ItemList[1]);
            }
            SecsItem secsItem8 = item.ItemList[4];
            if (secsItem8.Format != 0 || secsItem8.ItemList.Count != 2)
            {
                return SecsErrorCode.ErrorIllegalData;
            }
            byte b = (byte)secsItem8.ItemList[0].Value;
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onGetServiceNameRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 2)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, item.Format != SecsFormat.List({item.Format}) || item.ItemList.Count != 2({item.ItemList.Count})");
                return SecsErrorCode.ErrorIllegalData;
            }
            SecsItem secsItem = item.ItemList[0];
            if (secsItem.Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, itemObjSpec.Format != SecsFormat.Ascii({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text = secsItem.Value as string;
            SecsItem secsItem2 = item.ItemList[1];
            if (secsItem2.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, itemObjTypeList.Format != SecsFormat.List({secsItem2.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<string> list = new List<string>();
            for (int i = 0; i < secsItem2.ItemList.Count; i++)
            {
                SecsItem secsItem3 = secsItem2.ItemList[i];
                if (secsItem3.Format != SecsFormat.Ascii && !secsItem3.IsUnsignedInterger())
                {
                    FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, itemObjType.Format != SecsFormat.Ascii({secsItem3.Format})&&itemObjType.IsUnsignedInterger()（{secsItem3.Format}）");
                    return SecsErrorCode.ErrorIllegalData;
                }
                string item2 = secsItem3.Value as string;
                list.Add(item2);
            }
            return SecsErrorCode.Success;
        }

        private SecsErrorCode onGetServiceParameterNameRequest(SecsItem item, uint messageId)
        {
            FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: messageId={messageId}");
            if (item.Format != 0 || item.ItemList.Count != 3)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, item.Format != SecsFormat.List({item.Format})&&item.ItemList.Count!=3（{item.ItemList.Count}）");
                return SecsErrorCode.ErrorIllegalData;
            }
            SecsItem secsItem = item.ItemList[0];
            if (secsItem.Format != SecsFormat.Ascii)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, itemObjSpec.Format != SecsFormat.Ascii({secsItem.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text = secsItem.Value as string;
            SecsItem secsItem2 = item.ItemList[1];
            if (secsItem2.Format != SecsFormat.Ascii && !secsItem2.IsUnsignedInterger())
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, itemObjType.Format != SecsFormat.Ascii({secsItem2.Format})&&itemObjType.IsUnsignedInterger()({secsItem2.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            string text2 = secsItem2.Value as string;
            SecsItem secsItem3 = item.ItemList[2];
            if (secsItem3.Format != 0)
            {
                FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Error, itemSvcNameList.Format != SecsFormat.List({secsItem3.Format})");
                return SecsErrorCode.ErrorIllegalData;
            }
            List<string> list = new List<string>();
            for (int i = 0; i < secsItem3.ItemList.Count; i++)
            {
                SecsItem secsItem4 = secsItem3.ItemList[i];
                string item2 = secsItem4.Value as string;
                list.Add(item2);
            }
            return SecsErrorCode.Success;
        }

        public bool RegisterObjectTypeHandler(string objType, IE39Module module)
        {
            string key = objType.ToLower();
            if (m_objTypeDict.ContainsKey(key))
            {
                return false;
            }
            m_objTypeDict[key] = module;
            return true;
        }

        public bool UnRegisterObjectTypeHandler()
        {
            m_objTypeDict.Clear();
            return true;
        }

        public bool RegisterStreamFunctionHandler(byte stream, byte func, IE39Module module)
        {
            ushort key = makeSFKey(stream, func);
            if (m_sfHandlerDict.ContainsKey(key))
            {
                return false;
            }
            m_sfHandlerDict[key] = module;
            return true;
        }

        public bool UnRegisterStreamFunctionHandler()
        {
            m_sfHandlerDict.Clear();
            return true;
        }

        private ushort makeSFKey(byte stream, byte func)
        {
            ushort num = stream;
            num = (ushort)(num << 8);
            return (ushort)(num + func);
        }
    }
}
