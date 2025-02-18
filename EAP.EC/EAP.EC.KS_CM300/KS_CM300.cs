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
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EAP.EC.KS_CM300
{
    public class KS_CM300
    {

        public void ReciveMsgS1F1(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            var rootItem = primaryMessage.PrimaryMessage.SecsItem;
            //Item replyItem = Item.L();
            //SecsMessage replyMsg = new SecsMessage(1, 2);
            //replyMsg.SecsItem = replyItem;
            //primaryMessage.TryReplyAsync(replyMsg);
        }

        public void ReciveMsgS5F1(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            var rootItem = primaryMessage.PrimaryMessage.SecsItem;
        }




        public void ReciveMsgS6F11(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            var rootItem = primaryMessage.PrimaryMessage.SecsItem;
            Item replyItem = Item.B((byte)0);
            SecsMessage replyMsg = new SecsMessage(6, 12);
            replyMsg.SecsItem = replyItem;
            primaryMessage.TryReplyAsync(replyMsg);

            var dataid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[0]).ToString());
            var ceid =int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
            switch (ceid)
            {
                case 8:
                case 9:
                case 10:
                    ControlStateChange(context.EquipmentID, rootItem);
                    break;
                case 37:
                    WaitingForHost(context.EquipmentID,rootItem);
                    break;
                case 48:

                    break;
            }
        }




        #region Handler
        public async void ControlStateChange(string EqpID, Item rootItem)
        {
            if (rootItem.Items.Length != 3)
            {
                return;
            }
            var ceid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
            var eqp= AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == EqpID);
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
                            await HsmsInitial(EqpID);
                            eqp.Value.IsInitial = true;
                        }
                        break;
                } 
            }
         
        }
        public void WaitingForHost(string EqpID,Item rootItem)
        {
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
            Send_FirstProcessWithCarrier(EqpID, portId, carrierId);
        }

        public void Send_FirstProcessWithCarrier(string EqpId,int portId,string carrierId)
        {
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId),Item.U1((byte)portId),Item.L()};
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply =context.SecsGem.SendAsync(msg);
        }

        #endregion Handler



        public void SecsMsgHandler(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            try
            {
                Console.Write(primaryMessage.ToString());
                var messageId = primaryMessage.Id;
                var stream = primaryMessage.PrimaryMessage.S;
                var function = primaryMessage.PrimaryMessage.F;
                switch (stream)
                {
                    case 1:
                        switch (function)
                        {
                            case 1:
                                ReciveMsgS1F1(context, primaryMessage);
                                break;
                        }
                        break;
                    case 5:
                        switch(function)
                        {
                            case 1:
                                ReciveMsgS5F1(context, primaryMessage);
                                break;
                        }
                        break;
                    case 6:
                        switch (function)
                        {
                            case 11:
                                ReciveMsgS6F11(context, primaryMessage);
                                break;
                        }
                        break;
                    default:
                        break;
                }
            }
            catch(Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

        public void ConnectionChange(ContextObject context, ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Connecting:
                    break;
                case ConnectionState.Connected:
                    break;
                case ConnectionState.Selected:
                    HsmsConnect(context);
                    break;
                case ConnectionState.Retry:
                    HsmsDisConnect(context);
                    break;
            }
        }

        public void HsmsConnect(ContextObject context)
        {
            //建立逻辑连接
            Send_S1F13(context);
            AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == context.EquipmentID).Value.ConnectStatus = ConnectStatus.CONNECTED;
        }

        public void HsmsDisConnect(ContextObject context)
        {
            //建立逻辑连接
            AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == context.EquipmentID).Value.ConnectStatus = ConnectStatus.DISCONNECTED;
        }

        public async Task HsmsInitial(string eqpId)
        {
            Dictionary<uint, List<uint>> datareports = new Dictionary<uint, List<uint>>();
            Dictionary<uint, List<uint>> eventLinks = new Dictionary<uint, List<uint>>();
            LoadInitiaLXml(ref datareports,ref eventLinks);
            if(datareports.Count>0 && eventLinks.Count > 0)
            {
                //disable
                await Send_S2F37(eqpId, eventLinks.Keys.ToList(), false);
                await Send_S2F35(eqpId, eventLinks, false);
                await Send_S2F33(eqpId, datareports, false);
         
                await Send_S2F33(eqpId, datareports, true);
                await Send_S2F35(eqpId, eventLinks, true);
                await Send_S2F37(eqpId, eventLinks.Keys.ToList(), true);
            }
        }

        public async Task Send_S2F37(string eqpId,List<uint> ceids, bool enable)
        {
            SecsMessage msg = new SecsMessage(2, 37);
            Item[] items = new Item[ceids.Count];
            int i = 0;
            foreach(var ceid in ceids)
            {
                items[i]=Item.U4(ceid);
                i++;
            }
            Item item = Item.L(items);
            msg.SecsItem = Item.L(new Item[] { Item.Boolean(enable), item });
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = await context.SecsGem.SendAsync(msg);
        }

        public async Task Send_S2F35(string eqpId, Dictionary<uint, List<uint>> eventLinks, bool enable)
        {
            SecsMessage msg = new SecsMessage(2, 35);

            if (enable)
            {
                Item[] items = new Item[eventLinks.Count];
                int i = 0;
                foreach(var evt in eventLinks)
                {
                    Item[] items1 = new Item[evt.Value.Count];
                    for(int j=0;j<evt.Value.Count;j++)
                    {
                        items1[j]=Item.U4(evt.Value[j]);
                    }
                    items[i]=Item.L(new Item[] { Item.U4(evt.Key), Item.L(items1) });
                    i++;
                }

                Item item = Item.L(items);
                msg.SecsItem = Item.L(new Item[] { Item.U4(0), item });
            }
            else
            {
                Item[] items = new Item[eventLinks.Count];
                int i = 0;
                foreach (var evt in eventLinks)
                {
                    items[i]=Item.L(new Item[] { Item.U4(evt.Key), Item.L() });
                    i++;
                }
                Item item = Item.L(items);
                msg.SecsItem = Item.L(new Item[] { Item.U4(0), item });
            }

            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = await context.SecsGem.SendAsync(msg);
        }

        public async Task Send_S2F33(string eqpId, Dictionary<uint, List<uint>> datareports, bool enable)
        {
            SecsMessage msg = new SecsMessage(2, 33);
            if (enable)
            {
                Item[] items = new Item[datareports.Count];
                int i = 0;
                foreach (var rpt in datareports)
                {
                    Item[] items1 = new Item[rpt.Value.Count];
                    for (int j=0;j<rpt.Value.Count;j++)
                    {
                        items1[j]=Item.U4(rpt.Value[j]);
                    }
                    items[i]=Item.L(new Item[] { Item.U4(rpt.Key), Item.L(items1) });
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
        }

        public void Send_S1F13(ContextObject context)
        {
            SecsMessage msg = new SecsMessage(1, 13);
             msg.SecsItem = Item.L();
            var reply = context.SecsGem.SendAsync(msg);
        }

        public async Task<bool> Send_S1F3Async(ContextObject context,int vid)
        {
            SecsMessage msg = new SecsMessage(1, 3);
            Item[] items = new Item[] { Item.U4((uint)vid) };
            msg.SecsItem = Item.L(items);
            var reply = await context.SecsGem.SendAsync(msg);
            return true;
        }

        private void LoadInitiaLXml(ref Dictionary<uint, List<uint>> datareports,ref Dictionary<uint, List<uint>> eventLinks)
        {
            string path = Path.Combine(EAPContext.RootPath, $"event-report.xml");
            try
            {
                if (!File.Exists(path))
                {
                    return ;
                }
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(path);
                XmlNode xmlNode = xmlDocument.SelectSingleNode("/EVENT_REPORT");
                XmlNodeList xmlNodeList = xmlNode.SelectNodes("REPORT");
                foreach (XmlNode item in xmlNodeList)
                {
                    string value = XmlUtils.XmlGetNodeAttrVal(item, "id");
                    uint rptid = Convert.ToUInt32(value);
                    if (datareports.ContainsKey(rptid))
                    {
                        datareports.Remove(rptid);
                    }
                    datareports.Add(rptid, new List<uint>());
                    XmlNodeList xmlNodeList2 = item.SelectNodes("DV");
                    foreach (XmlNode item2 in xmlNodeList2)
                    {
                        string value2 = XmlUtils.XmlGetNodeAttrVal(item2, "id");
                        uint vid = Convert.ToUInt32(value2);

                        if (!datareports[rptid].Contains(vid))
                        {
                            datareports[rptid].Add(vid);
                        }
                    }
                }


                XmlNodeList xmlNodeList3 = xmlNode.SelectNodes("EVENT");
                foreach (XmlNode item3 in xmlNodeList3)
                {
                    string value3 = XmlUtils.XmlGetNodeAttrVal(item3, "id");
                    uint ceid = Convert.ToUInt32(value3);
                    if (eventLinks.ContainsKey(ceid))
                    {
                        eventLinks.Remove(ceid);
                    }
                    eventLinks.Add(ceid, new List<uint>());
                    XmlNodeList xmlNodeList4 = item3.SelectNodes("RPT");
                    foreach (XmlNode item4 in xmlNodeList4)
                    {
                        string value4 = XmlUtils.XmlGetNodeAttrVal(item4, "id");
                        uint rptid = Convert.ToUInt32(value4);
                        if (!eventLinks[ceid].Contains(rptid))
                        {
                            eventLinks[ceid].Add(rptid);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
            }
        }
    }
}
