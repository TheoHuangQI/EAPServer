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
        public string unit;
        public string ecsName;
        public string description;
        public int abnormal;
    }

    public class CH
    {
        public string name;
        public bool start;
    }

    public class EnabledAlarm
    {
        public byte ALCD;
        public uint ALID;
        public string ALTX;
    }

    public class KS_CM300
    {
        private Logger logger = Logger.Inst();
        private System.Object obj = new System.Object();
        private readonly object _alarmLock = new object();
        private readonly Dictionary<uint, EnabledAlarm> _enabledAlarmMap = new Dictionary<uint, EnabledAlarm>();
        private readonly object _connectionStateLock = new object();
        private readonly Dictionary<string, long> _connectionVersions = new Dictionary<string, long>();
        Dictionary<int, ChannelContext> _channelMap;
        Dictionary<string, Dictionary<int, VIDItem>> _channelSvidDictMap;

        static readonly Regex ChannelRegex = new Regex(@"^(L|R)\dCH\d+_Vlv\d+FlwPV$", RegexOptions.Compiled);
        static readonly Regex ChannelKeyRegex = new Regex(@"^(L|R)\dCH\d+", RegexOptions.Compiled);
        static readonly Regex ChannelKey2Regex = new Regex(@"^(L|R)\dSDS\d+", RegexOptions.Compiled);
        static readonly Dictionary<int, string> DashboardEventNames = new Dictionary<int, string>
        {
            { 8, "EV_CTRL_TO_OFFLINE" },
            { 9, "EV_CTRL_TO_LOCAL" },
            { 10, "EV_CTRL_TO_REMOTE" },
            { 14, "MaterialReceivedEvent" },
            { 15, "MaterialRemovedEvent" },
            { 23, "EV_LOADPORT_ACCESS_MODE_CHANGED_AUTO" },
            { 24, "EV_LOADPORT_ACCESS_MODE_CHANGED_Manual" },
            { 25, "EV_CARRIER_DOOR_CLOSED" },
            { 26, "EV_CARRIER_ID_READ_FAILED" },
            { 27, "EV_CARRIER_LOCATION_CHANGED" },
            { 31, "EV_CARRIER_CLAMPED" },
            { 32, "EV_CARRIER_UNCLAMPED" },
            { 33, "EV_CARRIER_TAG_RELEASED" },
            { 34, "EV_CARRIER_DOOR_OPENED" },
            { 35, "E87NoStateTrans01Carrier" },
            { 37, "EV_CARRIER_ID_WAIT_FOR_HOST" },
            { 42, "EV_CARRIER_ID_VERIFY_OK" },
            { 43, "EV_CARRIER_ID_VERIFY_FAILED" },
            { 46, "NoStateTransSotMapNotRead" },
            { 48, "EV_CARRIER_SLOTMAP_WAIT_FOR_HOST" },
            { 49, "EV_CARRIER_SLOTMAP_VERIFY_OK" },
            { 50, "EV_CARRIER_SLOTMAP_VERIFY_FAILED" },
            { 51, "E87NoStateTrans17NotAccessed" },
            { 52, "EV_CARRIER_IN_ACCESS" },
            { 53, "EV_CARRIER_COMPLETE" },
            { 54, "EV_CARRIER_STOPPED" },
            { 59, "AssocSMGoAssoc" },
            { 61, "AssocToNotAssoc" },
            { 65, "E87NoStateTrans01Service" },
            { 67, "E87InServiceTrans03OutService" },
            { 69, "E87TransferReadyTrans05ReadyTOLoadOrUnLoad" },
            { 70, "EV_LOADPORT_TRANSFER_BLOCKED" },
            { 71, "E87ReadyToUnLoadTrans07TransferBlocked" },
            { 72, "EV_LOADPORT_TRANSFER_READY_TO_LOAD" },
            { 73, "EV_LOADPORT_TRANSFER_READY_TO_UNLOAD" },
            { 74, "E87TransferBlockedTrans10TransferReady" },
            { 75, "EV_CARRIER_APPROACH_COMPLETE" },
            { 81, "SubstSMTrans01_InitToSource" },
            { 82, "SubstSMTrans02_SourceToWork" },
            { 83, "SubstSMTrans03_WorkToSource" },
            { 84, "SubstSMTrans04_WorkToWork" },
            { 85, "SubstSMTrans05_WorkToDestination" },
            { 86, "SubstSMTrans06_DestinationToWork" },
            { 87, "SubstSMTrans07_DestinationToExtinction" },
            { 88, "SubstSMTrans08_DestinationToSource" },
            { 89, "SubstSMTrans09_ProcessToEnd" },
            { 90, "SubstSMTrans10_InitToNeedProcess" },
            { 91, "SubstSMTrans11_NeedProcessToInProcess" },
            { 92, "SubstSMTrans12_InProcessToComplete" },
            { 93, "SubstSMTrans13_InProcessToNeedProcess" },
            { 94, "SubstSMTrans14_NeedProcessToComplete" },
            { 95, "SubstSMTrans16_InitToNotConfirmed" },
            { 96, "SubstSMTrans17_NotConfirmedToConfirmed" },
            { 97, "SubstSMTrans18_IDReadFailNotConfirmedToWaitingForHost" },
            { 98, "SubstSMTrans19_IDMismatchNotConfirmedToWaitingForHost" },
            { 99, "SubstSMTrans20_WaitingForHostToConfirmed" },
            { 100, "SubstSMTrans21_WaitingForHostToConfirmationFailed" },
            { 101, "UnOccupiedTrans01Occupied" },
            { 102, "OccupiedTrans02UnOccupied" },
            { 105, "E40_01_NostateToQueued" },
            { 106, "E40_02_QueuedToSettingUP" },
            { 107, "E40_03_SettingUPToWaittingForStart" },
            { 108, "E40_04_SettingUPToProcessing" },
            { 109, "E40_05_WaittingForStartToProcessing" },
            { 110, "E40_06_ProcessingToProcessComplete" },
            { 111, "E40_07_ProcessCompleteToNostate" },
            { 112, "E40_08_ExcutingToPausing" },
            { 113, "E40_09_PausingToPaused" },
            { 114, "E40_10_PauseToExecuting" },
            { 115, "E40_11_ExecutingToStopping" },
            { 116, "E40_12_PauseToStoping" },
            { 117, "E40_13_ExecutingToAborting" },
            { 118, "E40_14_StopingToAborting" },
            { 119, "E40_15_PauseToAborting" },
            { 120, "E40_16_AbortingToAborted" },
            { 121, "E40_17_StoppingToStopped" },
            { 122, "E40_18_QueuedToNostate" },
            { 123, "E94_CJ_01_NoStateToQueued" },
            { 124, "E94_CJ_02_QueuedToNoState" },
            { 125, "E94_CJ_03_QueuedToSelected" },
            { 126, "E94_CJ_04_SelectedToQueued" },
            { 127, "E94_CJ_05_SelectedToExecuting" },
            { 128, "E94_CJ_06_SelectedToWaitingforstart" },
            { 129, "E94_CJ_07_WaitingforstartToExecuting" },
            { 130, "E94_CJ_08_ExecutingToPaused" },
            { 131, "E94_CJ_09_PausedToExecuting" },
            { 132, "E94_CJ_10_ExecutingToCompleted" },
            { 133, "E94_CJ_11_ActiveToCompletedStop" },
            { 134, "E94_CJ_12_ActiveToCompletedAbort" },
            { 135, "E94_CJ_13_CompletedToNoState" },
            { 136, "SelectedCjSetEmptied" },
            { 138, "PRJobSetup" },
            { 139, "PRJobProcessing" },
            { 140, "PRJobProcessingComplete" },
            { 141, "PRJobComplete" },
            { 142, "PRJobWaitingForStart" },
            { 143, "PRJobWaitingForMaterial" },
            { 144, "PRJobStateChange" },
            { 1010, "EV_PP_CHANGED" },
            { 10005, "UnDockComplete" },
            { 10009, "DockComplete" },
            { 10010, "ECChange" },
            { 10011, "ExchangeStart" },
            { 10012, "ExchangeEnd" },
            { 11117, "EquipStateChange" }
        };

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

                // ===== SDSL3 =====
                { 23100, new ChannelContext { SvidDict = L3SDS1FlwSvidDict, PollAction = args => PollChannel(args, L3SDS1FlwSvidDict) } },
                { 23200, new ChannelContext { SvidDict = L3SDS2FlwSvidDict, PollAction = args => PollChannel(args, L3SDS2FlwSvidDict) } },
                { 23300, new ChannelContext { SvidDict = L3SDS3FlwSvidDict, PollAction = args => PollChannel(args, L3SDS3FlwSvidDict) } },
                { 23400, new ChannelContext { SvidDict = L3SDS4FlwSvidDict, PollAction = args => PollChannel(args, L3SDS4FlwSvidDict) } },

                // ===== SDSR3 =====
                { 23500, new ChannelContext { SvidDict = R3SDS1FlwSvidDict, PollAction = args => PollChannel(args, R3SDS1FlwSvidDict) } },
                { 23600, new ChannelContext { SvidDict = R3SDS2FlwSvidDict, PollAction = args => PollChannel(args, R3SDS2FlwSvidDict) } },
                { 23700, new ChannelContext { SvidDict = R3SDS3FlwSvidDict, PollAction = args => PollChannel(args, R3SDS3FlwSvidDict) } },
                { 23800, new ChannelContext { SvidDict = R3SDS4FlwSvidDict, PollAction = args => PollChannel(args, R3SDS4FlwSvidDict) } },
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

                { "L3SDS1", L3SDS1FlwSvidDict },
                { "L3SDS2", L3SDS2FlwSvidDict },
                { "L3SDS3", L3SDS3FlwSvidDict },
                { "L3SDS4", L3SDS4FlwSvidDict },

                { "R3SDS1", R3SDS1FlwSvidDict },
                { "R3SDS2", R3SDS2FlwSvidDict },
                { "R3SDS3", R3SDS3FlwSvidDict },
                { "R3SDS4", R3SDS4FlwSvidDict },
            };
        }

        public void PollChannel(Arguments args, Dictionary<int, VIDItem> svidDict)
        {
            if (svidDict.Count == 0) return;

            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

            var item1 = new Item[svidDict.Count];
            int i = 0;
            foreach (var svid in svidDict.Keys)
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
                                var vID = svidDict[svid];
                                var previousValue = vID.value;

                                if (string.Equals(previousValue, rawValue, StringComparison.OrdinalIgnoreCase) && previousValue != "0" && rawValue != "0")
                                {
                                    LogSVIDErrorAsync(svid, $"Value match: last={previousValue}, current={rawValue}", "SVID_ValueCheck_Result");
                                }

                                svidDict[svid].value = rawValue;
                                PublishSvidValue(eqpId, svid, vID.name, actualFormat, rawValue, previousValue);
                            }
                            catch (Exception ex)
                            {
                                LogSVIDErrorAsync(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSVIDErrorAsync(-1, ex.Message, "SVID_ValueCheck_Result");
            }
        }

        private static int ResolveAbnormalFlag(string previousValue, string currentValue)
        {
            if (string.Equals(currentValue, "0", StringComparison.OrdinalIgnoreCase))
            {
                return (int)VIDAbnormalType.ZeroValue;
            }

            if (string.IsNullOrWhiteSpace(previousValue))
            {
                return (int)VIDAbnormalType.FirstValue;
            }

            if (string.Equals(previousValue, currentValue, StringComparison.OrdinalIgnoreCase))
            {
                return (int)VIDAbnormalType.SameAsPrevious;
            }

            return (int)VIDAbnormalType.None;
        }

        private static void PublishSvidValue(string eqpId, int svid, string name, string type, string value, string previousValue)
        {
            MESClient.Send_SVIDValue(eqpId, new VID()
            {
                vID = svid,
                name = name,
                type = type,
                value = value,
                abnormal = ResolveAbnormalFlag(previousValue, value)
            });
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
                    // 鏃ュ織澶辫触涓嶈兘鍙嶅悜褰卞搷涓氬姟绾跨▼
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
                                case 13:  // 鏂板 S1F13 澶勭悊
                                    ReciveMsgS1F13(args);
                                    break;
                            }
                            break;
                        case 5:
                            switch (function)
                            {
                                case 1:
                                    ReciveMsgS5F1(args);
                                    break;
                                case 7:
                                    ReciveMsgS5F7(args);
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

        public async void ConnectionChange(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var state = (ConnectionState)args.Header[CItems.ConnectionState];
            var version = args.Header.ContainsKey(CItems.ConnectionVersion)
                ? Convert.ToInt64(args.Header[CItems.ConnectionVersion])
                : 0;

            lock (_connectionStateLock)
            {
                if (_connectionVersions.TryGetValue(eqpId, out var latestVersion) && version < latestVersion)
                {
                    return;
                }
                _connectionVersions[eqpId] = version;
            }

            switch (state)
            {
                case ConnectionState.Disabled:
                    HsmsDisConnect(args);
                    break;
                case ConnectionState.Connecting:
                    break;
                case ConnectionState.Connected:
                    break;
                case ConnectionState.Selecting:
                    break;
                case ConnectionState.Selected:
                    await HsmsConnectAsync(args);
                    break;
                case ConnectionState.Communicating:
                    break;
                case ConnectionState.Reconnecting:
                    HsmsDisConnect(args);
                    break;
                case ConnectionState.Retry:
                    HsmsDisConnect(args);
                    break;
                case ConnectionState.Disconnected:
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
                SendDashboardSecsEvent(args, ceid);

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
                    case 65:
                    case 67:
                    case 69:
                    case 70:
                    case 71:
                    case 72:
                    case 73:
                    case 74:
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
                //LogSVIDErrorAsync(ceid, $"Received Start CEID: {ceid}", "SVID_ValueCheck_Result");
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
                        Console.WriteLine($"Channel {startCeid} 鎵ц鏃堕棿: {sw.ElapsedMilliseconds} ms");
                    }
                });
            }
            else if (ceid == stopCeid)
            {
                ctx.IsRunning = false;
                //LogSVIDErrorAsync(ceid, $"Received Stop CEID: {ceid}", "SVID_ValueCheck_Result");
            }
        }

        private string GetDashboardEventName(int ceid)
        {
            if (DashboardEventNames.ContainsKey(ceid))
            {
                return DashboardEventNames[ceid];
            }
            return "CEID_" + ceid.ToString();
        }

        private LogType GetDashboardEventLogType(int ceid)
        {
            switch (ceid)
            {
                case 26:
                case 43:
                case 50:
                case 54:
                case 100:
                case 117:
                case 118:
                case 119:
                case 120:
                case 134:
                    return LogType.ERROR;
                case 46:
                case 70:
                case 71:
                case 112:
                case 113:
                case 115:
                case 116:
                case 121:
                case 143:
                    return LogType.WARNING;
                default:
                    return LogType.INFO;
            }
        }

        private string GetSecsTextByPath(Item item, params int[] path)
        {
            try
            {
                var current = item;
                foreach (var index in path)
                {
                    if (current == null || current.Items == null || index < 0 || index >= current.Items.Length)
                    {
                        return string.Empty;
                    }
                    current = current[index];
                }
                return SecsItemUtil.GetItemValue(current)?.ToString()?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private int NormalizePortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var text = value.Trim().ToUpperInvariant();
            if (text.StartsWith("LP"))
            {
                text = text.Substring(2);
            }

            if (int.TryParse(text, out var portId) && portId >= 1 && portId <= 4)
            {
                return portId;
            }

            return 0;
        }

        private int ExtractDashboardPortId(Item rootItem)
        {
            var candidates = new List<string>
            {
                GetSecsTextByPath(rootItem, 2, 0, 1, 0),
                GetSecsTextByPath(rootItem, 2, 0, 1, 1),
                GetSecsTextByPath(rootItem, 2, 1, 1, 0)
            };

            foreach (var candidate in candidates)
            {
                var portId = NormalizePortId(candidate);
                if (portId > 0)
                {
                    return portId;
                }
            }

            return 0;
        }

        private string ExtractDashboardCarrierId(Item rootItem)
        {
            var candidates = new List<string>
            {
                GetSecsTextByPath(rootItem, 2, 0, 1, 2),
                GetSecsTextByPath(rootItem, 2, 0, 1, 5, 0),
                GetSecsTextByPath(rootItem, 2, 0, 1, 10)
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (candidate.Contains("."))
                {
                    return candidate.Split('.')[0].Trim();
                }

                var portId = NormalizePortId(candidate);
                if (portId == 0)
                {
                    return candidate.Trim();
                }
            }

            return string.Empty;
        }

        private void SendDashboardSecsEvent(Arguments args, int ceid)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                var portId = ExtractDashboardPortId(rootItem);
                var carrierId = ExtractDashboardCarrierId(rootItem);
                var eventName = GetDashboardEventName(ceid);
                var parts = new List<string>
                {
                    "CEID=" + ceid.ToString(),
                    eventName
                };

                if (portId > 0)
                {
                    parts.Add("LP" + portId.ToString());
                }

                if (!string.IsNullOrWhiteSpace(carrierId))
                {
                    parts.Add("Carrier=" + carrierId);
                }

                MESClient.Send_Event(
                    eqpId,
                    portId > 0 ? portId.ToString() : "0",
                    ceid,
                    eventName,
                    string.Join(", ", parts),
                    GetDashboardEventLogType(ceid));
            }
            catch
            {
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

        public async void ReciveMsgS1F13(Arguments args)
        {
            try
            {
                // 鍒涘缓鍐呭眰 List (MDLN + SOFTREV)
                Item innerList = Item.L(
                    Item.A("EAPServer"),  // MDLN
                    Item.A("v1.1")         // SOFTREV
                );

                // 鍒涘缓澶栧眰 List (COMMACK + 鍐呭眰List)
                Item replyItem = Item.L(
                    Item.B(0x00),  // COMMACK = 0 (OK)
                    innerList
                );

                // 鍒涘缓 S1F14 鍥炲娑堟伅
                SecsMessage replyMsg = new SecsMessage(1, 14);
                replyMsg.SecsItem = replyItem;

                // 鍙戦€佸洖澶?
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
            try
            {
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                if (rootItem == null || rootItem.Count < 3)
                {
                    args.SetResultOK();
                    return;
                }

                var alcd = ConvertToByte(rootItem[0]);
                var alid = ConvertToUInt32(rootItem[1]);
                var altx = SecsItemUtil.GetItemValue(rootItem[2])?.ToString() ?? string.Empty;

                lock (_alarmLock)
                {
                    if (alcd == 0)
                    {
                        _enabledAlarmMap.Remove(alid);
                    }
                    else
                    {
                        _enabledAlarmMap[alid] = new EnabledAlarm
                        {
                            ALCD = alcd,
                            ALID = alid,
                            ALTX = altx
                        };
                    }
                }

                var eqpId = args.Header[CItems.EqpId].ToString();
                MESClient.Send_Alarm(eqpId, "0", alcd, alid, altx, alcd != 0);

                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public async void ReciveMsgS5F7(Arguments args)
        {
            try
            {
                Item[] alarmItems;
                lock (_alarmLock)
                {
                    alarmItems = _enabledAlarmMap.Values
                        .OrderBy(a => a.ALID)
                        .Select(a => Item.L(new Item[]
                        {
                            Item.B(a.ALCD),
                            Item.U4(a.ALID),
                            Item.A(a.ALTX ?? string.Empty)
                        }))
                        .ToArray();
                }

                var replyMsg = new SecsMessage(5, 8)
                {
                    SecsItem = Item.L(alarmItems)
                };

                await args.SecsData.TryReplyAsync(replyMsg);
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        private static byte ConvertToByte(Item item)
        {
            var value = SecsItemUtil.GetItemValue(item);
            if (value == null)
                return 0;

            if (value is byte b)
                return b;

            if (byte.TryParse(value.ToString(), out var parsed))
                return parsed;

            return 0;
        }

        private static uint ConvertToUInt32(Item item)
        {
            var value = SecsItemUtil.GetItemValue(item);
            if (value == null)
                return 0;

            if (value is uint ui)
                return ui;

            if (value is int i)
                return i < 0 ? 0u : (uint)i;

            if (uint.TryParse(value.ToString(), out var parsed))
                return parsed;

            return 0;
        }

        /// <summary>
        /// 璁惧鎺у埗鐘舵€丆heck
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
                                //await Send_S2F41(args, "go_remote");
                                //LoadSVIDDef();
                                //while (true)
                                //{
                                //    Stopwatch sw = Stopwatch.StartNew();
                                //    Send_S1F3_Get_AllDifferenceSVIDValue(args);

                                //    Thread.Sleep(150);
                                //    sw.Stop();
                                //    Console.WriteLine($"鎵ц鏃堕棿锛歿sw.ElapsedMilliseconds} ms \r\n");
                                //}

                                var controlState = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][1]).ToString());
                                eqp.Value.ControlStatus = (ControlState)controlState;
                                MESClient.Send_ControlState(eqpId, (ControlState)controlState);
                                if (controlState != 4)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report LOCAL Event,CEID={ceid},But SVID ControlState ={controlState}  ");
                                }
                                //args.Header[CItems.Check] = false;
                                //Send_S1F3_Get_AccessMode(args);
                                ////Send_S1F3_Get_AllDifferenceSVID(args);
                                //Send_S1F3_Get_TransferStatus(args);
                                break;
                            }

                        case 10:
                            {
                                //await Send_S2F41(args, "go_local");

                                //LoadSVIDDef();

                                //_ = Task.Run(async () =>
                                //{
                                //    await Task.Delay(1000);
                                //    await Send_S5F7_QueryEnabledAlarms(args);
                                //});

                                //Send_S1F3_Get_AllDifferenceSVIDValue(args);
                                //while (true)
                                //{
                                //    Stopwatch sw = Stopwatch.StartNew();
                                //    Send_S1F3_Get_AllDifferenceSVIDValue(args);
                                //    await Task.Delay(990);
                                //    sw.Stop();
                                //    Console.WriteLine($"鎵ц鏃堕棿锛歿sw.ElapsedMilliseconds} ms \r\n");
                                //}

                                var controlState = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][1]).ToString());
                                eqp.Value.ControlStatus = (ControlState)controlState;
                                MESClient.Send_ControlState(eqpId, (ControlState)controlState);
                                if (controlState != 5)
                                {
                                    MESClient.Send_Exception(eqpId, "0", $"EQP Report REMOTE Event,CEID={ceid},But SVID ControlState ={controlState}  ");
                                }
                                //else if (!eqp.Value.IsInitial)
                                //{
                                //    await HsmsInitial(args);
                                //    if (args.GetResultCode() == "OK")
                                //    {
                                //        eqp.Value.IsInitial = true;
                                //    }
                                //}
                                //args.Header[CItems.Check] = false;
                                //Send_S1F3_Get_TransferStatus(args);
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
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        /// <summary>
        /// LP Access 鐘舵€丆heck
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
                                    MESClient.Send_Exception(eqpId, portId.ToString(), $"EQP Report Auto Event,CEID={ceid},But SVID Aceess ={accessMode}  ");
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
                                    MESClient.Send_Exception(eqpId, portId.ToString(), $"EQP Report Manual Event,CEID={ceid},But SVID Aceess ={accessMode}  ");
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
        /// LP Transfer 鐘舵€丆heck
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
                var portId = ExtractDashboardPortId(rootItem);
                if (portId <= 0)
                {
                    return;
                }
                var eqp = AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId);
                if (eqp.Value != null)
                {
                    switch (ceid)
                    {
                        case 65:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.InService;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.InService);
                                break;
                            }
                        case 67:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.OutOfService;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.OutOfService);
                                break;
                            }
                        case 69:
                        case 74:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.TransferReady;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.TransferReady);
                                break;
                            }
                        case 70:
                        case 71:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.TransferBlocked;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.TransferBlocked);
                                var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][1][1][0]).ToString());
                                if (transferStatus != 1)
                                {
                                    MESClient.Send_Exception(eqpId, portId.ToString(), $"EQP Report TransferBlocked Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
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
                                    MESClient.Send_Exception(eqpId, portId.ToString(), $"EQP Report ReadyToLoad Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
                                }

                                if (!IsLoopProcessEnabled(eqpId, portId))
                                {
                                    MESClient.Send_Message(eqpId, portId.ToString(), "Loop process stopped, skip TEST_ToLoad.", LogType.INFO);
                                    break;
                                }

                                Random random = new Random();
                                Thread.Sleep(random.Next(1000, 4000));
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
                                    MESClient.Send_Exception(eqpId, portId.ToString(), $"EQP Report ReadyToUnload Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
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

            // 妫€鏌ュ惊鐜窇鐗囩姸鎬侊紙鎸塋P锛夛紝濡傛灉宸插仠姝㈡垨鏆傚仠锛屽垯閲婃斁Carrier
            if (!IsLoopProcessEnabled(eqpId, portId))
            {
                var loopState = 0;
                if (AppCache.Current.LoopProcessState.ContainsKey(eqpId) && AppCache.Current.LoopProcessState[eqpId].ContainsKey(portId))
                {
                    loopState = AppCache.Current.LoopProcessState[eqpId][portId];
                }
                var stateText = loopState == 2 ? "PAUSED" : "STOPPED";
                MESClient.Send_Message(eqpId, portId.ToString(), $"Loop process {stateText}, rejecting new lot. eqpId={eqpId}, carrierId={carrierId}", LogType.WARNING);
                args.Header[CItems.PortId] = portId;
                args.Header[CItems.CarrierId] = carrierId;
                Send_S3F17_CarrierRelease(args);
                args.SetResultNG();
                return;
            }

            var mesbatchInfo = await MESClient.MES_LotInfoDownloadRequest(eqpId, carrierId, portId.ToString());
            if (mesbatchInfo == null)
            {
                MESClient.Send_Message(eqpId, portId.ToString(), $"MES_LotInfoDownloadRequest returned null, releasing carrier. eqpId={eqpId}, carrierId={carrierId}", LogType.ERROR);
                args.Header[CItems.PortId] = portId;
                args.Header[CItems.CarrierId] = carrierId;
                Send_S3F17_CarrierRelease(args);
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

            // 鍐嶆妫€鏌ユ殏鍋滅姸鎬侊紙闃叉鍦ㄤ笅杞芥湡闂寸姸鎬佸彂鐢熸敼鍙橈級
            if (AppCache.Current.LoopProcessState.ContainsKey(eqpId) &&
                AppCache.Current.LoopProcessState[eqpId].ContainsKey(portId) &&
                AppCache.Current.LoopProcessState[eqpId][portId] == 2)
            {
                MESClient.Send_Message(eqpId, portId.ToString(), $"Loop process PAUSED after download, releasing carrier.", LogType.WARNING);
                Send_S3F17_CarrierRelease(args);
                args.SetResultNG();
                return;
            }

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
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][5][0]).ToString().Trim();
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
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][5][0]).ToString().Trim();
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

        public void Send_S7F25_GetRecipeBody(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var ppid = "TEST\\PROCESS\\lotrun";

            SecsMessage msg = new SecsMessage(7, 25);

            msg.SecsItem = Item.A(ppid);

            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = context.SecsGem.SendAsync(msg).Result;

            if (reply == null)
                return;

            // S7F26 瑙ｆ瀽...
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
                msg.SecsItem = Item.L(Item.U4((uint)svid));   // 鍙戦€?SVID

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

            if (dict.Count == 0)
            {
                return;
            }

            L2CH1FlwSvidDict.Clear();
            foreach (var item in allSvidDict)
            {
                if (IsMatchL2CH1VlvFlwPV(item.Value.name))
                {
                    L2CH1FlwSvidDict.Add(item.Key, item.Value);
                }
            }

            if (L2CH1FlwSvidDict.Count == 0)
            {
                return;
            }

            SecsMessage msg = new SecsMessage(1, 3);
            var svidKeys = L2CH1FlwSvidDict.Keys.ToArray();
            var item1 = svidKeys.Select(svid => Item.U4((uint)svid)).ToArray();
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
                return;
            }

            var previousByVid = currentVIDList.ToDictionary(item => item.VID, item => item);
            currentVIDList.Clear();

            for (var j = 0; j < reply.SecsItem.Items.Length && j < svidKeys.Length; j++)
            {
                var svid = svidKeys[j];
                var resItem = reply.SecsItem.Items[j];
                if (resItem.Count <= 0)
                {
                    continue;
                }

                try
                {
                    var rawValue = SecsItemUtil.GetItemValue(resItem)?.ToString();
                    var actualFormat = resItem.Format.ToString();
                    previousByVid.TryGetValue(svid, out var previousItem);
                    var previousValue = previousItem?.value;

                    currentVIDList.Add(new VIDItem()
                    {
                        VID = svid,
                        type = actualFormat,
                        value = rawValue
                    });

                    allSvidDict.TryGetValue(svid, out var definition);
                    PublishSvidValue(eqpId, svid, definition?.name, actualFormat, rawValue, previousValue);
                }
                catch (Exception ex)
                {
                    LogSVIDError(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
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

                if (item.Count > 0)
                {
                    try
                    {
                        var rawValue = SecsItemUtil.GetItemValue(item)?.ToString();
                        var previousValue = L2CH1FlwSvidDict[svid].value;

                        if (string.Equals(previousValue, rawValue, StringComparison.OrdinalIgnoreCase))
                        {
                            LogSVIDError(svid, $"Value mismatch: current={previousValue}, next={rawValue}", "SVID_ValueCheck_Result");
                        }
                        else
                        {
                            L2CH1FlwSvidDict[svid].value = rawValue;
                        }

                        PublishSvidValue(eqpId, svid, vidItem.Value.name, item.Format.ToString(), rawValue, previousValue);
                    }
                    catch (Exception ex)
                    {
                        LogSVIDError(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
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

                if (item.Count > 0)
                {
                    try
                    {
                        var rawValue = SecsItemUtil.GetItemValue(item)?.ToString();
                        var previousValue = L2CH2FlwSvidDict[svid].value;

                        if (string.Equals(previousValue, rawValue, StringComparison.OrdinalIgnoreCase))
                        {
                            LogSVIDError(svid, $"Value mismatch: current={previousValue}, next={rawValue}", "SVID_ValueCheck_Result");
                        }
                        else
                        {
                            L2CH2FlwSvidDict[svid].value = rawValue;
                        }

                        PublishSvidValue(eqpId, svid, vidItem.Value.name, item.Format.ToString(), rawValue, previousValue);
                    }
                    catch (Exception ex)
                    {
                        LogSVIDError(svid, $"Value get error: {ex.Message}", "SVID_ValueCheck_Result");
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

                // 濡傛灉鏂囦欢涓嶅瓨鍦紝鍐欏叆琛ㄥご
                if (!File.Exists(logFile))
                {
                    File.WriteAllText(logFile, "Time,SVID,Message\r\n");
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{svid},\"{msg}\"\r\n";

                File.AppendAllText(logFile, line);
            }
            catch (Exception ex)
            {
                // 鏈€缁堝厹搴曪紝閬垮厤鍥犱负鏃ュ織鍐欏け璐ュ奖鍝嶄富娴佺▼
                Console.WriteLine($"[LogSVIDError FAILED] {ex.Message}");
            }
        }

        string path = string.Empty;
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
        Dictionary<int, VIDItem> L3SDS1FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3SDS2FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3SDS3FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> L3SDS4FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3SDS1FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3SDS2FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3SDS3FlwSvidDict = new Dictionary<int, VIDItem>();
        Dictionary<int, VIDItem> R3SDS4FlwSvidDict = new Dictionary<int, VIDItem>();
        List<VIDItem> currentVIDList = new List<VIDItem>();

        public void LoadSVIDDef(string equipmentId = null)
        {
            dict.Clear();
            allSvidDict.Clear();
            if (_channelSvidDictMap != null)
            {
                foreach (var channelDict in _channelSvidDictMap.Values)
                {
                    channelDict.Clear();
                }
            }

            path = ResolveSvidDefinitionPath(equipmentId);
            if (!File.Exists(path))
            {
                return;
            }
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                // 璺宠繃绌鸿鎴栬〃澶?
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("id,"))
                    continue;

                var parts = ReadCsvFields(line);

                // 蹇呴』鑷冲皯鍖呭惈 id 鍜?type 瀛楁
                if (parts.Length < 3)
                    continue;

                // 瑙ｆ瀽 ID
                if (int.TryParse(parts[0], out int id))
                {
                    string type = parts[2].Trim();  // type 鍒?
                    dict[id] = type;
                    allSvidDict[id] = new VIDItem()
                    {
                        VID = id,
                        type = type,
                        name = parts[1].Trim(),
                        unit = parts.Length > 3 ? parts[3].Trim() : string.Empty,
                        ecsName = parts.Length > 5 ? parts[5].Trim() : string.Empty,
                        description = parts.Length > 6 ? parts[6].Trim() : string.Empty
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
                // 蹇€熷け璐ワ細涓嶇鍚堝熀鏈?FlwPV 瑙勫垯鐩存帴璺宠繃锛岃繕闇€瑕佹坊鍔燬DS鐨凷VID
                if (!ChannelRegex.IsMatch(svid.name) && !svid.name.Contains("SDS"))
                    continue;

                // 鎻愬彇 ChannelKey锛堝锛歀2CH1锛?
                var channelKeyMatch = ChannelKeyRegex.Match(svid.name);
                if (!channelKeyMatch.Success)
                {
                    channelKeyMatch = ChannelKey2Regex.Match(svid.name);
                    if (!channelKeyMatch.Success)
                        continue;
                }

                string channelKey = channelKeyMatch.Value;

                // 鏌ユ壘瀵瑰簲 Dictionary
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

            //return Regex.IsMatch(input, @"^R3CH1_Vlv\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH2_Vlv\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH3_Vlv\d+FlwPV$");
            //return Regex.IsMatch(input, @"^R3CH1_Ana\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH2_Ana\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH3_Ana\d+FlwPV$");
            return Regex.IsMatch(input, @"FlwPV")|| Regex.IsMatch(input, @"ComAss") || Regex.IsMatch(input, @"LifeAct") || Regex.IsMatch(input, @"Door_Open_Sns");
        }

        public static bool IsMatchCHVlvFlwPV(string input, string matchRule)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return Regex.IsMatch(input, matchRule);
        }

        public FdcSvidCollection CollectFdcSvidValues(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var request = args.Header.ContainsKey(CItems.FdcSvidCollection)
                ? args.Header[CItems.FdcSvidCollection] as FdcSvidCollection
                : null;

            request ??= new FdcSvidCollection();

            var result = new FdcSvidCollection
            {
                EQUIPMENTID = eqpId,
                SaveToHistory = request.SaveToHistory,
                MaxCount = request.MaxCount,
                ChunkSize = request.ChunkSize,
                CollectedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Return = "OK",
                Message = "SUCCESS"
            };

            try
            {
                LoadSVIDDef(eqpId);
                if (allSvidDict.Count == 0)
                {
                    result.Return = "NG";
                    result.Message = $"SVID definition file not found or empty: {path}";
                    return result;
                }

                var svids = request.RequestedSvids?
                    .Where(svid => svid > 0)
                    .Distinct()
                    .ToList() ?? new List<int>();

                if (svids.Count == 0)
                {
                    svids = allSvidDict.Keys.OrderBy(svid => svid).ToList();
                }

                if (request.MaxCount > 0)
                {
                    svids = svids.Take(request.MaxCount).ToList();
                }

                if (svids.Count == 0)
                {
                    result.Return = "NG";
                    result.Message = "No SVID requested.";
                    return result;
                }

                var chunkSize = request.ChunkSize <= 0 ? 120 : Math.Min(request.ChunkSize, 200);
                var context = EAPContext.contexts.FirstOrDefault(a => a.EquipmentID == eqpId);
                if (context == null)
                {
                    result.Return = "NG";
                    result.Message = $"No EAP context found for {eqpId}.";
                    return result;
                }

                foreach (var chunk in svids.Chunk(chunkSize))
                {
                    var items = chunk.Select(svid => Item.U4((uint)svid)).ToArray();
                    var msg = new SecsMessage(1, 3)
                    {
                        SecsItem = Item.L(items)
                    };

                    SecsMessage reply;
                    try
                    {
                        reply = context.SecsGem.SendAsync(msg).Result;
                    }
                    catch (Exception ex)
                    {
                        foreach (var svid in chunk)
                        {
                            result.Values.Add(CreateFailedSvidValue(svid, $"Send S1F3 failed: {ex.Message}"));
                        }
                        continue;
                    }

                    if (reply?.SecsItem?.Items == null || reply.SecsItem.Items.Length == 0)
                    {
                        foreach (var svid in chunk)
                        {
                            result.Values.Add(CreateFailedSvidValue(svid, "Empty S1F4 reply."));
                        }
                        continue;
                    }

                    for (var i = 0; i < chunk.Length; i++)
                    {
                        var svid = chunk[i];
                        if (i >= reply.SecsItem.Items.Length)
                        {
                            result.Values.Add(CreateFailedSvidValue(svid, "S1F4 reply item missing."));
                            continue;
                        }

                        try
                        {
                            result.Values.Add(CreateCollectedSvidValue(svid, reply.SecsItem.Items[i]));
                        }
                        catch (Exception ex)
                        {
                            result.Values.Add(CreateFailedSvidValue(svid, $"S1F4 value parse failed: {ex.Message}"));
                        }
                    }
                }

                if (!result.Values.Any(item => item.Status == "OK"))
                {
                    result.Return = "NG";
                    result.Message = "No SVID values collected.";
                }
            }
            catch (Exception ex)
            {
                result.Return = "NG";
                result.Message = ex.Message;
            }

            return result;
        }

        private FdcSvidValue CreateCollectedSvidValue(int svid, Item item)
        {
            allSvidDict.TryGetValue(svid, out var definition);
            return new FdcSvidValue
            {
                Svid = svid,
                Name = definition?.name,
                DefinedType = definition?.type,
                ActualType = NormalizeSecsFormat(item.Format.ToString()),
                Unit = definition?.unit,
                Value = FormatSecsItemValue(item),
                EcsName = definition?.ecsName,
                Description = definition?.description,
                Status = "OK",
                ErrorMessage = string.Empty
            };
        }

        private FdcSvidValue CreateFailedSvidValue(int svid, string errorMessage)
        {
            allSvidDict.TryGetValue(svid, out var definition);
            return new FdcSvidValue
            {
                Svid = svid,
                Name = definition?.name,
                DefinedType = definition?.type,
                ActualType = string.Empty,
                Unit = definition?.unit,
                Value = string.Empty,
                EcsName = definition?.ecsName,
                Description = definition?.description,
                Status = "NG",
                ErrorMessage = errorMessage
            };
        }

        private static string FormatSecsItemValue(Item item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.Format == SecsFormat.List)
            {
                return item.Items == null
                    ? "[]"
                    : "[" + string.Join(",", item.Items.Select(FormatSecsItemValue)) + "]";
            }

            return SecsItemUtil.GetItemValue(item)?.ToString() ?? string.Empty;
        }

        private static string NormalizeSecsFormat(string format)
        {
            return format switch
            {
                "ASCII" => "A",
                "List" => "L",
                _ => format
            };
        }

        private string ResolveSvidDefinitionPath(string equipmentId)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }

            var configName = string.IsNullOrWhiteSpace(equipmentId)
                ? EAPContext.ServerName ?? "AHEG07"
                : equipmentId;

            var candidates = new List<string>
            {
                Path.Combine(EAPContext.RootPath, "Configuration", "conf", configName, "ecs_sv_def.csv"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "conf", configName, "ecs_sv_def.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "conf", configName, "ecs_sv_def.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "EAPServer", "Configuration", "conf", configName, "ecs_sv_def.csv")
            };

            foreach (var root in new[] { EAPContext.RootPath, AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                var directory = new DirectoryInfo(root);
                while (directory != null)
                {
                    candidates.Add(Path.Combine(directory.FullName, "EAPServer", "Configuration", "conf", configName, "ecs_sv_def.csv"));
                    candidates.Add(Path.Combine(directory.FullName, "Configuration", "conf", configName, "ecs_sv_def.csv"));
                    directory = directory.Parent;
                }
            }

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private static string[] ReadCsvFields(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            fields.Add(current.ToString());
            return fields.ToArray();
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

        public async Task Send_S5F7_QueryEnabledAlarms(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

                SecsMessage msg = new SecsMessage(5, 7);
                var reply = await context.SecsGem.SendAsync(msg);

                if (reply == null)
                {
                    //Console.WriteLine($"[{eqpId}] S5F7 鏌ヨ澶辫触锛歋5F8 杩斿洖涓虹┖");
                    return;
                }

                var root = reply.SecsItem;
                if (root == null || root.Items == null || root.Items.Length == 0)
                {
                    //Console.WriteLine($"[{eqpId}] S5F8 鏃犲凡浣胯兘鎶ヨ");
                    return;
                }

                //Console.WriteLine($"[{eqpId}] S5F8 宸蹭娇鑳芥姤璀︽暟閲? {root.Items.Length}");
                for (int i = 0; i < root.Items.Length; i++)
                {
                    var alarmItem = root.Items[i];
                    if (alarmItem == null || alarmItem.Count < 3)
                    {
                        //Console.WriteLine($"[{eqpId}] Alarm[{i + 1}] 鏍煎紡寮傚父");
                        continue;
                    }

                    var alcdObj = SecsItemUtil.GetItemValue(alarmItem[0]);
                    var alidObj = SecsItemUtil.GetItemValue(alarmItem[1]);
                    var altxObj = SecsItemUtil.GetItemValue(alarmItem[2]);

                    var alcd = alcdObj?.ToString() ?? string.Empty;
                    var alid = alidObj?.ToString() ?? string.Empty;
                    var altx = altxObj?.ToString() ?? string.Empty;

                    //Console.WriteLine($"[{eqpId}] Alarm[{i + 1}] ALCD={alcd}, ALID={alid}, ALTX={altx}");
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Send S5F7 鏌ヨ鎶ヨ澶辫触: {ex.Message}");
            }
        }

        public async Task Send_S2F41(Arguments args, string cmd)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();

                SecsMessage msg = new SecsMessage(2, 41);

                Item rcmdItem = Item.A(cmd);
                Item emptyList = Item.L(new Item[] { });
                msg.SecsItem = Item.L(new Item[] { rcmdItem, emptyList });

                var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

                var reply = await context.SecsGem.SendAsync(msg);

                if (reply != null)
                {
                    byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
                    // 鏍￠獙杩斿洖鐮侊細0琛ㄧず鎴愬姛锛岄潪0琛ㄧず璁惧鎷掔粷
                    if (retCode != 0)
                    {
                        args.SetResultNG($"Send S2F41 {cmd}, Equipment Reject. ReturnCode: {retCode}");
                        return;
                    }
                }

                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG($"Send S2F41 (GO REMOTE) failed: {ex.Message}");
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
                int retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]);
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
            try
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
            catch (Exception ex)
            {

            }
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

        /// <summary>
        /// 妫€鏌P鏄惁鍏佽杩涜寰幆璺戠墖
        /// </summary>
        private bool IsLoopProcessEnabled(string eqpId, int portId = 0)
        {
            if (!AppCache.Current.LoopProcessState.ContainsKey(eqpId))
            {
                return true;
            }
            var portStates = AppCache.Current.LoopProcessState[eqpId];
            if (portStates == null || !portStates.ContainsKey(portId))
            {
                return true;
            }
            var state = portStates[portId];
            return state == 0;
        }

        public async Task HsmsConnectAsync(Arguments args)
        {
            var eqpId = string.Empty;
            try
            {
                //寤虹珛閫昏緫杩炴帴
                eqpId = args.Header[CItems.EqpId].ToString();
                var version = args.Header.ContainsKey(CItems.ConnectionVersion)
                    ? Convert.ToInt64(args.Header[CItems.ConnectionVersion])
                    : 0;
                var context = EAPContext.contexts?.FirstOrDefault(a => a.EquipmentID == eqpId);
                if (context == null)
                {
                    args.SetResultNG($"No EAP context found for {eqpId}.");
                    return;
                }
                if (!IsLatestConnectionVersion(eqpId, version))
                {
                    return;
                }

                // 妫€鏌ユ槸鍚﹀凡缁忚繛鎺ワ紝濡傛灉鏄垯涓嶉渶瑕佸彂閫?S1F13
                bool isAlreadyConnected = false;
                if (AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfoObj))
                {
                    isAlreadyConnected = eqpInfoObj.ConnectStatus == ConnectStatus.CONNECTED;
                }

                if (!isAlreadyConnected)
                {
                    SecsMessage msg = new SecsMessage(1, 13);
                    msg.SecsItem = Item.L();
                    var reply = await context.SecsGem.SendAsync(msg);

                    if (reply == null || reply.SecsItem == null || reply.SecsItem.Items.Length == 0)
                    {
                        args.SetResultNG("Send S1F13, Equipment Reply Empty.");
                        return;
                    }
                    byte retCode = (byte)SecsItemUtil.GetItemValue(reply.SecsItem[0]);
                    if (retCode != 0)
                    {
                        args.SetResultNG("Send S1F13, Equipment Reply Fail.");
                        return;
                    }

                    MESClient.Send_EqpState(eqpId, EQPState.Connected);

                    if (AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo))
                    {
                        eqpInfo.ConnectStatus = ConnectStatus.CONNECTED;
                    }
                }

                args.SetResultOK();
            }
            catch (TimeoutException ex)
            {
                logger.Error($"HsmsConnectAsync S1F13 timeout. eqpId={eqpId}, error={ex.Message}");
                args.SetResultNG($"Send S1F13 timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.Error($"HsmsConnectAsync failed. eqpId={eqpId}, error={ex}");
                args.SetResultNG($"HsmsConnect failed: {ex.Message}");
            }
        }

        public void HsmsDisConnect(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var version = args.Header.ContainsKey(CItems.ConnectionVersion)
                ? Convert.ToInt64(args.Header[CItems.ConnectionVersion])
                : 0;
            if (!IsLatestConnectionVersion(eqpId, version))
            {
                return;
            }
            MESClient.Send_EqpState(eqpId, EQPState.DisConnected);
            //寤虹珛閫昏緫杩炴帴
            if (AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo))
            {
                eqpInfo.ConnectStatus = ConnectStatus.DISCONNECTED;
            }
        }

        private bool IsLatestConnectionVersion(string eqpId, long version)
        {
            lock (_connectionStateLock)
            {
                return !_connectionVersions.TryGetValue(eqpId, out var latestVersion) || version >= latestVersion;
            }
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
                string path = ResolveEventReportPath(eqpId);

                if (!File.Exists(path))
                {
                    args.SetResultNG($"Event report XML not found. eqpId={eqpId}, path={path}");
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

        private string ResolveEventReportPath(string eqpId)
        {
            var fileName = EAPContext.EqpConfigures?
                .FirstOrDefault(config => config.Name == eqpId)?
                .EventReportFile;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "event-report.xml";
            }

            return Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(EAPContext.RootPath, fileName);
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
            //鍏堟牎楠岃妭鐐规牸寮忔槸鍚﹀鐨勪笂
            if (!CheckSecsItemFormart(replyItem.Format, secsItem.Type))
                return false;
            //闈濴ist鑺傜偣
            if (replyItem.Format != SecsFormat.List)
            {
                //鏍规嵁Fixed閰嶇疆鍐冲畾鏄惁鏍￠獙鑺傜偣鍊?
                if (secsItem.Fixed)
                {
                    //鏍规嵁count鍐冲畾鏍￠獙鏂瑰紡
                    if (secsItem.count == 0) //琛ㄧず涓嶆牎楠屽叿浣撳€硷紝鍙渶瑕佹湁璇ヨ妭鐐瑰嵆鍙?
                    {
                        return true;
                    }
                    else //琛ㄧず闇€瑕佹牎楠屽叿浣撳€硷紝璁惧鍥炲涓鑺傜偣鍊煎繀椤诲拰閰嶇疆鐨勪竴妯′竴鏍?
                    {
                        var replyValue = SecsItemUtil.GetItemValue(replyItem).ToString();
                        if (replyValue != secsItem.Value)
                            return false;
                        return true;
                    }
                }
                return true;
            }
            //List鑺傜偣鏍￠獙瀛愯妭鐐规暟閲?
            else
            {
                if (secsItem.Fixed)
                {
                    if (secsItem.count == 9999)  //琛ㄧずList涓嬪彲鑳芥湁N涓妭鐐癸紝浣嗕笉浼氭槸0鑺傜偣
                    {
                        if (replyItem.Items.Count() > 0)
                            return true;
                        return false;
                    }
                    else if (secsItem.count == 0) //琛ㄧずList涓嬩笉鑳芥湁鑺傜偣
                    {
                        if (replyItem.Items.Count() > 0)
                            return false;
                        return true;
                    }
                    else//琛ㄧず閰嶇疆浜嗗叿浣撶殑鑺傜偣鏁伴噺锛岃澶囩殑鍥炲闇€瑕佸鐨勪笂
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
                        // 鍗曟潯鏃ュ織澶辫触涓嶈兘褰卞搷绾跨▼
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

        // ================== 瀵瑰缁熶竴鍏ュ彛锛堜笟鍔″眰璋冪敤锛?==================

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

        // ================== 闃熷垪鍏ラ槦 ==================

        private static void Enqueue(Action action)
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.Add(action);
            }
        }

        private static void WriteSVIDError(int svid, string message, string category)
        {
            LogSVIDError(svid, message, category);   // 鍘熷悓姝ユ柟娉?
        }

        private static void WriteInfo(string message, string category)
        {
            LogInfo(message, category);              // 鍘熷悓姝ユ柟娉?
        }

        private static void WriteException(Exception ex, string category)
        {
            LogException(ex, category);              // 鍘熷悓姝ユ柟娉?
        }

        // ================== 浣犲師鏉ョ殑鍚屾鏃ュ織鏂规硶锛堢ず鎰忥級 ==================
        // 鈿狅笍 杩欎笁鑰呭繀椤绘槸鈥滃凡鏈夌殑銆佸悓姝ョ殑銆侀潪 AsyncLogger 鍐呴儴鏂规硶鈥?

        private static void LogSVIDError(int svid, string msg, string fileName)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string logFile = Path.Combine(desktop, $"{fileName}.csv");

                // 濡傛灉鏂囦欢涓嶅瓨鍦紝鍐欏叆琛ㄥご
                if (!File.Exists(logFile))
                {
                    File.WriteAllText(logFile, "Time,SVID,Message\r\n");
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{svid},\"{msg}\"\r\n";

                File.AppendAllText(logFile, line);
            }
            catch (Exception ex)
            {
                // 鏈€缁堝厹搴曪紝閬垮厤鍥犱负鏃ュ織鍐欏け璐ュ奖鍝嶄富娴佺▼
                Console.WriteLine($"[LogSVIDError FAILED] {ex.Message}");
            }
        }

        private static void LogInfo(string message, string category)
        {
            // 鏃ュ織閫昏緫
        }

        private static void LogException(Exception ex, string category)
        {
            // 鏃ュ織閫昏緫
        }
    }
}



