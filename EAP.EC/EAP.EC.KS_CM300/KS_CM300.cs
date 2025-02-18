using EAP.HC.Core;
using EAP.HC.Core.Entities.MES;
using EAP.RC.MES;
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
                    HsmsConnect(args);
                    break;
                case ConnectionState.Retry:
                    HsmsDisConnect(args);
                    break;
            }
        }

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
            catch(Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public void ReciveMsgS5F1(Arguments args)
        {
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            args.SetResultOK();
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
                        ControlStateChange(args);
                        break;
                    case 37:
                        WaitingForHost(args);
                        break;
                    case 48:

                        break;
                }
                args.SetResultOK();
            }
            catch(Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }




        #region Handler
        public async void ControlStateChange(Arguments args)
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
            catch(Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
            
         
        }
        public void WaitingForHost(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
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
           var mesbatchInfo= MESService.MES_LotInfoDownloadRequest(carrierId);
            if (mesbatchInfo == null)
                return;
            if (!AppCache.Current.MesBatchInfos.ContainsKey(mesbatchInfo.BATCH_ID))
            {
                AppCache.Current.MesBatchInfos.Add(mesbatchInfo.BATCH_ID, mesbatchInfo);
            }
            args.Header[CItems.PortId] = portId;
            args.Header[CItems.CarrierId] = carrierId;
            Send_FirstProcessWithCarrier(args);
        }

        public void Send_FirstProcessWithCarrier(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId),Item.U1((byte)portId),Item.L()};
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply =context.SecsGem.SendAsync(msg);
        }

        #endregion Handler



        public void HsmsConnect(Arguments args)
        {
            //建立逻辑连接
            var eqpId = args.Header[CItems.EqpId].ToString();
            Send_S1F13(args);
            AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId).Value.ConnectStatus = ConnectStatus.CONNECTED;
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
            catch(Exception ex)
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
            catch(Exception ex)
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
            catch(Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public void Send_S1F13(Arguments args)
        {
            var context = EAPContext.contexts.First(a => a.EquipmentID == args.Header[CItems.EqpId].ToString());
            SecsMessage msg = new SecsMessage(1, 13);
             msg.SecsItem = Item.L();
            var reply = context.SecsGem.SendAsync(msg);
        }

        private void LoadInitiaLXml(Arguments args)
        {
            try
            {
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
