using EAP.HC.Core;
using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
using EAP.RC.MES;
using EAPManage.Utils;
using Newtonsoft.Json.Linq;
using Secs4Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EAP.EC.KS_CM300
{
    public class EventSort
    {
        public int index;
        public int ceid;
        public bool state;
    }

    public class ChannelContext
    {
        public bool IsRunning;
        public Dictionary<int, VIDItem> SvidDict;
        public Action<Arguments> PollAction;
    }

    public class VIDItem
    {
        public int VID;
        public string type;
        public string value;
        public string name;
    }

    public class CH
    {
        public string name;
        public bool start;
    }

    public class KS_CM300
    {
        private Logger logger = Logger.Inst();
        private System.Object obj = new System.Object();
        Dictionary<int, ChannelContext> _channelMap;
        Dictionary<string, Dictionary<int, VIDItem>> _channelSvidDictMap;

        static readonly Regex ChannelRegex = new Regex(@"^(L|R)\dCH\d+_Vlv\d+FlwPV$", RegexOptions.Compiled);
        static readonly Regex ChannelKeyRegex = new Regex(@"^(L|R)\dCH\d+", RegexOptions.Compiled);

        public static bool check = false;
        public static bool error = false;

        //public static bool L2CH1Start = false;
        //public static bool L2CH2Start = false;
        //public static bool L2CH3Start = false;
        //public static bool L2CH4Start = false;

        public KS_CM300()
        {
            InitChannels();
        }

        void InitChannels()
        {
            _channelMap = new Dictionary<int, ChannelContext>
            {
                // ===== L2 =====
                { 20900, new ChannelContext { SvidDict = L2CH1FlwSvidDict, PollAction = args => PollChannel(args, L2CH1FlwSvidDict) } },
                { 21000, new ChannelContext { SvidDict = L2CH2FlwSvidDict, PollAction = args => PollChannel(args, L2CH2FlwSvidDict) } },
                { 21100, new ChannelContext { SvidDict = L2CH3FlwSvidDict, PollAction = args => PollChannel(args, L2CH3FlwSvidDict) } },
                { 21200, new ChannelContext { SvidDict = L2CH4FlwSvidDict, PollAction = args => PollChannel(args, L2CH4FlwSvidDict) } },

                // ===== R2 =====
                { 21300, new ChannelContext { SvidDict = R2CH1FlwSvidDict, PollAction = args => PollChannel(args, R2CH1FlwSvidDict) } },
                { 21400, new ChannelContext { SvidDict = R2CH2FlwSvidDict, PollAction = args => PollChannel(args, R2CH2FlwSvidDict) } },
                { 21500, new ChannelContext { SvidDict = R2CH3FlwSvidDict, PollAction = args => PollChannel(args, R2CH3FlwSvidDict) } },
                { 21600, new ChannelContext { SvidDict = R2CH4FlwSvidDict, PollAction = args => PollChannel(args, R2CH4FlwSvidDict) } },

                // ===== L3 =====
                { 21700, new ChannelContext { SvidDict = L3CH1FlwSvidDict, PollAction = args => PollChannel(args, L3CH1FlwSvidDict) } },
                { 21800, new ChannelContext { SvidDict = L3CH2FlwSvidDict, PollAction = args => PollChannel(args, L3CH2FlwSvidDict) } },
                { 21900, new ChannelContext { SvidDict = L3CH3FlwSvidDict, PollAction = args => PollChannel(args, L3CH3FlwSvidDict) } },
                { 22000, new ChannelContext { SvidDict = L3CH4FlwSvidDict, PollAction = args => PollChannel(args, L3CH4FlwSvidDict) } },

                // ===== R3 =====
                { 22100, new ChannelContext { SvidDict = R3CH1FlwSvidDict, PollAction = args => PollChannel(args, R3CH1FlwSvidDict) } },
                { 22200, new ChannelContext { SvidDict = R3CH2FlwSvidDict, PollAction = args => PollChannel(args, R3CH2FlwSvidDict) } },
                { 22300, new ChannelContext { SvidDict = R3CH3FlwSvidDict, PollAction = args => PollChannel(args, R3CH3FlwSvidDict) } },
                { 22400, new ChannelContext { SvidDict = R3CH4FlwSvidDict, PollAction = args => PollChannel(args, R3CH4FlwSvidDict) } },
            };

            _channelSvidDictMap = new Dictionary<string, Dictionary<int, VIDItem>>
            {
                { "L2CH1", L2CH1FlwSvidDict },
                { "L2CH2", L2CH2FlwSvidDict },
                { "L2CH3", L2CH3FlwSvidDict },
                { "L2CH4", L2CH4FlwSvidDict },

                { "R2CH1", R2CH1FlwSvidDict },
                { "R2CH2", R2CH2FlwSvidDict },
                { "R2CH3", R2CH3FlwSvidDict },
                { "R2CH4", R2CH4FlwSvidDict },

                { "L3CH1", L3CH1FlwSvidDict },
                { "L3CH2", L3CH2FlwSvidDict },
                { "L3CH3", L3CH3FlwSvidDict },
                { "L3CH4", L3CH4FlwSvidDict },

                { "R3CH1", R3CH1FlwSvidDict },
                { "R3CH2", R3CH2FlwSvidDict },
                { "R3CH3", R3CH3FlwSvidDict },
                { "R3CH4", R3CH4FlwSvidDict },
            };
        }

        public void PollChannel(Arguments args, Dictionary<int, VIDItem> svidDict)
        {
            if (svidDict.Count == 0) return;

            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

            var item1 = new Item[L2CH1FlwSvidDict.Count];
            int i = 0;
            foreach (var (svid, vidItem) in svidDict)
            {
                item1[i] = Item.U4((uint)svid);
                i++;
            }
            try
            {
                var msg = new SecsMessage(1, 3)
                {
                    SecsItem = Item.L(item1)
                };


                SecsMessage reply = null;
                try
                {
                    reply = context.SecsGem.SendAsync(msg).Result;
                }
                catch (Exception ex)
                {
                    LogSVIDErrorAsync(0, $"Send S1F3 failed: {ex.Message}, {msg.SecsItem}", "SVID_ValueCheck_Result");
                }

                if (reply == null || reply.SecsItem == null || reply.SecsItem.Count == 0)
                {
                    LogSVIDErrorAsync(0, $"Empty S1F3 reply, {msg.SecsItem}", "SVID_ValueCheck_Result");
                    return;
                }

                for (int j = 0; j < reply.SecsItem.Items.Length; j++)
                {
                    Item resItem = reply.SecsItem.Items[j];
                    var actualFormat = resItem.Format.ToString();
                    string svidTemp = SecsItemUtil.GetItemValue(item1[j])?.ToString();
                    if (int.TryParse(svidTemp, out int svid))
                    {
                        if (resItem.Count > 0)
                        {
                            try
                            {
                                var rawValue = SecsItemUtil.GetItemValue(resItem)?.ToString();
                                string curValue = svidDict[svid].value;
                                VIDItem vID = svidDict[svid];
                                if (curValue == rawValue && curValue != "0" && rawValue != "0")
                                {
                                    LogSVIDErrorAsync(svid, $"Value match: last={curValue}, current={rawValue}", "SVID_ValueCheck_Result");
                                    //AsyncLogger.SVIDError(svid, $"Value match: last={curValue}, current={rawValue}", "SVID_ValueCheck_Result");
                                }
                                else
                                {
                                    svidDict[svid].value = rawValue;
                                }
                                //发给后端，保存到数据库
                                MESClient.Send_SVIDValue(eqpId, new VID() { vID = svid, name = vID.name, type = vID.type, value = rawValue });
                            }
                            catch (Exception ex)
                            {
                                LogSVIDErrorAsync(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
                            }
                        }
                        else
                        {
                            //LogSVIDErrorAsync(svid, "Item has no children", "SVID_ValueCheck_Result");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSVIDErrorAsync(-1, ex.Message, "SVID_ValueCheck_Result");
            }
        }

        void LogSVIDErrorAsync(int svid, string message, string category)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    LogSVIDError(svid, message, category);
                }
                catch
                {
                    // 日志失败不能反向影响业务线程
                }
            });
        }

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
                    // MESClient.Send_Message(eqpId, "1", $"Recive SECS  MSG:S{stream}F{function}", LogType.INFO);
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
                var eqpId = args.Header[CItems.EqpId].ToString();
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
                    case 23:
                    case 24:
                        Receive_S6F11_LPAccessStateChange(args);
                        break;
                    case 70:
                    case 72:
                    case 73:
                        Receive_S6F11_LPTransferStateChange(args);
                        break;
                    case 59:
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
                    default:
                        await HandleChannelCEIDAsync(ceid, args);
                        break;
                }
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        private async Task HandleChannelCEIDAsync(int ceid, Arguments args)
        {
            int startCeid = ceid - (ceid % 2);
            int stopCeid = startCeid + 1;

            if (!_channelMap.TryGetValue(startCeid, out var ctx))
                return;

            LoadSVIDDef();
            if (ceid == startCeid)
            {
                LogSVIDErrorAsync(ceid, $"Received Start CEID: {ceid}", "SVID_ValueCheck_Result");
                if (ctx.IsRunning) return;

                ctx.IsRunning = true;

                _ = Task.Run(async () =>
                {
                    while (ctx.IsRunning)
                    {
                        var sw = Stopwatch.StartNew();
                        ctx.PollAction(args);

                        await Task.Delay(1000);
                        sw.Stop();
                        Console.WriteLine($"Channel {startCeid} 执行时间: {sw.ElapsedMilliseconds} ms");
                    }
                });
            }
            else if (ceid == stopCeid)
            {
                ctx.IsRunning = false;
                LogSVIDErrorAsync(ceid, $"Received Stop CEID: {ceid}", "SVID_ValueCheck_Result");
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

        /// <summary>
        /// 设备控制状态Check
        /// </summary>
        /// <param name="args"></param>
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
                            {
                                eqp.Value.ControlStatus = ControlState.EQPOFFLINE;
                                var controlState = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][1]).ToString());
                                MESClient.Send_ControlState(eqpId, (ControlState)controlState);
                                if (controlState != 1 && controlState != 2 && controlState != 3)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report EQPOFFLINE Event,CEID={ceid},But SVID ControlState={controlState}  ");
                                }
                                break;
                            }
                        case 9:
                            {
                                eqp.Value.ControlStatus = ControlState.LOCAL;
                                var controlState = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][1]).ToString());
                                MESClient.Send_ControlState(eqpId, (ControlState)controlState);
                                if (controlState != 4)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report LOCAL Event,CEID={ceid},But SVID ControlState ={controlState}  ");
                                }
                                args.Header[CItems.Check] = false;
                                Send_S1F3_Get_AccessMode(args);
                                //Send_S1F3_Get_AllDifferenceSVID(args);
                                Send_S1F3_Get_TransferStatus(args);
                                break;
                            }

                        case 10:
                            {

                                //LoadSVIDDef();
                                //Send_S1F3_Get_AllDifferenceSVID(args);
                                //while (true)
                                //{
                                //    Stopwatch sw = Stopwatch.StartNew();
                                //    Send_S1F3_Get_AllDifferenceSVIDValue(args);

                                //    Thread.Sleep(1020);
                                //    sw.Stop();
                                //    Console.WriteLine($"执行时间：{sw.ElapsedMilliseconds} ms \r\n");
                                //}

                                //eqp.Value.ControlStatus = ControlState.REMOTE;
                                //if (!eqp.Value.IsInitial)
                                //{
                                //    await HsmsInitial(args);
                                //    eqp.Value.IsInitial = true;
                                //}
                                //var controlState = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][1]).ToString());
                                //MESClient.Send_ControlState(eqpId, (ControlState)controlState);
                                //if (controlState != 5)
                                //{
                                //    MESClient.Send_Exception(eqpId, "0", $"EQP Report REMOTE Event,CEID={ceid},But SVID ControlState ={controlState}  ");
                                //}
                                //args.Header[CItems.Check] = false;
                                //Send_S1F3_Get_TransferStatus(args);
                                //Send_S1F3_Get_AllDifferenceSVID(args);
                                //while (true)
                                //{
                                //    Send_S1F3_Get_AllDifferenceSVIDValue(args);

                                //    Thread.Sleep(1000);
                                //}
                                //var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
                                //if (context.transactions != null)
                                //{
                                //    foreach (var trans in context.transactions)
                                //    {
                                //        if (trans.Direction == "H->E" || trans.Direction == "E<-H")
                                //            await context.SecsGem.SendAsync(trans.message);
                                //    }
                                //}
                                break;
                            }

                    }
                }
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        /// <summary>
        /// LP Access 状态Check
        /// </summary>
        /// <param name="args"></param>
        public async void Receive_S6F11_LPAccessStateChange(Arguments args)
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
                var portId = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0]).ToString());
                var eqp = AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId);
                if (eqp.Value != null)
                {
                    switch (ceid)
                    {
                        case 23:
                            {
                                eqp.Value.PortInfo[portId].AccessState = AccessMode.Auto;
                                MESClient.Send_LPAcessMode(eqpId, portId.ToString(), AccessMode.Auto);
                                var accessMode = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][1][1][0]).ToString());
                                if (accessMode != 1)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report Auto Event,CEID={ceid},But SVID Aceess ={accessMode}  ");
                                }
                                break;
                            }
                        case 24:
                            {
                                eqp.Value.PortInfo[portId].AccessState = AccessMode.Manual;
                                MESClient.Send_LPAcessMode(eqpId, portId.ToString(), AccessMode.Manual);
                                var accessMode = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][1][1][0]).ToString());
                                if (accessMode != 0)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report Manual Event,CEID={ceid},But SVID Aceess ={accessMode}  ");
                                }
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }


        }

        /// <summary>
        /// LP Transfer 状态Check
        /// </summary>
        /// <param name="args"></param>
        public async void Receive_S6F11_LPTransferStateChange(Arguments args)
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
                var portId = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0]).ToString());
                var eqp = AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId);
                if (eqp.Value != null)
                {
                    switch (ceid)
                    {
                        case 70:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.TransferBlocked;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.TransferBlocked);
                                var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][1][1][0]).ToString());
                                if (transferStatus != 1)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report TransferBlocked Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
                                }
                                break;
                            }
                        case 72:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.ReadyToLoad;
                                var carrierId = eqp.Value.PortInfo[portId].CarrierId;
                                var batchs = AppCache.Current.MesBatchInfos.Where(a => a.Value.CARRIERID == carrierId);
                                if (batchs != null && batchs.Count() > 0)
                                {
                                    foreach (var batch in batchs)
                                    {
                                        AppCache.Current.MesBatchInfos.Remove(batch.Key);
                                        if (AppCache.Current.CJQuendList.Keys.Contains(batch.Key))
                                        {
                                            AppCache.Current.CJQuendList.Remove(batch.Key);
                                        }
                                    }
                                }
                                eqp.Value.PortInfo[portId].CarrierId = string.Empty;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.ReadyToLoad);
                                var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][3]).ToString());
                                if (transferStatus != 2)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report ReadyToLoad Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
                                }
                                Random random = new Random();
                                Thread.Sleep(random.Next(10000, 100000));
                                SecsMessage msg = new SecsMessage(2, 41);
                                Item paramItem = Item.L(new Item[] { Item.A("port"), Item.U1((byte)portId) });
                                msg.SecsItem = Item.L(new Item[] { Item.A("TEST_ToLoad"), paramItem });
                                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
                                var reply = context.SecsGem.SendAsync(msg);
                                break;
                            }
                        case 73:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.ReadyToUnload;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.ReadyToUnload);
                                var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][3]).ToString());
                                if (transferStatus != 3)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report ReadyToUnload Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
                                }
                                SecsMessage msg = new SecsMessage(2, 41);
                                Item paramItem = Item.L(new Item[] { Item.A("port"), Item.U1((byte)portId) });
                                msg.SecsItem = Item.L(new Item[] { Item.A("TEST_ToUnload"), paramItem });
                                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
                                var reply = context.SecsGem.SendAsync(msg);
                                break;
                            }
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
            var portstr = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][1]).ToString().Trim();
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][2]).ToString().Trim();
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
            AppCache.Current.EqpInfos[eqpId].PortInfo[portId].CarrierId = carrierId;
            MESClient.Send_MaterialRecived(eqpId, portId, carrierId);
            var mesbatchInfo = await MESClient.MES_LotInfoDownloadRequest(eqpId, carrierId);
            if (mesbatchInfo == null)
            {
                args.SetResultNG();
                return;
            }
            if (!AppCache.Current.MesBatchInfos.ContainsKey(mesbatchInfo.BATCHCHID))
            {
                AppCache.Current.MesBatchInfos.Add(mesbatchInfo.BATCHCHID, mesbatchInfo);
            }
            if (!AppCache.Current.CJQuendList.ContainsKey(mesbatchInfo.BATCHCHID))
            {
                AppCache.Current.CJQuendList.Add(mesbatchInfo.BATCHCHID, new Dictionary<string, int>());

                foreach (var lot in mesbatchInfo.LOTLIST)
                {
                    AppCache.Current.CJQuendList[mesbatchInfo.BATCHCHID].Add(lot.LOT_ID, 0);
                }
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
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][2]).ToString().Trim();
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
            if (rootItem.Items[2][0][1][3].Items.Length == 25)
            {
                foreach (var item in rootItem.Items[2][0][1][3].Items)
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

                        if (slotMap[wafer.SLOT - 1] != 3)
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
                args.Header[CItems.SlotMap] = slotMap;
                args.Header[CItems.BatchInfo] = batchInfo;
                await Send_S3F17_SecondProcessWithCarrier(args);
                if (args.GetResultCode() != "OK")
                    Send_S3F17_CancelCarrier(args);
            }
            else
            {
                batchInfo = new MESLotInfoDownloadReply() { LOTLIST = new List<LotInfo>() };

                LotInfo lotInfo = new LotInfo();
                lotInfo.LOT_ID = "EAPTEST" + carrierId;
                lotInfo.WAFERS = new List<Wafer>();
                for (int m = 1; m <= 25; m++)
                {
                    slotMap.Add(3);
                    lotInfo.WAFERS.Add(new Wafer() { WAFER_ID = "EAPTEST" + carrierId + m.ToString(), SLOT = m });
                }
                batchInfo.LOTLIST.Add(lotInfo);
                args.Header[CItems.SlotMap] = slotMap;
                args.Header[CItems.BatchInfo] = batchInfo;
                await Send_S3F17_SecondProcessWithCarrier(args);
                //args.SetResultNG();
                //Send_S3F17_CancelCarrier(args);
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
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][2]).ToString().Trim();
            var portId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0]).ToString().Trim();
            args.Header[CItems.CarrierId] = carrierId;
            args.Header[CItems.PortId] = portId;
            var res = await MESClient.MES_TrackIn(eqpId, carrierId);
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
            string SubstLocSubstLocID = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][10]).ToString().Trim();
            var carrierId = SubstLocSubstLocID.Split('.')[0];
            var slot = SubstLocSubstLocID.Split('.')[1];
            var portId = AppCache.Current.EqpInfos[eqpId].PortInfo.Values.FirstOrDefault(a => a.CarrierId == carrierId).PortNo.ToString();
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
            var pjid = SecsItemUtil.GetItemValue(rootItem[2][0][1][1]).ToString();
            bool creatCJ = true;
            var cjid = string.Empty;
            foreach (var cj in AppCache.Current.CJQuendList)
            {
                if (cj.Value.ContainsKey(pjid))
                {
                    cjid = cj.Key;
                    cj.Value[pjid] = 1;

                    foreach (var pj in cj.Value.Values)
                    {
                        if (pj == 0)
                            creatCJ = false;
                    }
                }
            }
            if (creatCJ)
            {
                var batchInfo = AppCache.Current.MesBatchInfos.FirstOrDefault(a => a.Key == cjid);
                args.Header[CItems.BatchInfo] = batchInfo.Key;
                await Send_S14F9_CreateCJ(args);
            }
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
        public ControlState Send_S1F3_Get_ControlState(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var check = (bool)args.Header[CItems.Check];
            SecsMessage msg = new SecsMessage(1, 3);
            Item[] items = new Item[] { Item.U4(29) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = context.SecsGem.SendAsync(msg).Result;
            if (reply != null)
            {
                var res = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0]).ToString());
                if (!check)
                {
                    MESClient.Send_ControlState(eqpId, (ControlState)res);
                }
                return (ControlState)res;
            }
            return ControlState.UNKNOWN;
        }

        public void Send_S1F3_FDCTEST(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(1, 3);
            List<Item> FDCItems = new List<Item>();
            for (int i = 9; i <= 24; i++)
            {
                if (i != 12 && i != 16 && i != 20 && i != 24)
                {
                    List<Item> items = new List<Item>() {
                        Item.U4((uint)((1100+i)*10000+112)),
                        Item.U4((uint)((1100+i)*10000+123)),
                        Item.U4((uint)((1100+i)*10000+124)),
                        Item.U4((uint)((1100+i)*10000+132)),
                        Item.U4((uint)((1100+i)*10000+133)),
                        Item.U4((uint)((1100+i)*10000+141)),
                        Item.U4((uint)((1100+i)*10000+143)),
                        Item.U4((uint)((1100+i)*10000+151)),
                        Item.U4((uint)((1100+i)*10000+152)),
                        Item.U4((uint)((1100+i)*10000+153)),
                        Item.U4((uint)((1100+i)*10000+154)),
                        Item.U4((uint)((1100+i)*10000+157)),
                        Item.U4((uint)((1100+i)*10000+160)),
                        Item.U4((uint)((1100+i)*10000+161)),
                        Item.U4((uint)((1100+i)*10000+162)),
                        Item.U4((uint)((1100+i)*10000+164)),
                        Item.U4((uint)((1100+i)*10000+170)),
                        Item.U4((uint)((1100+i)*10000+171)),
                        Item.U4((uint)((1100+i)*10000+172)),
                        Item.U4((uint)((1100+i)*10000+174)),
                        Item.U4((uint)((1100+i)*10000+206)),
                        Item.U4((uint)((1100+i)*10000+207)),
                        Item.U4((uint)((1100+i)*10000+208)),
                        Item.U4((uint)((1100+i)*10000+209)),
                        Item.U4((uint)((1100+i)*10000+210)),
                        Item.U4((uint)((1100+i)*10000+211)),
                        Item.U4((uint)((1100+i)*10000+212)),
                        Item.U4((uint)((1100+i)*10000+213)),
                        Item.U4((uint)((1100+i)*10000+214)),
                        Item.U4((uint)((1100+i)*10000+216)),
                        Item.U4((uint)((1100+i)*10000+217)),
                        Item.U4((uint)((1100+i)*10000+112)),
                    };
                    FDCItems.AddRange(items);
                }
            }
            msg.SecsItem = Item.L(FDCItems.ToArray());
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            while (true)
            {
                var reply = context.SecsGem.SendAsync(msg).Result;
                Thread.Sleep(1000);
            }
        }


        public List<AccessMode> Send_S1F3_Get_AccessMode(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var check = (bool)args.Header[CItems.Check];
            SecsMessage msg = new SecsMessage(1, 3);
            Item[] items = new Item[] { Item.U4(164) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = context.SecsGem.SendAsync(msg).Result;
            if (reply != null)
            {
                var access1 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][0]).ToString());
                var access2 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][1]).ToString());
                var access3 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][2]).ToString());
                var access4 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][3]).ToString());
                if (!check)
                {
                    MESClient.Send_LPAcessMode(eqpId, "1", (AccessMode)access1);
                    MESClient.Send_LPAcessMode(eqpId, "2", (AccessMode)access2);
                    MESClient.Send_LPAcessMode(eqpId, "3", (AccessMode)access3);
                    MESClient.Send_LPAcessMode(eqpId, "4", (AccessMode)access4);
                }
                List<AccessMode> accessModes = new List<AccessMode>();
                accessModes.Add((AccessMode)access1);
                accessModes.Add((AccessMode)access2);
                accessModes.Add((AccessMode)access3);
                accessModes.Add((AccessMode)access4);
                return accessModes;
            }
            else
            {
                return null;
            }
        }
        public List<TransferStatus> Send_S1F3_Get_TransferStatus(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var check = (bool)args.Header[CItems.Check];
            SecsMessage msg = new SecsMessage(1, 3);
            Item[] items = new Item[] { Item.U4(163) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = context.SecsGem.SendAsync(msg).Result;
            if (reply != null)
            {
                var transfer1 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][0]).ToString());
                var transfer2 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][1]).ToString());
                var transfer3 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][2]).ToString());
                var transfer4 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][3]).ToString());
                if (!check)
                {
                    MESClient.Send_LPTransferStatus(eqpId, "1", (TransferStatus)transfer1);
                    MESClient.Send_LPTransferStatus(eqpId, "2", (TransferStatus)transfer2);
                    MESClient.Send_LPTransferStatus(eqpId, "3", (TransferStatus)transfer3);
                    MESClient.Send_LPTransferStatus(eqpId, "4", (TransferStatus)transfer4);
                }
                List<TransferStatus> transferStatuses = new List<TransferStatus>();
                transferStatuses.Add((TransferStatus)transfer1);
                transferStatuses.Add((TransferStatus)transfer2);
                transferStatuses.Add((TransferStatus)transfer3);
                transferStatuses.Add((TransferStatus)transfer4);
                return transferStatuses;
            }
            else
            {
                return null;
            }
        }

        public void Send_S1F3_Get_AllDifferenceSVID(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

            foreach (var kv in dict)
            {
                int svid = kv.Key;
                string definedType = kv.Value;

                SecsMessage msg = new SecsMessage(1, 3);
                msg.SecsItem = Item.L(Item.U4((uint)svid));   // 发送 SVID

                SecsMessage reply = null;

                try
                {
                    reply = context.SecsGem.SendAsync(msg).Result;
                }
                catch (Exception ex)
                {
                    LogSVIDError(svid, $"Send S1F3 failed: {ex.Message}", "SVID_Check_Result");
                    continue;
                }

                if (reply == null || reply.SecsItem == null || reply.SecsItem.Count == 0)
                {
                    LogSVIDError(svid, $"Empty S1F3 reply", "SVID_Check_Result");
                    continue;
                }

                var item = reply.SecsItem[0];
                var actualFormat = item.Format.ToString();

                switch (actualFormat)
                {
                    case "ASCII":
                        actualFormat = "A";
                        continue;
                    case "List":
                        actualFormat = "L";
                        continue;
                }

                if (!string.Equals(definedType, actualFormat, StringComparison.OrdinalIgnoreCase))
                {
                    LogSVIDError(svid, $"Format mismatch: CSV={definedType}, Actual={actualFormat}", "SVID_Check_Result");
                }

                if (item.Format != SecsFormat.List)
                {
                    if (item.Count > 0)
                    {
                        try
                        {
                            var rawValue = SecsItemUtil.GetItemValue(item)?.ToString();

                            if (int.TryParse(rawValue, out int value))
                            {
                                if (value == -1)
                                {
                                    LogSVIDError(svid, "Value = -1", "SVID_Check_Result");
                                }
                            }
                            else
                            {
                                LogSVIDError(svid, $"Cannot parse value: {rawValue}", "SVID_Check_Result");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogSVIDError(svid, $"Value parse error: {ex.Message}", "SVID_Check_Result");
                        }
                    }
                    else
                    {
                        LogSVIDError(svid, "Item has no children", "SVID_Check_Result");
                    }
                }
            }
        }

        public void Send_S1F3_Get_AllDifferenceSVIDValue(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            bool first = false;

            if (dict.Count == 0)
            {
                return;
            }
            SecsMessage msg = new SecsMessage(1, 3);
            nextVIDList.Clear();
            var item1 = new Item[L2CH1FlwSvidDict.Count];
            int i = 0;
            foreach (var chItem in L2CH1FlwSvidDict)
            {
                int svid = chItem.Key;
                item1[i] = Item.U4((uint)svid);
                i++;
            }
            msg.SecsItem = Item.L(item1);
            SecsMessage reply = null;
            try
            {
                reply = context.SecsGem.SendAsync(msg).Result;
            }
            catch (Exception ex)
            {
                LogSVIDError(0, $"Send S1F3 failed: {ex.Message}, {msg.SecsItem}", "SVID_ValueCheck_Result");
            }

            if (reply == null || reply.SecsItem == null || reply.SecsItem.Count == 0)
            {
                LogSVIDError(0, $"Empty S1F3 reply, {msg.SecsItem}", "SVID_ValueCheck_Result");
            }

            for (int j = 0; j < reply.SecsItem.Items.Length; j++)
            {
                Item resItem = reply.SecsItem.Items[j];
                var actualFormat = resItem.Format.ToString();
                string svidTemp = SecsItemUtil.GetItemValue(item1[j])?.ToString();
                if (int.TryParse(svidTemp, out int svid))
                {
                    if (resItem.Count > 0)
                    {
                        try
                        {
                            var rawValue = SecsItemUtil.GetItemValue(resItem)?.ToString();
                            if (!currentVIDList.Any(item => item.VID == svid))
                            {
                                first = true;
                                currentVIDList.Add(new VIDItem()
                                {
                                    VID = svid,
                                    type = actualFormat,
                                    value = rawValue
                                });
                            }
                            else
                            {
                                first = false;
                            }

                            nextVIDList.Add(new VIDItem()
                            {
                                VID = svid,
                                type = actualFormat,
                                value = rawValue
                            });
                        }
                        catch (Exception ex)
                        {
                            LogSVIDError(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
                        }
                    }
                    else
                    {
                        //LogSVIDError(svid, "Item has no children", "SVID_ValueCheck_Result");
                    }
                }
            }

            if (!first)
            {
                foreach (var kv in currentVIDList)
                {
                    if (nextVIDList.Any(item => item.VID == kv.VID))
                    {
                        VIDItem vidItem = nextVIDList.FirstOrDefault(item => item.VID == kv.VID);

                        if (kv.value == "0" || vidItem.value == "0")
                        {
                            kv.value = vidItem.value;
                            continue;
                        }
                        if (string.Equals(kv.value, vidItem.value, StringComparison.OrdinalIgnoreCase))
                        {
                            LogSVIDError(kv.VID, $"Value match: last={kv.value}, current={vidItem.value}, name={allSvidDict[kv.VID].name}", "SVID_ValueCheck_Result");
                        }
                        else
                        {
                            kv.value = vidItem.value;
                        }
                    }
                    else
                    {
                        LogSVIDError(kv.VID, "第一次查询到的SVID，本次查不到值", "SVID_ValueCheck_Result");
                    }
                }
            }
        }

        public void Send_S1F3_GetL2CH1_AllDifferenceSVIDValue(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

            if (dict.Count == 0)
            {
                return;
            }

            foreach (var vidItem in L2CH1FlwSvidDict)
            {
                int svid = vidItem.Key;
                string definedType = vidItem.Value.type;

                SecsMessage msg = new SecsMessage(1, 3);
                msg.SecsItem = Item.L(Item.U4((uint)svid));

                SecsMessage reply = null;

                try
                {
                    reply = context.SecsGem.SendAsync(msg).Result;
                }
                catch (Exception ex)
                {
                    LogSVIDError(svid, $"Send S1F3 failed: {ex.Message}", "SVID_ValueCheck_Result");
                    continue;
                }

                if (reply == null || reply.SecsItem == null || reply.SecsItem.Count == 0)
                {
                    LogSVIDError(svid, $"Empty S1F3 reply", "SVID_ValueCheck_Result");
                    continue;
                }

                var item = reply.SecsItem[0];

                //if (item.Format != SecsFormat.List)
                {
                    if (item.Count > 0)
                    {
                        try
                        {
                            var rawValue = SecsItemUtil.GetItemValue(item)?.ToString();

                            //如果前后值相等就打印
                            if (L2CH1FlwSvidDict[svid].value == rawValue)
                            {
                                LogSVIDError(svid, $"Value mismatch: current={L2CH1FlwSvidDict[svid].value}, next={rawValue}", "SVID_ValueCheck_Result");
                            }
                            else
                            {
                                L2CH1FlwSvidDict[svid].value = rawValue;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogSVIDError(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
                        }
                    }
                    else
                    {
                        //LogSVIDError(svid, "Item has no children", "SVID_ValueCheck_Result");
                    }
                }
            }
        }

        public void Send_S1F3_GetL2CH2_AllDifferenceSVIDValue(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

            if (dict.Count == 0)
            {
                return;
            }

            foreach (var vidItem in L2CH2FlwSvidDict)
            {
                int svid = vidItem.Key;
                string definedType = vidItem.Value.type;

                SecsMessage msg = new SecsMessage(1, 3);
                msg.SecsItem = Item.L(Item.U4((uint)svid));

                SecsMessage reply = null;

                try
                {
                    reply = context.SecsGem.SendAsync(msg).Result;
                }
                catch (Exception ex)
                {
                    LogSVIDError(svid, $"Send S1F3 failed: {ex.Message}", "SVID_ValueCheck_Result");
                    continue;
                }

                if (reply == null || reply.SecsItem == null || reply.SecsItem.Count == 0)
                {
                    LogSVIDError(svid, $"Empty S1F3 reply", "SVID_ValueCheck_Result");
                    continue;
                }

                var item = reply.SecsItem[0];

                //if (item.Format != SecsFormat.List)
                {
                    if (item.Count > 0)
                    {
                        try
                        {
                            var rawValue = SecsItemUtil.GetItemValue(item)?.ToString();

                            //如果前后值相等就打印
                            if (L2CH2FlwSvidDict[svid].value == rawValue)
                            {
                                LogSVIDError(svid, $"Value mismatch: current={L2CH2FlwSvidDict[svid].value}, next={rawValue}", "SVID_ValueCheck_Result");
                            }
                            else
                            {
                                L2CH2FlwSvidDict[svid].value = rawValue;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogSVIDError(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
                        }
                    }
                    else
                    {
                        //LogSVIDError(svid, "Item has no children", "SVID_ValueCheck_Result");
                    }
                }
            }
        }

        private void LogSVIDError(int svid, string msg, string fileName)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string logFile = Path.Combine(desktop, $"{fileName}.csv");

                // 如果文件不存在，写入表头
                if (!File.Exists(logFile))
                {
                    File.WriteAllText(logFile, "Time,SVID,Message\r\n");
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{svid},\"{msg}\"\r\n";

                File.AppendAllText(logFile, line);
            }
            catch (Exception ex)
            {
                // 最终兜底，避免因为日志写失败影响主流程
                Console.WriteLine($"[LogSVIDError FAILED] {ex.Message}");
            }
        }

        string path = @"D:\projects\S25017\CTC\Bin\Module_Gem\conf\ecs_sv_def.csv";
        Dictionary<int, string> dict = new Dictionary<int, string>();
        Dictionary<int, VIDItem> allSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L2CH1FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L2CH2FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L2CH3FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L2CH4FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R2CH1FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R2CH2FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R2CH3FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R2CH4FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3CH1FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3CH2FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3CH3FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3CH4FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3CH1FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3CH2FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3CH3FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3CH4FlwSvidDict = new Dictionary<int, VIDItem>();
        List<VIDItem> currentVIDList = new List<VIDItem>();
        List<VIDItem> nextVIDList = new List<VIDItem>();

        public void LoadSVIDDef()
        {
            dict.Clear();
            if (!File.Exists(path))
            {
                return;
            }
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                // 跳过空行或表头
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("id,"))
                    continue;

                var parts = line.Split(',');

                // 必须至少包含 id 和 type 字段
                if (parts.Length < 3)
                    continue;

                // 解析 ID
                if (int.TryParse(parts[0], out int id))
                {
                    string type = parts[2].Trim();  // type 列
                    dict[id] = type;
                    allSvidDict[id] = new VIDItem()
                    {
                        VID = id,
                        type = type,
                        name = parts[1]
                    };

                    //currentVIDList.Add(new VIDItem()
                    //{
                    //    VID = id,
                    //    type = type,
                    //    value = "0"
                    //});
                    //nextVIDList.Add(new VIDItem()
                    //{
                    //    VID = id,
                    //    type = type,
                    //    value = "0"
                    //});
                }
            }
            foreach (var svid in allSvidDict.Values)
            {
                // 快速失败：不符合基本 FlwPV 规则直接跳过
                if (!ChannelRegex.IsMatch(svid.name))
                    continue;

                // 提取 ChannelKey（如：L2CH1）
                var channelKeyMatch = ChannelKeyRegex.Match(svid.name);
                if (!channelKeyMatch.Success)
                    continue;

                string channelKey = channelKeyMatch.Value;

                // 查找对应 Dictionary
                if (_channelSvidDictMap.TryGetValue(channelKey, out var dict))
                {
                    dict[svid.VID] = svid;
                }
            }
        }

        public static bool IsMatchL2CH1VlvFlwPV(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return Regex.IsMatch(input, @"^L3CH1_Vlv\d+FlwPV$") || Regex.IsMatch(input, @"^L3CH2_Vlv\d+FlwPV$") || Regex.IsMatch(input, @"^L3CH3_Vlv\d+FlwPV$");
        }

        public static bool IsMatchCHVlvFlwPV(string input, string matchRule)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return Regex.IsMatch(input, matchRule);
        }

        public async void Send_S1F3_Get_DataVarible(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(1, 3);
            Item[] items = new Item[] { Item.U4(163) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            if (reply != null)
            {
                var transfer1 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][0]).ToString());
                var transfer2 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][1]).ToString());
                var transfer3 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][2]).ToString());
                var transfer4 = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[0][4]).ToString());
                MESClient.Send_LPTransferStatus(eqpId, "1", (TransferStatus)transfer1);
                MESClient.Send_LPTransferStatus(eqpId, "2", (TransferStatus)transfer2);
                MESClient.Send_LPTransferStatus(eqpId, "3", (TransferStatus)transfer3);
                MESClient.Send_LPTransferStatus(eqpId, "4", (TransferStatus)transfer4);
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
                if (reply != null)
                {
                    byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem);
                    if (retCode == null || retCode != 0)
                    {
                        args.SetResultNG("Send S2F37, Equipment Reject.");
                        return;
                    }
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
                if (reply != null)
                {
                    byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem);
                    if (retCode == null || retCode != 0)
                    {
                        args.SetResultNG("Send S2F35, Equipment Reject.");
                        return;
                    }
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
                if (reply != null)
                {
                    byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem);
                    if (retCode == null || retCode != 0)
                    {
                        args.SetResultNG("Send S2F33, Equipment Reject.");
                        return;
                    }
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
            if (reply != null)
            {
                int retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
                if (retCode != 0)
                {
                    args.SetResultNG($"Send_S3F17_FirstProcessWithCarrierAsync, Equipment Reject,Return Code:{retCode}.");
                    return;
                }
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
            for (int i = 0; i < 25; i++)
            {
                slotItems[i] = Item.U1((byte)eqpSlotMap[i]);
                Wafer wafer = null;
                string LotId = string.Empty;
                foreach (var lot in batchInfo.LOTLIST)
                {
                    wafer = lot.WAFERS.FirstOrDefault(m => m.SLOT == (i + 1));
                    LotId = lot.LOT_ID;
                    if (wafer != null)
                        goto ExitLoop;
                }
            ExitLoop:
                contentItems[i] = wafer == null ? Item.L(new Item[] { Item.A(""), Item.A("") }) : Item.L(new Item[] { Item.A(LotId), Item.A(wafer.WAFER_ID) });
            }
            Item mapItem = Item.L();
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId), Item.U1((byte)portId), Item.L(new Item[] { Item.L(Item.A("ContentMap"), Item.L(contentItems)), Item.L(Item.A("SlotMap"), Item.L(slotItems)) }) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            if (reply != null)
            {
                int retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
                if (retCode != 0)
                {
                    args.SetResultNG($"Send_S3F17_SecondProcessWithCarrierAsync, Equipment Reject,Return Code:{retCode}.");
                    return;
                }
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
            if (reply != null)
            {
                int retCode = (int)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
                if (retCode != 0)
                {
                    args.SetResultNG($"Send_S3F17_CarrierRelease, Equipment Reject,Return Code:{retCode}.");
                    return;
                }
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
            if (reply != null)
            {
                int retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
                if (retCode != 0)
                {
                    args.SetResultNG($"Send_S3F17_CancelCarrier, Equipment Reject,Return Code:{retCode}.");
                    return;
                }
            }
            args.SetResultOK();
        }

        public async Task<List<string>> Send_S7F19_GetRecipeList(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(7, 19);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            List<string> recipeList = new List<string>();
            if (reply != null)
            {
                var count = reply.SecsItem.Items.Count();

                for (int i = 0; i < count; i++)
                {
                    recipeList.Add(SecsItemUtil.GetItemValue(reply.SecsItem.Items[i]).ToString());
                }
                args.SetResultOK();
                return recipeList;
            }
            else
            {
                args.SetResultNG();
                return recipeList;
            }
        }

        public async Task Send_S16F21_SelectPJSpace(Arguments args)
        {
            var EqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(16, 21);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            if (reply != null)
            {
                int retCode = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem).ToString());
                if (retCode <= 0)
                {
                    args.SetResultNG();
                    return;
                }
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
            MESClient.Send_LPLotId(EqpId, portId, string.Join(';', batchInfo.LOTLIST.Select(u => u.LOT_ID).ToList()));
            foreach (var lot in batchInfo.LOTLIST)
            {
                var pjIdItem = Item.A(lot.LOT_ID);
                var meatItem = Item.B((byte)13);
                var slotItems = new Item[lot.WAFERS.Count];
                int i = 0;
                foreach (var wafer in lot.WAFERS)
                {
                    slotItems[i] = Item.U1((byte)wafer.SLOT);
                    slots.Add(wafer.SLOT);
                    i++;
                }
                var carrierSpecItem = Item.L(new Item[] { Item.L(new Item[] { Item.A(carrierId), Item.L(slotItems) }) });
                var recipeSpecItem = Item.L(new Item[] { Item.U1((byte)1), Item.A(lot.PPID), Item.L() });
                var startItem = Item.Boolean(true);
                var pauseEventItem = Item.L();

                pjItems[index] = Item.L(new Item[] { pjIdItem, meatItem, carrierSpecItem, recipeSpecItem, startItem, pauseEventItem });
                index++;
            }
            msg.SecsItem = Item.L(new Item[] { Item.U1((byte)0), Item.L(pjItems) });
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            if (reply != null)
            {
                var res = (bool)SecsItemUtil.GetItemValue(reply.SecsItem[1][0]);
                if (!res)
                {
                    args.SetResultNG();
                    return;
                }
                MESClient.Send_MapResult(EqpId, portId.ToString(), string.Join(';', slots) + ";");
            }
            args.SetResultOK();
        }

        public async Task Send_S14F9_CreateCJ(Arguments args)
        {

            var batchId = args.Header[CItems.BatchInfo].ToString();
            var batchInfo = AppCache.Current.MesBatchInfos[batchId];
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = batchInfo.CARRIERID;
            var portId = AppCache.Current.EqpInfos[EqpId].PortInfo.Values.FirstOrDefault(a => a.CarrierId == carrierId).PortNo.ToString();
            SecsMessage msg = new SecsMessage(14, 9);
            Item[] pjItems = new Item[batchInfo.BATCHSIZE];
            int index = 0;
            List<int> slots = new List<int>();
            foreach (var lot in batchInfo.LOTLIST)
            {
                foreach (var wafer in lot.WAFERS)
                {
                    slots.Add(wafer.SLOT);
                }
                Item pjItem = Item.L(new Item[] { Item.A(lot.LOT_ID), Item.L(), Item.L() });
                pjItems[index] = pjItem;
                index++;
            }
            Item sourceItem = Item.L(new Item[slots.Count]);
            Item destItem = Item.L(new Item[slots.Count]);
            int slotindex = 0;
            foreach (var slot in slots)
            {
                sourceItem.Items[slotindex] = Item.U1((byte)slot);
                destItem.Items[slotindex] = Item.U1((byte)slot);
                slotindex++;
            }
            var destcarrierId = string.Empty;
            switch (portId)
            {
                case "1":
                    destcarrierId = AppCache.Current.EqpInfos[EqpId].PortInfo[2].CarrierId;
                    break;
                case "2":
                    destcarrierId = AppCache.Current.EqpInfos[EqpId].PortInfo[1].CarrierId;
                    break;
                case "3":
                    destcarrierId = AppCache.Current.EqpInfos[EqpId].PortInfo[4].CarrierId;
                    break;
                case "4":
                    destcarrierId = AppCache.Current.EqpInfos[EqpId].PortInfo[3].CarrierId;
                    break;
            }

            var MtrlOutSpecItem = Item.L(new Item[] { Item.L(new Item[]{
             Item.L(new Item[]{Item.A(batchInfo.CARRIERID), sourceItem }),
              Item.L(new Item[]{Item.A(destcarrierId) ,destItem  }),
             }) });
            Item cjItem = Item.L(new Item[] {
                Item.L(new Item[] {Item.A("ObjID"),Item.A("ControlJob101"+carrierId)}),
                Item.L(new Item[]{Item.A("CarrierInputSpec"),Item.L(new Item[] { Item.A(batchInfo.CARRIERID)}) }),
                Item.L(new Item[]{Item.A("ProcessingCtrlSpec"),Item.L(pjItems) }),
                Item.L(new Item[] {Item.A("ProcessOrderMgmt"),Item.U1((byte)3)}),
                Item.L(new Item[] {Item.A("StartMethod"),Item.Boolean(true)}),
                Item.L(new Item[] {Item.A("MtrlOutSpec"),Item.L()}),
                Item.L(new Item[] {Item.A("PauseEvent"),Item.L()}),
            });
            msg.SecsItem = Item.L(new Item[] { Item.A("Equipment"), Item.A("ControlJob"), cjItem });
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await context.SecsGem.SendAsync(msg);
            if (reply != null)
            {
                var res = int.Parse(SecsItemUtil.GetItemValue(reply.SecsItem[2][0]).ToString());
                if (res != 0)
                {
                    args.SetResultNG();
                    return;
                }
            }
            args.SetResultOK();
        }
        #endregion Send Message

        #region Others
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
            MESClient.Send_EqpState(eqpId, EQPState.DisConnected);
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
                        if (args.GetResultCode() != "OK") return;
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
            catch (Exception ex)
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
                        if (vid >= 20000 && vid <= 40000)
                        {
                            string ch = vid.ToString().Substring(1, 2);
                            ch = ch.TrimStart('0');
                            if (!ChamberList.Contains(ch))
                            {
                                dataReportDic.Remove(rptid);
                                break;
                            }

                            //var flag = (vid - 20000) % 100;
                            //if (flag == 0 || flag == 1 || flag == 2 || flag == 3 || flag == 4 || flag == 5 || flag == 6 || flag == 7)
                            //{
                            //    var chIndex = (vid - flag - 20000) / 100;
                            //    if (!ChamberList.Contains(chIndex.ToString()))
                            //    {
                            //        dataReportDic.Remove(rptid);
                            //        break;
                            //    }
                            //}
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

                    if (ceid >= 20000 && ceid < 40000)
                    {
                        string ch = ceid.ToString().Substring(1, 2);
                        ch = ch.TrimStart('0');
                        if (!ChamberList.Contains(ch))
                        {
                            continue;
                        }
                    }

                    //if (ceid >= 20000 && ceid < 30000)
                    //{
                    //    var flag = (ceid - 20000) % 100;
                    //    if (flag == 0 || flag == 1 || flag == 10 || flag == 11)
                    //    {
                    //        var chIndex = (ceid - flag - 20000) / 100;
                    //        if (!ChamberList.Contains(chIndex.ToString()))
                    //        {
                    //            continue;
                    //        }
                    //    }
                    //}
                    //else if (ceid >= 30000 && ceid < 40000)
                    //{
                    //    var flag = (ceid - 30000) % 100;
                    //    if (flag == 0 || flag == 1 || flag == 2 || flag == 3 || flag == 10 || flag == 11)
                    //    {
                    //        var chIndex = (ceid - flag - 30000) / 100;
                    //        if (!ChamberList.Contains(chIndex.ToString()))
                    //        {
                    //            continue;
                    //        }
                    //    }
                    //}

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

        public bool Heart(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == args.Header[CItems.EqpId].ToString());
            SecsMessage msg = new SecsMessage(1, 1);
            var reply = context.SecsGem.SendAsync(msg).Result;
            if (reply != null)
            {
                args.SetResultOK();
                return true;
            }
            return false;
        }

        public bool StartTest(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            EAPTestItem testItem = args.Header[CItems.TestItem] as EAPTestItem;
            var context = EAPContext.contexts.First(a => a.EquipmentID == args.Header[CItems.EqpId].ToString());
            SecsMessage message = new SecsMessage((byte)testItem.Stream, (byte)testItem.Function);
            if (!string.IsNullOrEmpty(testItem.DataItemContent))
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlNode dataNode = xmlDocument.CreateNode(XmlNodeType.Element, "DataItem", "");
                dataNode.InnerXml = testItem.DataItemContent;
                if (dataNode.ChildNodes.Count > 0 && (testItem.Direction == "H->E" || testItem.Direction == "E<-H"))
                {
                    message.SecsItem = EAPContext.GetMessageSecsItem(dataNode.ChildNodes[0]);
                }
            }
            var reply = context.SecsGem.SendAsync(message).Result;
            if (reply != null)
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlNode dataNode = xmlDocument.CreateNode(XmlNodeType.Element, "DataItem", "");
                dataNode.InnerXml = testItem.ReplyDataItemContent;
                var elementNodes = dataNode.ChildNodes.OfType<XmlElement>().ToList();
                Item replySecsItem = EAPContext.GetMessageSecsItem(elementNodes[0]);
                SECSITEM secsItem = GetSecsItemObj(elementNodes[0]);
                if (testItem.ReplyStream == reply.S && testItem.ReplyFunction == reply.F)
                {
                    var res = CheckItem(reply.SecsItem, secsItem);
                    return res;
                }
                return false;
            }
            return false;
        }

        public static SECSITEM GetSecsItemObj(XmlElement node)
        {
            SECSITEM secsItem = new SECSITEM()
            {
                Type = node.Name,
                count = int.Parse(node.GetAttribute("Count")),
                Fixed = bool.Parse(node.GetAttribute("Fixed")),
                ItemName = node.GetAttribute("ItemName"),
                Value = node.InnerText,
                ChildItems = new List<SECSITEM>()
            };
            var childNodes = node.ChildNodes.OfType<XmlElement>().ToList();
            if (childNodes.Count > 0)
            {
                foreach (var childNode in childNodes)
                {
                    var childSecsItem = GetSecsItemObj(childNode);
                    secsItem.ChildItems.Add(childSecsItem);
                }
            }
            return secsItem;
        }

        public static bool CheckItem(Item replyItem, SECSITEM secsItem)
        {
            //先校验节点格式是否对的上
            if (!CheckSecsItemFormart(replyItem.Format, secsItem.Type))
                return false;
            //非List节点
            if (replyItem.Format != SecsFormat.List)
            {
                //根据Fixed配置决定是否校验节点值
                if (secsItem.Fixed)
                {
                    //根据count决定校验方式
                    if (secsItem.count == 0) //表示不校验具体值，只需要有该节点即可
                    {
                        return true;
                    }
                    else //表示需要校验具体值，设备回复中该节点值必须和配置的一模一样
                    {
                        var replyValue = SecsItemUtil.GetItemValue(replyItem).ToString();
                        if (replyValue != secsItem.Value)
                            return false;
                        return true;
                    }
                }
                return true;
            }
            //List节点校验子节点数量
            else
            {
                if (secsItem.Fixed)
                {
                    if (secsItem.count == 9999)  //表示List下可能有N个节点，但不会是0节点
                    {
                        if (replyItem.Items.Count() > 0)
                            return true;
                        return false;
                    }
                    else if (secsItem.count == 0) //表示List下不能有节点
                    {
                        if (replyItem.Items.Count() > 0)
                            return false;
                        return true;
                    }
                    else//表示配置了具体的节点数量，设备的回复需要对的上
                    {
                        if (replyItem.Items.Count() != secsItem.count)
                            return false;
                        int i = 0;
                        foreach (var item in replyItem.Items)
                        {
                            var res = CheckItem(item, secsItem.ChildItems[i]);
                            if (!res)
                                return false;
                            i++;
                        }
                        return true;
                    }
                }
                return true;
            }
        }

        public static bool CheckSecsItemFormart(SecsFormat format, string name)
        {
            if (format.ToString() == "List" && name == "L")
                return true;
            if (format.ToString() == "Binary" && name == "B")
                return true;
            if (format.ToString() == "Boolean" && name == "BOOLEAN")
                return true;
            if (format.ToString() == name)
                return true;
            return false;
        }
        #endregion
    }


    public static class AsyncLogger
    {
        private static readonly BlockingCollection<Action> _queue = new();
        private static readonly CancellationTokenSource _cts = new();

        static AsyncLogger()
        {
            Task.Factory.StartNew(
                ProcessQueue,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private static void ProcessQueue()
        {
            try
            {
                foreach (var action in _queue.GetConsumingEnumerable(_cts.Token))
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // 单条日志失败不能影响线程
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public static void Stop()
        {
            _cts.Cancel();
            _queue.CompleteAdding();
        }

        // ================== 对外统一入口（业务层调用） ==================

        public static void SVIDError(int svid, string message, string category)
        {
            Enqueue(() =>
                WriteSVIDError(svid, message, category));
        }

        public static void Info(string message, string category)
        {
            Enqueue(() =>
                WriteInfo(message, category));
        }

        public static void Exception(Exception ex, string category)
        {
            Enqueue(() =>
                WriteException(ex, category));
        }

        // ================== 队列入队 ==================

        private static void Enqueue(Action action)
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.Add(action);
            }
        }

        private static void WriteSVIDError(int svid, string message, string category)
        {
            LogSVIDError(svid, message, category);   // 原同步方法
        }

        private static void WriteInfo(string message, string category)
        {
            LogInfo(message, category);              // 原同步方法
        }

        private static void WriteException(Exception ex, string category)
        {
            LogException(ex, category);              // 原同步方法
        }

        // ================== 你原来的同步日志方法（示意） ==================
        // ⚠️ 这三者必须是“已有的、同步的、非 AsyncLogger 内部方法”

        private static void LogSVIDError(int svid, string msg, string fileName)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string logFile = Path.Combine(desktop, $"{fileName}.csv");

                // 如果文件不存在，写入表头
                if (!File.Exists(logFile))
                {
                    File.WriteAllText(logFile, "Time,SVID,Message\r\n");
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{svid},\"{msg}\"\r\n";

                File.AppendAllText(logFile, line);
            }
            catch (Exception ex)
            {
                // 最终兜底，避免因为日志写失败影响主流程
                Console.WriteLine($"[LogSVIDError FAILED] {ex.Message}");
            }
        }

        private static void LogInfo(string message, string category)
        {
            // 日志逻辑
        }

        private static void LogException(Exception ex, string category)
        {
            // 日志逻辑
        }
    }
}
