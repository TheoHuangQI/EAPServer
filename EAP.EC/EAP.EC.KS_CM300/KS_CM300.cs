using EAP.HC.Core;
using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
using EAP.RC.MES;
using EAPManage.Utils;
using Newtonsoft.Json.Linq;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EAP.EC.KS_CM300
{
    public class KS_CM300
    {
        private Logger logger = Logger.Inst();
        #region Handler
        public void SecsMsgHandler(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var context = EAPContext.contexts.FirstOrDefault(a => a.EquipmentID == eqpId);
                if (context != null)
                {
                    var stream = args.SecsData.PrimaryMessage.S;
                    var function = args.SecsData.PrimaryMessage.F;
                    MESClient.Send_Message(eqpId,"1",$"Recive SECS  MSG:S{stream}F{function}",LogType.INFO);
                    switch (stream)
                    {
                        case 1:
                            switch (function)
                            {
                                case 1:
                                    ReciveMsgS1F1(args);
                                    break;
                            }
                            break;
                        case 5:
                            switch (function)
                            {
                                case 1:
                                    ReciveMsgS5F1(args);
                                    break;
                            }
                            break;
                        case 6:
                            switch (function)
                            {
                                case 11:
                                    ReciveMsgS6F11(args);
                                    break;
                            }
                            break;
                        default:
                            break;
                    }
                }
                args.SetResultOK();

            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                args.SetResultNG(ex.Message);
            }
        }

        public void ConnectionChange(Arguments args)
        {
            var state = (ConnectionState)args.Header[CItems.ConnectionState];
            switch (state)
            {
                case ConnectionState.Connecting:
                    break;
                case ConnectionState.Connected:
                    break;
                case ConnectionState.Selected:
                    HsmsConnectAsync(args);
                    break;
                case ConnectionState.Retry:
                    HsmsDisConnect(args);
                    break;
            }
        }

        public async void ReciveMsgS6F11(Arguments args)
        {
            try
            {
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                Item replyItem = Item.B((byte)0);
                SecsMessage replyMsg = new SecsMessage(6, 12);
                replyMsg.SecsItem = replyItem;
                await args.SecsData.TryReplyAsync(replyMsg);

                var dataid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[0]).ToString());
                var ceid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
                switch (ceid)
                {
                    case 8:
                    case 9:
                    case 10:
                        Receive_S6F11_ControlStateChange(args);
                        break;
                    case 37:
                        Receive_S6F11_CIDWaitingForHost(args);
                        break;
                    case 48:
                        Receive_S6F11_SlotMapWaitingForHost(args);
                        break;
                    case 49:
                        Receive_S6F11_SlotMapVerifyok(args);
                        break;
                    case 92:
                        Receive_S6F11_WaferProcessComplete(args);
                        break;
                    case 105:
                        Receive_S6F11_PRJOB_QUEUED(args);
                        break;
                    case 127:
                        Receive_S6F11_ProcessStart(args);
                        break;
                    case 132:
                        Receive_S6F11_ProcessEnd(args);
                        break;
                }
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }
        #endregion Handler


        #region Receive  Message

        public async void ReciveMsgS1F1(Arguments args)
        {
            try
            {
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                Item replyItem = Item.L();
                SecsMessage replyMsg = new SecsMessage(1, 2);
                replyMsg.SecsItem = replyItem;
                await args.SecsData.TryReplyAsync(replyMsg);
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public void ReciveMsgS5F1(Arguments args)
        {
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            args.SetResultOK();
        }

        public async void Receive_S6F11_ControlStateChange(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                if (rootItem.Items.Length != 3)
                {
                    return;
                }
                var ceid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
                var eqp = AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId);
                if (eqp.Value != null)
                {
                    switch (ceid)
                    {
                        case 8:
                            eqp.Value.ControlStatus = ControlState.EQPOFFLINE;
                            break;
                        case 9:
                            eqp.Value.ControlStatus = ControlState.LOCAL;
                            break;
                        case 10:
                            eqp.Value.ControlStatus = ControlState.REMOTE;
                            if (!eqp.Value.IsInitial)
                            {
                                await HsmsInitial(args);
                                eqp.Value.IsInitial = true;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }


        }

        public async void Receive_S6F11_CIDWaitingForHost(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var portstr = SecsItemUtil.GetItemValue(rootItem.Items[2][1][1][0]).ToString().Trim();
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][2][1][0]).ToString().Trim();
            var portId = -1;
            switch (portstr)
            {
                case "LP1":
                    portId = 1;
                    break;
                case "LP2":
                    portId = 2;
                    break;
                case "LP3":
                    portId = 3;
                    break;
                case "LP4":
                    portId = 4;
                    break;
            }
            var mesbatchInfo = MESClient.MES_LotInfoDownloadRequest(eqpId,carrierId);
            if (mesbatchInfo == null)
            {
                args.SetResultNG();
                return;
            }
            if (!AppCache.Current.MesBatchInfos.ContainsKey(mesbatchInfo.BATCHCHID))
            {
                AppCache.Current.MesBatchInfos.Add(mesbatchInfo.BATCHCHID, mesbatchInfo);
            }
            args.Header[CItems.PortId] = portId;
            args.Header[CItems.CarrierId] = carrierId;
            AppCache.Current.EqpInfos[eqpId].PortInfo[portId].CarrierId = carrierId;
            await Send_S3F17_FirstProcessWithCarrier(args);
            if (args.GetResultCode() != "OK")
            {
                Send_S3F17_CarrierRelease(args);
            }
        }

        public async void Receive_S6F11_SlotMapWaitingForHost(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var portstr = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0]).ToString().Trim();
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][2][1][0]).ToString().Trim();
            var portId = -1;
            switch (portstr)
            {
                case "1":
                    portId = 1;
                    break;
                case "2":
                    portId = 2;
                    break;
                case "3":
                    portId = 3;
                    break;
                case "4":
                    portId = 4;
                    break;
            }
            args.Header[CItems.CarrierId] = carrierId;
            args.Header[CItems.PortId] = portId;
            var slotMapStr = "";
            List<int> slotMap = new List<int>();
            if (rootItem.Items[2][3][1][0].Items.Length == 25)
            {
                foreach(var item in rootItem.Items[2][3][1][0].Items)
                {
                    var value = SecsItemUtil.GetItemValue(item).ToString().Trim();
                    slotMap.Add(int.Parse(value));
                    slotMapStr += value;
                    slotMapStr += ";";
                }
            }
            MESClient.Send_MapResult(eqpId, portId.ToString(), slotMapStr);
            var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
            if (batchInfo != null)
            {
                foreach (var lot in batchInfo.LOTLIST)
                {
                    foreach (var wafer in lot.WAFERS)
                    {

                        if (slotMap[wafer.SLOT-1]!=3)
                        {
                            args.SetResultNG($"SlotMap  Check Fail: MES Slot :{wafer.SLOT} Not Exist in Carrier,EQP Slot Map: {slotMapStr}");
                            goto ExitLoop;
                        }
                    }
                }
            ExitLoop:
                if (args.GetResultCode() == "NG")
                {
                    Send_S3F17_CancelCarrier(args);
                    return;
                }
                args.Header[CItems.SlotMap]= slotMap;
                args.Header[CItems.BatchInfo] = batchInfo;
                await Send_S3F17_SecondProcessWithCarrier(args);
                if(args.GetResultCode()!="OK")
                    Send_S3F17_CancelCarrier(args);
            }
            else
            {
                args.SetResultNG();
                Send_S3F17_CancelCarrier(args);
            }
        }

        public async void Receive_S6F11_SlotMapVerifyok(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            await Send_S16F21_SelectPJSpace(args);
            if (args.GetResultCode() != "OK")
            {
                Send_S3F17_CancelCarrier(args);
                args.SetResultNG();
                return;
            }
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][2][1][0]).ToString().Trim();
            var portId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0]).ToString().Trim();
            args.Header[CItems.CarrierId] = carrierId;
            args.Header[CItems.PortId] = portId;

            var res = MESClient.MES_TrackIn(eqpId, carrierId);
            if (res)
            {
                await Send_S16F15_CreatePJ(args);
                if (args.GetResultCode() != "OK")
                {
                    Send_S3F17_CancelCarrier(args);
                    args.SetResultNG();
                    return;
                }
            }
            else
            {
                Send_S3F17_CancelCarrier(args);
                args.SetResultNG("TrackIn Fail");
                return;
            }
        }

        public async void Receive_S6F11_WaferProcessComplete(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            string SubstLocSubstLocID= SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][3]).ToString().Trim();
            var carrierId = SubstLocSubstLocID.Split('.')[0];
            var slot = SubstLocSubstLocID.Split('.')[1];
            var portId= AppCache.Current.EqpInfos[eqpId].PortInfo.Values.FirstOrDefault(a => a.CarrierId == carrierId).PortNo.ToString();
            MESClient.Send_MapResult(eqpId, portId, slot);
        }
        
        public async void Receive_S6F11_PRJOB_QUEUED(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var pjid = SecsItemUtil.GetItemValue(rootItem[2][0][1][0]).ToString();
            var carrierId = SecsItemUtil.GetItemValue(rootItem[2][0][1][2][0][0]).ToString();
            args.Header[CItems.CarrierId] = carrierId;
            var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
            args.Header[CItems.BatchInfo] = batchInfo;
            AppCache.Current.PjQuendList.Add(pjid);
            foreach(var lot in batchInfo.LOTLIST)
            {
                if (AppCache.Current.PjQuendList.Contains(lot.LOT_ID))
                    continue;
                else
                {
                    args.SetResultNG();
                    return;
                }
            }
            await Send_S14F9_CreateCJ(args);
        }

        public void Receive_S6F11_ProcessStart(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0][0]).ToString().Trim();
            MESClient.MES_LotStart(eqpId, carrierId);
        }

        public void Receive_S6F11_ProcessEnd(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0][0]).ToString().Trim();
            MESClient.MES_LotEnd(eqpId, carrierId);
            MESClient.MES_TrackOut(eqpId, carrierId);
        }
        #endregion Receive  Message


        #region Send Message
        public async Task Send_S2F37(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var eventLinkRptDic = args.Header[CItems.EventLinkRptDic] as Dictionary<uint, List<uint>>;
                var ceids = eventLinkRptDic.Keys.ToList();
                var enable = (bool)args.Header[CItems.Enable];
                SecsMessage msg = new SecsMessage(2, 37);
                Item[] items = new Item[ceids.Count];
                int i = 0;
                foreach (var ceid in ceids)
                {
                    items[i] = Item.U4(ceid);
                    i++;
                }
                Item item = Item.L(items);
                msg.SecsItem = Item.L(new Item[] { Item.Boolean(enable), item });
                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
                var reply = await context.SecsGem.SendAsync(msg);
                byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem);
                if (retCode == null || retCode != 0)
                {
                    args.SetResultNG("Send S2F37, Equipment Reject.");
                    return;
                }
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public async Task Send_S2F35(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var eventLinkRptDic = args.Header[CItems.EventLinkRptDic] as Dictionary<uint, List<uint>>;
                var enable = (bool)args.Header[CItems.Enable];

                SecsMessage msg = new SecsMessage(2, 35);
                if (enable)
                {
                    Item[] items = new Item[eventLinkRptDic.Count];
                    int i = 0;
                    foreach (var evt in eventLinkRptDic)
                    {
                        Item[] items1 = new Item[evt.Value.Count];
                        for (int j = 0; j < evt.Value.Count; j++)
                        {
                            items1[j] = Item.U4(evt.Value[j]);
                        }
                        items[i] = Item.L(new Item[] { Item.U4(evt.Key), Item.L(items1) });
                        i++;
                    }

                    Item item = Item.L(items);
                    msg.SecsItem = Item.L(new Item[] { Item.U4(0), item });
                }
                else
                {
                    Item[] items = new Item[eventLinkRptDic.Count];
                    int i = 0;
                    foreach (var evt in eventLinkRptDic)
                    {
                        items[i] = Item.L(new Item[] { Item.U4(evt.Key), Item.L() });
                        i++;
                    }
                    Item item = Item.L(items);
                    msg.SecsItem = Item.L(new Item[] { Item.U4(0), item });
                }
                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
                var reply = await context.SecsGem.SendAsync(msg);
                byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem);
                if (retCode == null || retCode != 0)
                {
                    args.SetResultNG("Send S2F37, Equipment Reject.");
                    return;
                }
                args.SetResultOK();

            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public async Task Send_S2F33(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var dataReportDic = args.Header[CItems.DataReportDic] as Dictionary<uint, List<uint>>;
                var enable = (bool)args.Header[CItems.Enable];
                SecsMessage msg = new SecsMessage(2, 33);
                if (enable)
                {
                    Item[] items = new Item[dataReportDic.Count];
                    int i = 0;
                    foreach (var rpt in dataReportDic)
                    {
                        Item[] items1 = new Item[rpt.Value.Count];
                        for (int j = 0; j < rpt.Value.Count; j++)
                        {
                            items1[j] = Item.U4(rpt.Value[j]);
                        }
                        items[i] = Item.L(new Item[] { Item.U4(rpt.Key), Item.L(items1) });
                        i++;
                    }
                    Item item = Item.L(items);
                    msg.SecsItem = Item.L(new Item[] { Item.U4(0), item });
                }
                else
                {
                    msg.SecsItem = Item.L(new Item[] { Item.U4(0), Item.L() });
                }
                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
                var reply = await context.SecsGem.SendAsync(msg);
                byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem);
                if (retCode == null || retCode != 0)
                {
                    args.SetResultNG("Send S2F37, Equipment Reject.");
                    return;
                }
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public async Task Send_S3F17_FirstProcessWithCarrier(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId), Item.U1((byte)portId), Item.L() };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            int retCode =(byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
            if (retCode != 0)
            {
                args.SetResultNG($"Send_S3F17_FirstProcessWithCarrierAsync, Equipment Reject,Return Code:{retCode}.");
                return;
            }
            args.SetResultOK();
        }

        public async Task Send_S3F17_SecondProcessWithCarrier(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            var eqpSlotMap = args.Header[CItems.SlotMap] as List<int>;
            var batchInfo = args.Header[CItems.BatchInfo] as MESLotInfoDownloadReply;
            SecsMessage msg = new SecsMessage(3, 17);

            Item[] slotItems = new Item[25];
            Item[] contentItems = new Item[25];
            for(int i = 0; i < 25; i++)
            {
                slotItems[i] = Item.U1((byte)eqpSlotMap[i]);
                Wafer wafer=null;
                string LotId=string.Empty;
                foreach(var lot in batchInfo.LOTLIST)
                {
                    wafer = lot.WAFERS.FirstOrDefault(m => m.SLOT == (i + 1));
                    LotId = lot.LOT_ID;
                    if (wafer != null)
                        goto ExitLoop;
                }
            ExitLoop:
                contentItems[i] = wafer== null ? Item.L(new Item[] { Item.A(""), Item.A("") }) : Item.L(new Item[] { Item.A(LotId), Item.A(wafer.WAFER_ID) });
            }
            Item mapItem = Item.L();
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId), Item.U1((byte)portId), Item.L(new Item[] {Item.L(Item.A("ContentMap"),Item.L(contentItems)), Item.L(Item.A("SlotMap"), Item.L(slotItems)) }) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            int retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
            if (retCode != 0)
            {
                args.SetResultNG($"Send_S3F17_SecondProcessWithCarrierAsync, Equipment Reject,Return Code:{retCode}.");
                return;
            }
            args.SetResultOK();
        }

        public async void Send_S3F17_CarrierRelease(Arguments args)
        {

            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("CarrierRelease"), Item.A(carrierId), Item.U1((byte)portId), Item.L() };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            int retCode = (int)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
            if (retCode != 0)
            {
                args.SetResultNG($"Send_S3F17_CarrierRelease, Equipment Reject,Return Code:{retCode}.");
                return;
            }
            args.SetResultOK();
        }

        public async void Send_S3F17_CancelCarrier(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("CancelCarrier"), Item.A(carrierId), Item.U1((byte)portId), Item.L() };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            int retCode = (int)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
            if (retCode != 0)
            {
                args.SetResultNG($"Send_S3F17_CancelCarrier, Equipment Reject,Return Code:{retCode}.");
                return;
            }
            args.SetResultOK();
        }


        public async Task<List<string>> Send_S7F19_GetRecipeList(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(7, 19);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            var count = reply.SecsItem.Items.Count();
            List<string> recipeList = new List<string>();
            for(int i = 0; i < count; i++)
            {
                recipeList.Add(SecsItemUtil.GetItemValue(reply.SecsItem.Items[i]).ToString());
            }
            return recipeList;
        }

        public async Task Send_S16F21_SelectPJSpace(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(16, 21);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            int retCode = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem).ToString());
            if (retCode <= 0)
            {
                args.SetResultNG();
                return;
            }
            args.SetResultOK();
        }

        public async Task Send_S16F15_CreatePJ(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
            SecsMessage msg = new SecsMessage(16, 15);
            Item[] pjItems = new Item[batchInfo.BATCHSIZE];
            int index = 0;
            List<int> slots = new List<int>();
            foreach(var lot in batchInfo.LOTLIST)
            {
                var pjIdItem = Item.A(lot.LOT_ID);
                var meatItem = Item.B((byte)13);
                var slotItems = new Item[lot.WAFERS.Count];
                int i = 0;
                foreach(var wafer in lot.WAFERS)
                {
                    slotItems[i] = Item.U1((byte)wafer.SLOT);
                    slots.Add(wafer.SLOT);
                    i++;
                }
                var carrierSpecItem =Item.L(new Item[] { Item.L(new Item[] {Item.A(carrierId),Item.L(slotItems) })});
                var recipeSpecItem = Item.L(new Item[] { Item.U1((byte)1), Item.A(lot.PPID), Item.L() });
                var startItem = Item.Boolean(true);
                var pauseEventItem = Item.L();

                pjItems[index] = Item.L(new Item[] { pjIdItem,meatItem,carrierSpecItem,recipeSpecItem,startItem,pauseEventItem });
                index++;
            }
            msg.SecsItem = Item.L(new Item[] {Item.U1((byte)0),Item.L(pjItems) });
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            var res = (bool)SecsItemUtil.GetItemValue(reply.SecsItem[1][0]);
            if(!res)
            {
                args.SetResultNG();
                return;
            }
            MESClient.Send_MapResult(EqpId, portId.ToString(), string.Join(';', slots)+";");
            args.SetResultOK();
        }

        public async Task Send_S14F9_CreateCJ(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
            SecsMessage msg = new SecsMessage(14, 9);
            Item[] pjItems = new Item[batchInfo.BATCHSIZE];
            int index = 0;
            foreach(var lot in batchInfo.LOTLIST)
            {
                Item pjItem = Item.L(new Item[] { Item.A(lot.LOT_ID),Item.L(),Item.L() });
                pjItems[index] = pjItem;
                index++;
            }
            Item cjItem = Item.L(new Item[] { 
                Item.L(new Item[] {Item.A("ObjID"),Item.A("ControlJob101")}),
                Item.L(new Item[]{Item.A("CarrierInputSpec"),Item.L(new Item[] { Item.A(batchInfo.CARRIERID)}) }),
                Item.L(new Item[]{Item.A("ProcessingCtrlSpec"),Item.L(pjItems) }),
                Item.L(new Item[] {Item.A("ProcessOrderMgmt"),Item.U1((byte)3)}),
                Item.L(new Item[] {Item.A("StartMethod"),Item.Boolean(true)}),
                Item.L(new Item[] {Item.A("MtrlOutSpec"),Item.L()}),
                Item.L(new Item[] {Item.A("PauseEvent"),Item.L()}),
            });
            msg.SecsItem = Item.L(new Item[] {Item.A("Equipment"),Item.A("ControlJob"), cjItem });
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            var res =int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[2][0]).ToString());
            if (res!=0)
            {
                args.SetResultNG();
                return;
            }
            args.SetResultOK();
        }
        #endregion Send Message

        public List<string> GetPPIDList(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var recipeList = Send_S7F19_GetRecipeList(args).Result;
            return recipeList;
        }

        public async Task HsmsConnectAsync(Arguments args)
        {
            //建立逻辑连接
            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == args.Header[CItems.EqpId].ToString());
            SecsMessage msg = new SecsMessage(1, 13);
            msg.SecsItem = Item.L();
            var reply = await context.SecsGem.SendAsync(msg);
            byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem[0]);
            if (retCode == null || retCode != 0)
            {
                args.SetResultNG("Send S1F13, Equipment Reply Fail.");
                return;
            }
            MESClient.Send_EqpState(eqpId, EQPState.Connected);
            AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId).Value.ConnectStatus = ConnectStatus.CONNECTED;
            args.SetResultOK();
        }

        public void HsmsDisConnect(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            //建立逻辑连接
            AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId).Value.ConnectStatus = ConnectStatus.DISCONNECTED;
        }

        public async Task HsmsInitial(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                LoadInitiaLXml(args);
                if (args.GetResultCode() == "OK")
                {
                    var dataReportDic = args.Header[CItems.DataReportDic] as Dictionary<uint, List<uint>>;
                    var eventLinkRptDic = args.Header[CItems.EventLinkRptDic] as Dictionary<uint, List<uint>>;
                    if (dataReportDic.Count > 0 && eventLinkRptDic.Count > 0)
                    {
                        //disable
                        args.Header[CItems.Enable] = false;
                        await Send_S2F37(args);
                        if (args.GetResultCode()!="OK") return;
                        await Send_S2F35(args);
                        if (args.GetResultCode() != "OK") return;
                        await Send_S2F33(args);
                        if (args.GetResultCode() != "OK") return;
                        args.Header[CItems.Enable] = true;
                        await Send_S2F33(args);
                        if (args.GetResultCode() != "OK") return;
                        await Send_S2F35(args);
                        if (args.GetResultCode() != "OK") return;
                        await Send_S2F37(args);
                        if (args.GetResultCode() != "OK") return;
                    }
                }
                args.SetResultOK();
            }
            catch(Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
            
        }

        private void LoadInitiaLXml(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var ChamberList = AppCache.Current.EqpInfos[eqpId].Chamber.Split(',').ToList();
                Dictionary<uint, List<uint>> dataReportDic = new Dictionary<uint, List<uint>>();
                Dictionary<uint, List<uint>> eventLinkRptDic = new Dictionary<uint, List<uint>>();
                string path = Path.Combine(EAPContext.RootPath, $"event-report.xml");

                if (!File.Exists(path))
                {
                    return;
                }
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(path);
                XmlNode xmlNode = xmlDocument.SelectSingleNode("/EVENT_REPORT");
                XmlNodeList xmlNodeList = xmlNode.SelectNodes("REPORT");
                foreach (XmlNode item in xmlNodeList)
                {
                    string value = XmlUtils.XmlGetNodeAttrVal(item, "id");
                    uint rptid = Convert.ToUInt32(value);
                    if (dataReportDic.ContainsKey(rptid))
                    {
                        dataReportDic.Remove(rptid);
                    }
                    dataReportDic.Add(rptid, new List<uint>());
                    XmlNodeList xmlNodeList2 = item.SelectNodes("DV");
                    foreach (XmlNode item2 in xmlNodeList2)
                    {
                        string value2 = XmlUtils.XmlGetNodeAttrVal(item2, "id");
                        uint vid = Convert.ToUInt32(value2);
                        if(vid>=20000 && vid <= 40000)
                        {
                            var flag = (vid - 20000) % 100;
                            if (flag == 0 || flag == 1 || flag == 2 || flag == 3 || flag == 4 || flag == 5 || flag == 6 || flag == 7)
                            {
                                var chIndex = (vid - flag - 20000) / 100;
                                if (!ChamberList.Contains(chIndex.ToString()))
                                {
                                    dataReportDic.Remove(rptid);
                                    break;
                                }
                            }
                        }
                        

                        if (!dataReportDic[rptid].Contains(vid))
                        {
                            dataReportDic[rptid].Add(vid);
                        }
                    }
                }


                XmlNodeList xmlNodeList3 = xmlNode.SelectNodes("EVENT");
                foreach (XmlNode item3 in xmlNodeList3)
                {
                    string value3 = XmlUtils.XmlGetNodeAttrVal(item3, "id");
                    uint ceid = Convert.ToUInt32(value3);
                    if (ceid >= 20000 && ceid <30000)
                    {
                        var flag = (ceid - 20000) % 100;
                        if (flag == 0 || flag == 1 || flag == 10 || flag ==11)
                        {
                            var chIndex = (ceid - flag - 20000) / 100;
                            if (!ChamberList.Contains(chIndex.ToString()))
                            {
                                continue;
                            }
                        }
                    }
                    else if (ceid >= 30000 && ceid < 40000)
                    {
                        var flag = (ceid - 30000) % 100;
                        if (flag == 0 || flag == 1 || flag == 2 || flag == 3)
                        {
                            var chIndex = (ceid - flag - 30000) / 100;
                            if (!ChamberList.Contains(chIndex.ToString()))
                            {
                                continue;
                            }
                        }
                    }
                    if (eventLinkRptDic.ContainsKey(ceid))
                    {
                        eventLinkRptDic.Remove(ceid);
                    }
                    eventLinkRptDic.Add(ceid, new List<uint>());
                    XmlNodeList xmlNodeList4 = item3.SelectNodes("RPT");
                    foreach (XmlNode item4 in xmlNodeList4)
                    {
                        string value4 = XmlUtils.XmlGetNodeAttrVal(item4, "id");
                        uint rptid = Convert.ToUInt32(value4);
                        if (!eventLinkRptDic[ceid].Contains(rptid))
                        {
                            eventLinkRptDic[ceid].Add(rptid);
                        }
                    }
                }

                args.Header[CItems.DataReportDic] = dataReportDic;
                args.Header[CItems.EventLinkRptDic] = eventLinkRptDic;
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
           
        }
    }
}
