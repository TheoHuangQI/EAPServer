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
using System.Runtime.CompilerServices;
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
        internal ChannelPollingTask Worker { get; } = new();
        public bool IsRunning => Worker.IsRunning;
        public Dictionary<int, VIDItem> SvidDict;
        public Func<Arguments, CancellationToken, Task> PollAction;
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
        private readonly Dictionary<string, long> _connectionSessionIds = new Dictionary<string, long>();
        private readonly Dictionary<string, ConnectionState> _connectionStates = new Dictionary<string, ConnectionState>();
        private readonly Dictionary<string, long> _controlStateConnectionGenerations = new Dictionary<string, long>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _eventReportInitializationLocks = new();
        private readonly ControlStateQueryCoordinator _controlStateQueries = new();
        private readonly object _svidDefinitionLock = new();
        private readonly ConcurrentDictionary<string, (bool Found, uint ReportId, int DataIndex)> _processCompleteLocationFields = new();
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
            { 3500, "EV_LOADPORT_TRANSFER_OOS" },
            { 3510, "EV_LOADPORT_ALARM_ACCESS_MODE_ON" },
            { 3511, "EV_LOADPORT_ALARM_ACCESS_MODE_OFF" },
            { 3512, "EV_LOADPORT_ALARM_OOS_ON" },
            { 3513, "EV_LOADPORT_ALARM_OOS_OFF" },
            { 3514, "EV_LOADPORT_RESERVATION_ON" },
            { 3515, "EV_LOADPORT_RESERVATION_OFF" },
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
                { 20900, new ChannelContext { SvidDict = L2CH1FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L2CH1FlwSvidDict, token) } },
                { 21000, new ChannelContext { SvidDict = L2CH2FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L2CH2FlwSvidDict, token) } },
                { 21100, new ChannelContext { SvidDict = L2CH3FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L2CH3FlwSvidDict, token) } },
                { 21200, new ChannelContext { SvidDict = L2CH4FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L2CH4FlwSvidDict, token) } },

                // ===== R2 =====
                { 21300, new ChannelContext { SvidDict = R2CH1FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R2CH1FlwSvidDict, token) } },
                { 21400, new ChannelContext { SvidDict = R2CH2FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R2CH2FlwSvidDict, token) } },
                { 21500, new ChannelContext { SvidDict = R2CH3FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R2CH3FlwSvidDict, token) } },
                { 21600, new ChannelContext { SvidDict = R2CH4FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R2CH4FlwSvidDict, token) } },

                // ===== L3 =====
                { 21700, new ChannelContext { SvidDict = L3CH1FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3CH1FlwSvidDict, token) } },
                { 21800, new ChannelContext { SvidDict = L3CH2FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3CH2FlwSvidDict, token) } },
                { 21900, new ChannelContext { SvidDict = L3CH3FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3CH3FlwSvidDict, token) } },
                { 22000, new ChannelContext { SvidDict = L3CH4FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3CH4FlwSvidDict, token) } },

                // ===== R3 =====
                { 22100, new ChannelContext { SvidDict = R3CH1FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3CH1FlwSvidDict, token) } },
                { 22200, new ChannelContext { SvidDict = R3CH2FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3CH2FlwSvidDict, token) } },
                { 22300, new ChannelContext { SvidDict = R3CH3FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3CH3FlwSvidDict, token) } },
                { 22400, new ChannelContext { SvidDict = R3CH4FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3CH4FlwSvidDict, token) } },

                // ===== SDSL3 =====
                { 23100, new ChannelContext { SvidDict = L3SDS1FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3SDS1FlwSvidDict, token) } },
                { 23200, new ChannelContext { SvidDict = L3SDS2FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3SDS2FlwSvidDict, token) } },
                { 23300, new ChannelContext { SvidDict = L3SDS3FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3SDS3FlwSvidDict, token) } },
                { 23400, new ChannelContext { SvidDict = L3SDS4FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, L3SDS4FlwSvidDict, token) } },

                // ===== SDSR3 =====
                { 23500, new ChannelContext { SvidDict = R3SDS1FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3SDS1FlwSvidDict, token) } },
                { 23600, new ChannelContext { SvidDict = R3SDS2FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3SDS2FlwSvidDict, token) } },
                { 23700, new ChannelContext { SvidDict = R3SDS3FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3SDS3FlwSvidDict, token) } },
                { 23800, new ChannelContext { SvidDict = R3SDS4FlwSvidDict, PollAction = (args, token) => PollChannelAsync(args, R3SDS4FlwSvidDict, token) } },
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

        public async Task PollChannelAsync(Arguments args, Dictionary<int, VIDItem> svidDict,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyValuePair<int, VIDItem>[] requested;
            lock (_svidDefinitionLock)
                requested = svidDict.ToArray();
            if (requested.Length == 0) return;

            var eqpId = args.Header[CItems.EqpId].ToString();
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);

            var item1 = requested.Select(svid => Item.U4((uint)svid.Key)).ToArray();
            try
            {
                var msg = new SecsMessage(1, 3)
                {
                    SecsItem = Item.L(item1)
                };

                SecsMessage reply = null;
                try
                {
                    using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    try
                    {
                        reply = await SecsExecution.SendAsync(args, context.SecsGem, msg, transportCancellation.Token)
                            .WaitAsync(GetSecsReplyTimeout(eqpId), cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        transportCancellation.Cancel();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogSVIDErrorAsync(0, $"Send S1F3 failed: {ex.Message}, {msg.SecsItem}", "SVID_ValueCheck_Result");
                    throw;
                }

                if (reply == null || reply.SecsItem == null || reply.SecsItem.Count == 0)
                {
                    LogSVIDErrorAsync(0, $"Empty S1F3 reply, {msg.SecsItem}", "SVID_ValueCheck_Result");
                    return;
                }

                if (reply.S != 1 || reply.F != 4 || reply.SecsItem.Format != SecsFormat.List
                    || reply.SecsItem.Count != item1.Length)
                    throw new InvalidOperationException("Channel S1F3 expected S1F4 with one value per requested SVID.");

                for (int j = 0; j < reply.SecsItem.Items.Length; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
                                var vID = requested[j].Value;
                                var previousValue = vID.value;

                                if (string.Equals(previousValue, rawValue, StringComparison.OrdinalIgnoreCase) && previousValue != "0" && rawValue != "0")
                                {
                                    LogSVIDErrorAsync(svid, $"Value match: last={previousValue}, current={rawValue}", "SVID_ValueCheck_Result");
                                }

                                vID.value = rawValue;
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogSVIDErrorAsync(-1, ex.Message, "SVID_ValueCheck_Result");
                throw;
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
        public async Task SecsMsgHandler(Arguments args)
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
                                    await ReciveMsgS1F1(args);
                                    break;
                                case 13:  // 鏂板 S1F13 澶勭悊
                                    await ReciveMsgS1F13(args);
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
                                    await ReciveMsgS5F7(args);
                                    break;
                            }
                            break;
                        case 6:
                            switch (function)
                            {
                                case 11:
                                    await ReciveMsgS6F11(args);
                                    break;
                            }
                            break;
                        default:
                            break;
                    }
                    // MESClient.Send_Message(eqpId, "1", $"Recive SECS  MSG:S{stream}F{function}", LogType.INFO);
                }
                else
                {
                    args.SetResultNG($"No SECS context found for {eqpId}.");
                }
                if (string.IsNullOrEmpty(args.GetResultCode()))
                    args.SetResultOK();
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                args.SetResultNG(ex.Message);
            }
        }

        public async Task ConnectionChange(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var state = (ConnectionState)args.Header[CItems.ConnectionState];
            var version = args.Header.ContainsKey(CItems.ConnectionVersion)
                ? Convert.ToInt64(args.Header[CItems.ConnectionVersion])
                : 0;
            var sessionId = args.Header.ContainsKey(CItems.ConnectionSessionId)
                ? Convert.ToInt64(args.Header[CItems.ConnectionSessionId])
                : 0;

            lock (_connectionStateLock)
            {
                if ((_connectionVersions.TryGetValue(eqpId, out var latestVersion) && version < latestVersion)
                    || (_connectionVersions.TryGetValue(eqpId, out latestVersion) && version == latestVersion
                        && _connectionSessionIds.TryGetValue(eqpId, out var latestSessionId) && sessionId < latestSessionId))
                {
                    return;
                }
                _connectionVersions[eqpId] = version;
                _connectionSessionIds[eqpId] = sessionId;
                _connectionStates[eqpId] = state;
            }

            switch (state)
            {
                case ConnectionState.Disabled:
                    await HsmsDisConnectAsync(args);
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
                    // A SECS reply may arrive while S1F13 is still being completed. If
                    // the handshake timed out during a debugger pause, subsequent valid
                    // traffic is still authoritative evidence that the session recovered.
                    if (TrySetConnectionStatus(eqpId, version, sessionId,
                            ConnectStatus.CONNECTED, out var connectionStatusChanged)
                        && connectionStatusChanged)
                    {
                        logger.Info($"HSMS traffic confirmed equipment connected. EqpId={eqpId} "
                            + $"Version={version} SessionId={sessionId}");
                        await MESClient.Send_EqpStateAsync(eqpId, EQPState.Connected, version);
                    }
                    break;
                case ConnectionState.Reconnecting:
                    await HsmsDisConnectAsync(args);
                    break;
                case ConnectionState.Retry:
                    await HsmsDisConnectAsync(args);
                    break;
                case ConnectionState.Disconnected:
                    await HsmsDisConnectAsync(args);
                    break;
            }
        }

        public async Task ReciveMsgS6F11(Arguments args)
        {
            var ceid = 0;
            Item rootItem = null;
            var eqpId = string.Empty;
            try
            {
                eqpId = args.Header[CItems.EqpId].ToString();
                rootItem = args.SecsData.PrimaryMessage.SecsItem;
                var dataid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[0]).ToString());
                ceid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
                Item replyItem = Item.B((byte)0);
                SecsMessage replyMsg = new SecsMessage(6, 12);
                replyMsg.SecsItem = replyItem;
                if (!await SecsExecution.ReplyAsync(args, replyMsg))
                {
                    args.SetResultNG("S6F12 acknowledgement was not sent.");
                    return;
                }

                SendDashboardSecsEvent(args, ceid);
                var isLpStateMachineEvent = IsLPStateMachineEvent(ceid);
                if (isLpStateMachineEvent)
                {
                    Receive_S6F11_LPStateMachineEvent(args, ceid);
                }

                switch (ceid)
                {
                    case 8:
                    case 9:
                    case 10:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/ControlStateChange", () => Receive_S6F11_ControlStateChange(args));
                        break;
                    case 23:
                    case 24:
                        Receive_S6F11_LPAccessStateChange(args);
                        break;
                    case 14:
                    case 15:
                    case 25:
                    case 26:
                    case 27:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                    case 35:
                    case 42:
                    case 43:
                    case 46:
                    case 50:
                    case 51:
                    case 52:
                    case 53:
                    case 54:
                    case 59:
                    case 61:
                    case 75:
                    case 3500:
                    case 3510:
                    case 3511:
                    case 3512:
                    case 3513:
                    case 3514:
                    case 3515:
                        break;
                    case 37:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/CIDWaitingForHost", () => Receive_S6F11_CIDWaitingForHost(args));
                        break;
                    case 65:
                    case 67:
                    case 69:
                    case 70:
                    case 71:
                    case 72:
                    case 73:
                    case 74:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/LPTransferStateChange", () => Receive_S6F11_LPTransferStateChange(args));
                        break;
                    case 48:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/SlotMapWaitingForHost", () => Receive_S6F11_SlotMapWaitingForHost(args));
                        break;
                    case 49:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/SlotMapVerifyOK", () => Receive_S6F11_SlotMapVerifyok(args));
                        break;
                    case 92:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/WaferProcessComplete", () => Receive_S6F11_WaferProcessComplete(args));
                        break;
                    case 105:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/PRJOB_QUEUED", () => Receive_S6F11_PRJOB_QUEUED(args));
                        break;
                    case 127:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/ProcessStart", () => Receive_S6F11_ProcessStart(args));
                        break;
                    case 132:
                        await SecsExecution.RunAsync(args, $"CEID={ceid}/ProcessEnd", () => Receive_S6F11_ProcessEnd(args));
                        break;
                    default:
                        if (!isLpStateMachineEvent)
                        {
                            await SecsExecution.RunAsync(args, $"CEID={ceid}/ChannelEvent", () => HandleChannelCEIDAsync(ceid, args));
                        }
                        break;
                }
                if (string.IsNullOrEmpty(args.GetResultCode()))
                    args.SetResultOK();

            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
            finally
            {
                if (args.GetResultCode() == "NG" && IsAutomaticRunEvent(ceid) && !string.IsNullOrWhiteSpace(eqpId))
                {
                    var portId = args.Header != null && args.Header.TryGetValue(CItems.PortId, out var portValue)
                        ? portValue?.ToString()
                        : null;
                    if (string.IsNullOrWhiteSpace(portId) && rootItem != null)
                        portId = ExtractDashboardPortId(rootItem).ToString();
                    var failure = args.GetResultMsg();
                    MESClient.Send_Message(eqpId, string.IsNullOrWhiteSpace(portId) ? "0" : portId,
                        $"Automatic run failed. CEID={ceid}; TraceId={args.TraceId}; Reason={failure}", LogType.ERROR);
                }
            }
        }

        private static bool IsAutomaticRunEvent(int ceid)
        {
            switch (ceid)
            {
                case 37:  // Carrier ID waiting for host
                case 48:  // Slot map waiting for host
                case 49:  // Slot map verified
                case 65:
                case 67:
                case 69:
                case 70:
                case 71:
                case 72:
                case 73:
                case 74:
                case 92:  // Wafer process complete
                case 105: // Process job queued
                case 127: // Process start
                case 132: // Process end
                    return true;
                default:
                    return false;
            }
        }

        private async Task HandleChannelCEIDAsync(int ceid, Arguments args)
        {
            int startCeid = ceid - (ceid % 2);
            if (!_channelMap.TryGetValue(startCeid, out var ctx))
                return;

            if (ceid != startCeid)
            {
                await ctx.Worker.StopAsync().ConfigureAwait(false);
                return;
            }

            if (ctx.IsRunning) return;
            // Background polling owns its result and trace, separate from the start event.
            var pollArgs = new Arguments { Header = new Dictionary<string, object>(args.Header) };
            ctx.Worker.Start(token => SecsExecution.RunAsync(pollArgs, $"Channel={startCeid}/Polling", async () =>
            {
                logger.Info($"Channel polling started TraceId={pollArgs.TraceId} ParentTraceId={args.TraceId} "
                    + $"EqpId={pollArgs.Header[CItems.EqpId]} Channel={startCeid}");
                token.ThrowIfCancellationRequested();
                LoadSVIDDef(pollArgs.Header[CItems.EqpId].ToString());
                while (await IsOnRemoteAsync(pollArgs, $"Channel={startCeid}/REMOTE", token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    await ctx.PollAction(pollArgs, token).ConfigureAwait(false);
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }), ex => logger.Error($"Channel polling failed TraceId={pollArgs.TraceId} "
                + $"EqpId={pollArgs.Header[CItems.EqpId]} Channel={startCeid} Exception={ex}"));
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

        public async Task ReciveMsgS1F1(Arguments args)
        {
            try
            {
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                Item replyItem = Item.L();
                SecsMessage replyMsg = new SecsMessage(1, 2);
                replyMsg.SecsItem = replyItem;
                if (!await SecsExecution.ReplyAsync(args, replyMsg))
                {
                    args.SetResultNG("S1F2 reply was not sent.");
                    return;
                }
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG(ex.Message);
            }
        }

        public async Task ReciveMsgS1F13(Arguments args)
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
                if (!await SecsExecution.ReplyAsync(args, replyMsg))
                {
                    args.SetResultNG("S1F14 reply was not sent.");
                    return;
                }

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

        public async Task ReciveMsgS5F7(Arguments args)
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

                if (!await SecsExecution.ReplyAsync(args, replyMsg))
                {
                    args.SetResultNG("S5F8 reply was not sent.");
                    return;
                }
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
        public async Task Receive_S6F11_ControlStateChange(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                if (rootItem?.Items == null || rootItem.Items.Length < 2)
                {
                    return;
                }
                var ceid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
                var eqp = AppCache.Current.EqpInfos.FirstOrDefault(a => a.Key == eqpId);
                if (eqp.Value != null)
                {
                    if (ceid is 8 or 9 or 10)
                    {
                        var eventControlState = ceid switch
                        {
                            8 => ControlState.EQPOFFLINE,
                            9 => ControlState.LOCAL,
                            10 => ControlState.REMOTE,
                            _ => ControlState.UNKNOWN
                        };
                        InvalidateControlStateQueries(eqpId);
                        eqp.Value.ControlStatus = eventControlState;
                        PublishControlState(eqpId, eventControlState, force: true);
                        if (eventControlState == ControlState.REMOTE)
                        {
                            await EnsureEventReportsInitializedAsync(args, eqp.Value);
                        }
                        else
                        {
                            StopChannelPolling();
                        }
                        return;
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
        public void Receive_S6F11_LPAccessStateChange(Arguments args)
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
                                    MESClient.Send_Message(eqpId, portId.ToString(), $"EQP Report Auto Event,CEID={ceid},But SVID Aceess ={accessMode}", LogType.ERROR);
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
                                    MESClient.Send_Message(eqpId, portId.ToString(), $"EQP Report Manual Event,CEID={ceid},But SVID Aceess ={accessMode}", LogType.ERROR);
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
        /// Handles E87 carrier/load-port state-machine events that do not start
        /// an automatic host transaction.  The event report is still reflected
        /// in the local port cache so later transfer events use current state.
        /// </summary>
        private void Receive_S6F11_LPStateMachineEvent(Arguments args, int ceid)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var rootItem = args.SecsData.PrimaryMessage.SecsItem;
                var portId = ExtractDashboardPortId(rootItem);
                if (portId <= 0 || !AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo) ||
                    eqpInfo?.PortInfo == null || !eqpInfo.PortInfo.TryGetValue(portId, out var port))
                {
                    return;
                }

                var carrierId = ExtractDashboardCarrierId(rootItem);
                switch (ceid)
                {
                    case 14: // MaterialReceivedEvent
                        if (!string.IsNullOrWhiteSpace(carrierId))
                        {
                            port.CarrierId = carrierId;
                        }
                        break;
                    case 15: // MaterialRemovedEvent
                        port.CarrierId = string.Empty;
                        port.AssociationState = false;
                        port.ClampState = false;
                        port.CarrierApproachComplete = false;
                        port.CarrierTagReleased = false;
                        break;
                    case 26: // EV_CARRIER_ID_READ_FAILED
                    case 43: // EV_CARRIER_ID_VERIFY_FAILED
                        port.CarrierIdState = CarrierIdState.Failed;
                        break;
                    case 27: // EV_CARRIER_LOCATION_CHANGED
                        port.CarrierLocationId = GetSecsTextByPath(rootItem, 2, 0, 1, 1);
                        if (!string.IsNullOrWhiteSpace(carrierId))
                        {
                            port.CarrierId = carrierId;
                        }
                        break;
                    case 75: // EV_CARRIER_APPROACH_COMPLETE
                        port.CarrierApproachComplete = true;
                        break;
                    case 31:
                        port.ClampState = true;
                        break;
                    case 32:
                        port.ClampState = false;
                        break;
                    case 33: // EV_CARRIER_TAG_RELEASED
                        port.CarrierTagReleased = true;
                        break;
                    case 34: // EV_CARRIER_DOOR_OPENED
                        port.CarrierDoorOpen = true;
                        port.CarrierTagReleased = false;
                        break;
                    case 25: // EV_CARRIER_DOOR_CLOSED
                        port.CarrierDoorOpen = false;
                        break;
                    case 35: // E87NoStateTrans01Carrier
                    case 46: // NoStateTransSotMapNotRead
                        port.SlotMapState = CarrierSlotMapState.NotRead;
                        break;
                    case 37: // EV_CARRIER_ID_WAIT_FOR_HOST
                        port.CarrierIdState = CarrierIdState.WaitingForHost;
                        if (!string.IsNullOrWhiteSpace(carrierId))
                        {
                            port.CarrierId = carrierId;
                        }
                        break;
                    case 42: // EV_CARRIER_ID_VERIFY_OK
                        port.CarrierIdState = CarrierIdState.Verified;
                        break;
                    case 48: // EV_CARRIER_SLOTMAP_WAIT_FOR_HOST
                        port.SlotMapState = CarrierSlotMapState.WaitingForHost;
                        break;
                    case 49: // EV_CARRIER_SLOTMAP_VERIFY_OK
                        port.SlotMapState = CarrierSlotMapState.Verified;
                        break;
                    case 50: // EV_CARRIER_SLOTMAP_VERIFY_FAILED
                        port.SlotMapState = CarrierSlotMapState.Failed;
                        break;
                    case 51: // E87NoStateTrans17NotAccessed
                        port.CarrierAccessState = CarrierAccessState.NotAccessed;
                        break;
                    case 52: // EV_CARRIER_IN_ACCESS
                        port.CarrierAccessState = CarrierAccessState.InAccess;
                        break;
                    case 53: // EV_CARRIER_COMPLETE
                        port.CarrierAccessState = CarrierAccessState.Complete;
                        break;
                    case 54: // EV_CARRIER_STOPPED
                        port.CarrierAccessState = CarrierAccessState.Stopped;
                        break;
                    case 59: // AssocSMGoAssoc
                        port.AssociationState = true;
                        if (!string.IsNullOrWhiteSpace(carrierId))
                        {
                            port.CarrierId = carrierId;
                        }
                        break;
                    case 61: // AssocToNotAssoc
                        port.AssociationState = false;
                        port.CarrierId = string.Empty;
                        break;
                    case 3500: // EV_LOADPORT_TRANSFER_OOS (extended event ID)
                        port.TransferStatus = TransferStatus.OutOfService;
                        break;
                    case 3510:
                        port.AccessModeAlarm = true;
                        break;
                    case 3511:
                        port.AccessModeAlarm = false;
                        break;
                    case 3512:
                        port.OutOfServiceAlarm = true;
                        break;
                    case 3513:
                        port.OutOfServiceAlarm = false;
                        break;
                    case 3514:
                        port.ReservationState = true;
                        break;
                    case 3515:
                        port.ReservationState = false;
                        break;
                }

                if (ceid == 3500)
                {
                    MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), port.TransferStatus);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply LP CEID {ceid} state.", ex);
            }
        }

        private static bool IsLPStateMachineEvent(int ceid)
        {
            switch (ceid)
            {
                case 14:
                case 15:
                case 25:
                case 26:
                case 27:
                case 31:
                case 32:
                case 33:
                case 34:
                case 35:
                case 37:
                case 42:
                case 43:
                case 46:
                case 48:
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:
                case 59:
                case 61:
                case 75:
                case 3500:
                case 3510:
                case 3511:
                case 3512:
                case 3513:
                case 3514:
                case 3515:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// LP Transfer 鐘舵€丆heck
        /// </summary>
        /// <param name="args"></param>
        public async Task Receive_S6F11_LPTransferStateChange(Arguments args)
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
                                //var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][3]).ToString());
                                //if (transferStatus != 1)
                                //{
                                //    MESClient.Send_Exception(eqpId, portId.ToString(), $"EQP Report TransferBlocked Event,CEID={ceid},But SVID Aceess ={transferStatus}  ");
                                //}
                                break;
                            }
                        case 72:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.ReadyToLoad;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.ReadyToLoad);
                                var carrierId = eqp.Value.PortInfo[portId].CarrierId;
                                if (eqp.Value.PortInfo[portId].JobMode == 1)
                                {
                                    CleanupTransferStateForPort(eqpId, portId, carrierId);
                                }
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
                                var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][3]).ToString());
                                if (transferStatus != 2)
                                {
                                    MESClient.Send_Message(eqpId, portId.ToString(), $"EQP Report ReadyToLoad Event,CEID={ceid},But SVID Aceess ={transferStatus}", LogType.ERROR);
                                }

                                // Loading a carrier is part of the automatic run
                                // sequence.  Keep state and MES reports current in
                                // LOCAL, but do not issue the automatic command.
                                if (!await IsOnRemoteAsync(eqpId))
                                {
                                    break;
                                }

                                if (!EnsureLoopProcessEnabled(eqpId, portId, "TEST_ToLoad", args))
                                {
                                    break;
                                }

                                Random random = new Random();
                                Thread.Sleep(random.Next(1000, 4000));
                                SecsMessage msg = new SecsMessage(2, 41);
                                Item paramItem = Item.L(new Item[] { Item.A("port"), Item.U1((byte)portId) });
                                msg.SecsItem = Item.L(new Item[] { Item.A("TEST_ToLoad"), paramItem });
                                await SendAutomaticCommandAsync(eqpId, portId, msg, "TEST_ToLoad", args);
                                break;
                            }
                        case 73:
                            {
                                eqp.Value.PortInfo[portId].TransferStatus = TransferStatus.ReadyToUnload;
                                MESClient.Send_LPTransferStatus(eqpId, portId.ToString(), TransferStatus.ReadyToUnload);
                                var transferStatus = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][3]).ToString());
                                if (transferStatus != 3)
                                {
                                    MESClient.Send_Message(eqpId, portId.ToString(), $"EQP Report ReadyToUnload Event,CEID={ceid},But SVID Aceess ={transferStatus}", LogType.ERROR);
                                }
                                if (!await IsOnRemoteAsync(eqpId))
                                {
                                    break;
                                }

                                if (!EnsureLoopProcessEnabled(eqpId, portId, "TEST_ToUnload", args))
                                {
                                    break;
                                }

                                SecsMessage msg = new SecsMessage(2, 41);
                                Item paramItem = Item.L(new Item[] { Item.A("port"), Item.U1((byte)portId) });
                                msg.SecsItem = Item.L(new Item[] { Item.A("TEST_ToUnload"), paramItem });
                                var unloadReady = true;
                                if (eqp.Value.PortInfo[portId].JobMode == 1
                                    && eqp.Value.PortInfo[portId].TransferRole == 2)
                                {
                                    unloadReady = await Send_TOSwitchSimWafer(args, portId, 1);
                                }
                                if (unloadReady)
                                {
                                    await SendAutomaticCommandAsync(eqpId, portId, msg, "TEST_ToUnload", args);
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


        public async Task Receive_S6F11_CIDWaitingForHost(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

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
            if (portId <= 0 || !AppCache.Current.EqpInfos.TryGetValue(eqpId, out var equipment)
                || !equipment.PortInfo.TryGetValue(portId, out var portState))
            {
                args.SetResultNG($"Unknown load port reported by CEID=37. EqpId={eqpId}, Port={portstr}.");
                return;
            }

            portState.CarrierId = carrierId;
            MESClient.Send_MaterialRecived(eqpId, portId, carrierId);

            // 妫€鏌ュ惊鐜窇鐗囩姸鎬侊紙鎸塋P锛夛紝濡傛灉宸插仠姝㈡垨鏆傚仠锛屽垯閲婃斁Carrier
            if (!IsLoopProcessEnabled(eqpId, portId))
            {
                var stateText = TryGetLoopProcessState(eqpId, portId, out var loopState)
                    ? loopState == 2 ? "PAUSED" : "STOPPED"
                    : "STOPPED or not synchronized";
                MESClient.Send_Message(eqpId, portId.ToString(), $"Loop process {stateText}, rejecting new lot. eqpId={eqpId}, carrierId={carrierId}", LogType.WARNING);
                args.Header[CItems.PortId] = portId;
                args.Header[CItems.CarrierId] = carrierId;
                await Send_S3F17_CarrierRelease(args);
                args.SetResultNG();
                return;
            }

            var mesbatchInfo = await SecsExecution.RunAsync(args, "MES_LotInfoDownloadRequest",
                () => MESClient.MES_LotInfoDownloadRequest(eqpId, carrierId, portId.ToString()));
            if (mesbatchInfo == null)
            {
                MESClient.Send_Message(eqpId, portId.ToString(), $"MES_LotInfoDownloadRequest returned null, releasing carrier. eqpId={eqpId}, carrierId={carrierId}", LogType.ERROR);
                args.Header[CItems.PortId] = portId;
                args.Header[CItems.CarrierId] = carrierId;
                await Send_S3F17_CarrierRelease(args);
                args.SetResultNG();
                return;
            }
            var portInfo = AppCache.Current.EqpInfos[eqpId].PortInfo[portId];
            var isTransfer = portInfo.JobMode == 1;
            var isTransferSource = isTransfer && portInfo.TransferRole == 1;
            if (isTransfer)
            {
                // Create the pair state as soon as either carrier arrives. This
                // lets a PJ queued event find its transfer state even when the
                // source/destination LPs arrive in either order.
                GetOrUpdateTransferState(eqpId, portId, carrierId, mesbatchInfo);

                // Transfer pairs may receive the same BATCHCHID from MES. Keep a
                // carrier-scoped copy so source and destination cannot overwrite
                // each other, while retaining the source copy in the legacy cache
                // for PJ/CJ creation.
                AppCache.Current.TransferBatchInfos[GetTransferCarrierKey(eqpId, carrierId)] = mesbatchInfo;
                if (isTransferSource)
                {
                    if (AppCache.Current.MesBatchInfos.TryGetValue(mesbatchInfo.BATCHCHID, out var cachedBatch))
                    {
                        cachedBatch.PORTID = mesbatchInfo.PORTID;
                    }
                    else
                    {
                        AppCache.Current.MesBatchInfos[mesbatchInfo.BATCHCHID] = mesbatchInfo;
                    }
                }
            }
            else if (AppCache.Current.MesBatchInfos.TryGetValue(mesbatchInfo.BATCHCHID, out var cachedBatch))
            {
                // Refresh only the routing field; the existing lot state may already be in use.
                cachedBatch.PORTID = mesbatchInfo.PORTID;
            }
            else
            {
                AppCache.Current.MesBatchInfos.Add(mesbatchInfo.BATCHCHID, mesbatchInfo);
            }
            if (!isTransfer || isTransferSource)
            {
                if (!AppCache.Current.CJQuendList.ContainsKey(mesbatchInfo.BATCHCHID))
                {
                    AppCache.Current.CJQuendList.Add(mesbatchInfo.BATCHCHID, new Dictionary<string, int>());

                    foreach (var lot in mesbatchInfo.LOTLIST)
                    {
                        AppCache.Current.CJQuendList[mesbatchInfo.BATCHCHID].Add(lot.LOT_ID, 0);
                    }
                }
            }
            args.Header[CItems.PortId] = portId;
            args.Header[CItems.CarrierId] = carrierId;
            AppCache.Current.EqpInfos[eqpId].PortInfo[portId].CarrierId = carrierId;

            // Check again after the MES download because the LP may have been
            // stopped while the request was in flight.
            if (!IsLoopProcessEnabled(eqpId, portId))
            {
                var stateText = TryGetLoopProcessState(eqpId, portId, out var currentState)
                    ? currentState == 2 ? "PAUSED" : "STOPPED"
                    : "STOPPED or not synchronized";
                MESClient.Send_Message(eqpId, portId.ToString(), $"Loop process {stateText} after download, releasing carrier.", LogType.WARNING);
                await Send_S3F17_CarrierRelease(args);
                args.SetResultNG();
                return;
            }

            await Send_S3F17_FirstProcessWithCarrier(args);
            if (args.GetResultCode() != "OK")
            {
                var failureMessage = args.GetResultMsg();
                await Send_S3F17_CarrierRelease(args);
                args.SetResultNG(failureMessage);
            }
        }

        public async Task Receive_S6F11_SlotMapWaitingForHost(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

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

            if (!EnsureLoopProcessEnabled(eqpId, portId, "Slot map processing", args))
            {
                await Send_S3F17_CancelCarrier(args);
                return;
            }

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
            await MESClient.Send_MapResultAsync(eqpId, portId.ToString(), slotMapStr);
            if (AppCache.Current.EqpInfos.TryGetValue(eqpId, out var mapEqpInfo)
                && mapEqpInfo.PortInfo.TryGetValue(portId, out var mapPortInfo))
            {
                mapPortInfo.CurrentSlotMap = slotMap.ToList();
            }
            var batchInfo = GetBatchInfoForCarrier(eqpId, carrierId);
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
                    var failureMessage = args.GetResultMsg();
                    await Send_S3F17_CancelCarrier(args);
                    args.SetResultNG(failureMessage);
                    return;
                }
                if (!EnsureLoopProcessEnabled(eqpId, portId, "Slot map processing", args))
                {
                    await Send_S3F17_CancelCarrier(args);
                    return;
                }
                args.Header[CItems.SlotMap] = slotMap;
                args.Header[CItems.BatchInfo] = batchInfo;
                await Send_S3F17_SecondProcessWithCarrier(args);
                if (args.GetResultCode() != "OK")
                {
                    var failureMessage = args.GetResultMsg();
                    await Send_S3F17_CancelCarrier(args);
                    args.SetResultNG(failureMessage);
                }
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
                if (!EnsureLoopProcessEnabled(eqpId, portId, "Slot map processing", args))
                {
                    await Send_S3F17_CancelCarrier(args);
                    return;
                }
                args.Header[CItems.SlotMap] = slotMap;
                args.Header[CItems.BatchInfo] = batchInfo;
                await Send_S3F17_SecondProcessWithCarrier(args);
                //args.SetResultNG();
                //Send_S3F17_CancelCarrier(args);
            }
        }

        public async Task Receive_S6F11_SlotMapVerifyok(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][2]).ToString().Trim();
            var portId = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][0]).ToString().Trim();
            args.Header[CItems.CarrierId] = carrierId;
            args.Header[CItems.PortId] = portId;

            if (!int.TryParse(portId.Replace("LP", string.Empty, StringComparison.OrdinalIgnoreCase), out var parsedPortId)
                || !AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo)
                || !eqpInfo.PortInfo.TryGetValue(parsedPortId, out var portInfo))
            {
                args.SetResultNG($"Unknown load port: {portId}");
                await Send_S3F17_CancelCarrier(args);
                return;
            }

            if (!EnsureLoopProcessEnabled(eqpId, parsedPortId, "Slot map verification", args))
            {
                await Send_S3F17_CancelCarrier(args);
                return;
            }

            var batchInfo = GetBatchInfoForCarrier(eqpId, carrierId);
            var transferState = GetOrUpdateTransferState(eqpId, parsedPortId, carrierId, batchInfo);
            if (portInfo.JobMode == 1 && transferState == null)
            {
                args.SetResultNG($"Invalid Transfer configuration for LP{parsedPortId}.");
                await Send_S3F17_CancelCarrier(args);
                return;
            }
            if (transferState != null)
            {
                var trackInOk = await TrackInTransferPortAsync(args, transferState, parsedPortId);
                if (!trackInOk)
                {
                    await Send_S3F17_CancelCarrier(args);
                    args.SetResultNG($"MES TrackIn failed for Transfer LP{parsedPortId}.");
                    return;
                }

                var currentSlotMap = portInfo.CurrentSlotMap;
                if (currentSlotMap == null || currentSlotMap.Count != 25)
                {
                    args.SetResultNG($"Transfer LP{parsedPortId} has no valid slot map.");
                    await Send_S3F17_CancelCarrier(args);
                    return;
                }
                if (IsSourcePort(transferState, parsedPortId))
                {
                    transferState.SourceSlotMap = currentSlotMap.ToList();
                    await Send_S16F21_SelectPJSpace(args);
                    if (args.GetResultCode() != "OK")
                    {
                        var failureMessage = args.GetResultMsg();
                        await Send_S3F17_CancelCarrier(args);
                        args.SetResultNG(failureMessage);
                        return;
                    }

                    transferState.SourcePjCreateInProgress = true;
                    await Send_S16F15_CreatePJ(args);
                    transferState.SourcePjCreateInProgress = false;
                    transferState.SourcePjCreated = args.GetResultCode() == "OK";
                    if (args.GetResultCode() != "OK")
                    {
                        var failureMessage = args.GetResultMsg();
                        await Send_S3F17_CancelCarrier(args);
                        args.SetResultNG(failureMessage);
                        return;
                    }
                }
                else
                {
                    transferState.DestinationSlotMap = currentSlotMap.ToList();
                }
                // A PJ queued event can arrive before the partner LP finishes
                // mapping. Re-check here once both LPs are ready.
                await TryCreateTransferCJAsync(args, transferState);
                return;
            }

            await Send_S16F21_SelectPJSpace(args);
            if (args.GetResultCode() != "OK")
            {
                var failureMessage = args.GetResultMsg();
                await Send_S3F17_CancelCarrier(args);
                args.SetResultNG(failureMessage);
                return;
            }
            var res = await SecsExecution.RunAsync(args, "MES_TrackIn",
                () => MESClient.MES_TrackIn(eqpId, portId, carrierId));
            if (res)
            {
                await Send_S16F15_CreatePJ(args);
                if (args.GetResultCode() != "OK")
                {
                    var failureMessage = args.GetResultMsg();
                    await Send_S3F17_CancelCarrier(args);
                    args.SetResultNG(failureMessage);
                    return;
                }
            }
            else
            {
                await Send_S3F17_CancelCarrier(args);
                args.SetResultNG("TrackIn Fail");
                return;
            }
        }

        public async Task Receive_S6F11_WaferProcessComplete(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData?.PrimaryMessage?.SecsItem;
            if (rootItem?.Items == null || rootItem.Items.Length != 3)
            {
                args.SetResultNG("Wafer process complete event has an invalid S6F11 structure.");
                return;
            }

            // CEID 92 report layouts differ between equipment configurations.
            // Resolve SubstLocSubstLocID's index from this tool's event-report file.
            var locationId = FindSubstrateLocationId(eqpId, rootItem.Items[2]);
            if (!TryParseSubstrateLocationId(locationId, out var carrierId, out var slot))
            {
                args.SetResultNG($"CEID=92 did not contain a valid SubstLocSubstLocID. Value={locationId ?? "<missing>"}.");
                return;
            }

            if (!AppCache.Current.EqpInfos.TryGetValue(eqpId, out var equipment)
                || equipment?.PortInfo == null)
            {
                args.SetResultNG($"CEID=92 could not resolve equipment port state. EqpId={eqpId}.");
                return;
            }

            var port = equipment.PortInfo.Values.FirstOrDefault(item =>
                string.Equals(item.CarrierId?.Trim(), carrierId, StringComparison.OrdinalIgnoreCase));
            if (port == null)
            {
                args.SetResultNG($"CEID=92 carrier is not associated with a load port. EqpId={eqpId}, CarrierId={carrierId}.");
                return;
            }

            args.Header[CItems.PortId] = port.PortNo;
            args.Header[CItems.CarrierId] = carrierId;
            await MESClient.Send_MapResultAsync(eqpId, port.PortNo.ToString(), slot);
        }

        private string FindSubstrateLocationId(string eqpId, Item reportList)
        {
            if (reportList?.Items == null)
            {
                return null;
            }

            var knownCarriers = AppCache.Current.EqpInfos.TryGetValue(eqpId, out var equipment)
                ? equipment.PortInfo.Values.Select(port => port.CarrierId?.Trim())
                    .Where(carrierId => !string.IsNullOrWhiteSpace(carrierId))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var field = _processCompleteLocationFields.GetOrAdd(eqpId, ResolveProcessCompleteLocationField);
            string mappedValue = null;
            if (field.Found)
            {
                foreach (var report in reportList.Items)
                {
                    if (report?.Items == null || report.Items.Length < 2
                        || !uint.TryParse(SecsItemUtil.GetItemValue(report.Items[0])?.ToString(), out var reportId)
                        || reportId != field.ReportId
                        || report.Items[1]?.Items == null
                        || field.DataIndex >= report.Items[1].Items.Length)
                    {
                        continue;
                    }

                    mappedValue = SecsItemUtil.GetItemValue(report.Items[1].Items[field.DataIndex])?.ToString()?.Trim();
                    if (TryParseSubstrateLocationId(mappedValue, out var mappedCarrier, out _)
                        && (knownCarriers.Count == 0 || knownCarriers.Contains(mappedCarrier)))
                    {
                        return mappedValue;
                    }
                }
            }

            // Some older reports omit DV 185; use the matching loaded carrier
            // as a fallback instead of depending on a layout-specific position.
            foreach (var value in EnumerateSecsLeafValues(reportList))
            {
                if (TryParseSubstrateLocationId(value, out var candidateCarrier, out _)
                    && (knownCarriers.Count == 0 || knownCarriers.Contains(candidateCarrier)))
                {
                    return value;
                }
            }

            return mappedValue;
        }

        private (bool Found, uint ReportId, int DataIndex) ResolveProcessCompleteLocationField(string eqpId)
        {
            try
            {
                var path = ResolveEventReportPath(eqpId);
                if (!File.Exists(path))
                {
                    return (false, 0, -1);
                }

                var document = new XmlDocument();
                document.Load(path);
                var eventNode = document.SelectSingleNode("/EVENT_REPORT/EVENT[@id='92']");
                var reportNodes = document.SelectNodes("/EVENT_REPORT/REPORT");
                if (eventNode == null || reportNodes == null)
                {
                    return (false, 0, -1);
                }

                foreach (XmlNode linkedReport in eventNode.SelectNodes("RPT"))
                {
                    if (!uint.TryParse(XmlUtils.XmlGetNodeAttrVal(linkedReport, "id"), out var reportId))
                    {
                        continue;
                    }

                    var definition = reportNodes.Cast<XmlNode>().FirstOrDefault(node =>
                        uint.TryParse(XmlUtils.XmlGetNodeAttrVal(node, "id"), out var id) && id == reportId);
                    var dataVariables = definition?.SelectNodes("DV");
                    if (dataVariables == null)
                    {
                        continue;
                    }

                    for (var index = 0; index < dataVariables.Count; index++)
                    {
                        if (XmlUtils.XmlGetNodeAttrVal(dataVariables[index], "id") == "185")
                        {
                            return (true, reportId, index);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Unable to resolve CEID=92 SubstLocSubstLocID field. EqpId={eqpId}, Error={ex.Message}");
            }

            return (false, 0, -1);
        }

        private IEnumerable<string> EnumerateSecsLeafValues(Item item)
        {
            if (item == null)
            {
                yield break;
            }

            if (item.Format == SecsFormat.List)
            {
                if (item.Items != null)
                {
                    foreach (var child in item.Items)
                    {
                        foreach (var value in EnumerateSecsLeafValues(child))
                        {
                            yield return value;
                        }
                    }
                }

                yield break;
            }

            var valueText = SecsItemUtil.GetItemValue(item)?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(valueText))
            {
                yield return valueText;
            }
        }

        private static bool TryParseSubstrateLocationId(string value, out string carrierId, out string slot)
        {
            carrierId = null;
            slot = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var separator = value.LastIndexOf('.');
            if (separator <= 0 || separator >= value.Length - 1)
            {
                return false;
            }

            var candidateCarrierId = value.Substring(0, separator).Trim();
            var candidateSlot = value.Substring(separator + 1).Trim();
            if (string.IsNullOrWhiteSpace(candidateCarrierId)
                || !int.TryParse(candidateSlot, out var slotNumber)
                || slotNumber < 1 || slotNumber > 25)
            {
                return false;
            }

            carrierId = candidateCarrierId;
            slot = slotNumber.ToString();
            return true;
        }

        public async Task Receive_S6F11_PRJOB_QUEUED(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var pjid = SecsItemUtil.GetItemValue(rootItem[2][0][1][1]).ToString();

            // Transfer PJs must never fall through to the ordinary CJ path.
            // Identify them by the source batch/PJ itself, so this remains
            // correct even when the LPs arrive in the opposite order or an
            // older CJQuendList entry is still present.
            var transferState = FindTransferStateBySourcePjId(eqpId, pjid);
            if (transferState != null)
            {
                lock (transferState)
                {
                    transferState.SourceQueuedPjIds.Add(pjid);
                    transferState.SourcePjQueued = GetLotIds(transferState.SourceBatchInfo)
                        .All(lotId => transferState.SourceQueuedPjIds.Contains(lotId));
                }
                await TryCreateTransferCJAsync(args, transferState);
                return;
            }

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
                transferState = FindTransferStateBySourceBatchId(eqpId, cjid);
                if (transferState != null)
                {
                    lock (transferState)
                    {
                        transferState.SourceQueuedPjIds.Add(pjid);
                        transferState.SourcePjQueued = GetLotIds(transferState.SourceBatchInfo)
                            .All(lotId => transferState.SourceQueuedPjIds.Contains(lotId));
                    }
                    await TryCreateTransferCJAsync(args, transferState);
                    return;
                }
                if (IsTransferSourceBatch(eqpId, batchInfo.Value))
                {
                    // A transfer PJ may arrive before its state is fully
                    // populated. Never create an ordinary CJ for that batch.
                    MESClient.Send_Message(eqpId, "0",
                        $"Transfer PJ {pjid} is waiting for its paired LP; ordinary CJ creation was skipped.",
                        LogType.WARNING);
                    return;
                }
                await Send_S14F9_CreateCJ(args);
            }
        }

        public async Task Receive_S6F11_ProcessStart(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var carrierID = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][5][0]).ToString().Trim();
            var portID = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][5][0]).ToString().Trim();

            await SecsExecution.RunAsync(args, "MES_LotStart", () => MESClient.MES_LotStart(eqpId, portID, carrierID));
        }

        public async Task Receive_S6F11_ProcessEnd(Arguments args)
        {
            if (!await IsOnRemoteAsync(args))
            {
                return;
            }

            var eqpId = args.Header[CItems.EqpId].ToString();
            var rootItem = args.SecsData.PrimaryMessage.SecsItem;
            if (rootItem.Items.Length != 3)
            {
                args.SetResultNG();
                return;
            }
            var carrierID = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][5][0]).ToString().Trim();
            var portID = SecsItemUtil.GetItemValue(rootItem.Items[2][0][1][5][0]).ToString().Trim();
            var transferState = FindTransferStateByCarrier(eqpId, carrierID);
            if (transferState != null)
            {
                await CompleteTransferTrackOutAsync(args, transferState);
                return;
            }
            await SecsExecution.RunAsync(args, "MES_LotEnd", () => MESClient.MES_LotEnd(eqpId, portID, carrierID));
            await SecsExecution.RunAsync(args, "MES_TrackOut", () => MESClient.MES_TrackOut(eqpId, portID, carrierID));
        }
        #endregion Receive  Message

        #region Send Message
        public ControlState Send_S1F3_Get_ControlState(Arguments args)
        {
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var check = (bool)args.Header[CItems.Check];
                // Compatibility boundary for the synchronous MQ/reflection dispatcher.
                // The actual SECS query and parsing are shared with all async callers.
                var result = QueryControlStateAsync(eqpId).GetAwaiter().GetResult();
                if (!result.Success)
                {
                    args.SetResultNG(result.Message);
                    return ControlState.UNKNOWN;
                }

                if (!check)
                {
                    PublishControlState(eqpId, result.State.Value);
                }
                args.SetResultOK();
                return result.State.Value;
            }
            catch (Exception ex)
            {
                args?.SetResultNG($"S1F3/SVID29 query failed: {ex.Message}");
                return ControlState.UNKNOWN;
            }
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
            lock (_svidDefinitionLock)
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
        }

        public static bool IsMatchL2CH1VlvFlwPV(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            //return Regex.IsMatch(input, @"^R3CH1_Vlv\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH2_Vlv\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH3_Vlv\d+FlwPV$");
            //return Regex.IsMatch(input, @"^R3CH1_Ana\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH2_Ana\d+FlwPV$") || Regex.IsMatch(input, @"^R3CH3_Ana\d+FlwPV$");
            return Regex.IsMatch(input, @"FlwPV") || Regex.IsMatch(input, @"ComAss") || Regex.IsMatch(input, @"LifeAct") || Regex.IsMatch(input, @"Door_Open_Sns");
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

        public async Task Send_S1F3_Get_DataVarible(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            SecsMessage msg = new SecsMessage(1, 3);
            Item[] items = new Item[] { Item.U4(163) };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
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
                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
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
                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);

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

                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);

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

        private async Task<bool> Send_TOSwitchSimWafer(Arguments args, int inputPortId, uint simWaferStatus)
        {
            try
            {
                if (!await EnsureRemoteAsync(args, "Switch simulated wafer status on input LP"))
                    return false;

                if (inputPortId <= 0)
                {
                    args.SetResultNG($"Send TEST_TOSWITCHSIMWAFER failed: invalid input LP number {inputPortId}.");
                    return false;
                }

                var eqpId = args.Header[CItems.EqpId].ToString();
                if (!EnsureLoopProcessEnabled(eqpId, inputPortId, "TEST_TOSWITCHSIMWAFER", args))
                {
                    return false;
                }

                var msg = new SecsMessage(2, 41)
                {
                    SecsItem = Item.L(new Item[]
                    {
                        Item.A("TEST_TOSWITCHSIMWAFER"),
                        Item.L(new Item[]
                        {
                            Item.L(new Item[] { Item.A("PORTNUM"), Item.U4((uint)inputPortId) }),
                            Item.L(new Item[] { Item.A("SIMWAFERSTATUS"), Item.U4(simWaferStatus) })
                        })
                    })
                };

                var context = EAPContext.contexts?.FirstOrDefault(a => a.EquipmentID == eqpId);
                if (context?.SecsGem == null)
                {
                    args.SetResultNG("Send TEST_TOSWITCHSIMWAFER failed: SECS context unavailable.");
                    return false;
                }

                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
                if (reply?.SecsItem == null || reply.SecsItem.Items.Length == 0)
                {
                    args.SetResultNG("Send TEST_TOSWITCHSIMWAFER failed: no reply received.");
                    return false;
                }

                var retCode = Convert.ToInt32(SecsItemUtil.GetItemValue(reply.SecsItem.Items[0]));

                //不校验SwitchSimWafer是否成功，避免影响后续流程，因为有的项目（如23001）不支持该命令，返回非0会导致后续流程中断
                //if (retCode != 0)
                //{
                //    args.SetResultNG($"Send TEST_TOSWITCHSIMWAFER rejected by equipment. ReturnCode: {retCode}");
                //    return false;
                //}

                return true;
            }
            catch (Exception ex)
            {
                args.SetResultNG($"Send TEST_TOSWITCHSIMWAFER failed: {ex.Message}");
                return false;
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
                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
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
                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
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
            if (!await EnsureRemoteAsync(args, "ProceedWithCarrier"))
            {
                return;
            }

            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            if (!EnsureLoopProcessEnabled(EqpId, portId, "ProceedWithCarrier", args))
            {
                return;
            }

            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId), Item.U1((byte)portId), Item.L() };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Send ProceedWithCarrier failed: no reply received.");
                return;
            }
            else
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
            if (!await EnsureRemoteAsync(args, "ProceedWithCarrier slot map"))
            {
                return;
            }

            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            if (!EnsureLoopProcessEnabled(EqpId, portId, "ProceedWithCarrier slot map", args))
            {
                return;
            }

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
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Send ProceedWithCarrier slot map failed: no reply received.");
                return;
            }
            else
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

        public async Task Send_S3F17_CarrierRelease(Arguments args)
        {
            if (!await EnsureRemoteAsync(args, "CarrierRelease"))
            {
                return;
            }

            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("CarrierRelease"), Item.A(carrierId), Item.U1((byte)portId), Item.L() };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Send CarrierRelease failed: no reply received.");
                return;
            }
            else
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

        public async Task Send_S3F17_CancelCarrier(Arguments args)
        {
            if (!await EnsureRemoteAsync(args, "CancelCarrier"))
            {
                return;
            }

            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = args.Header[CItems.CarrierId].ToString();
            var portId = int.Parse(args.Header[CItems.PortId].ToString());
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("CancelCarrier"), Item.A(carrierId), Item.U1((byte)portId), Item.L() };
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Send CancelCarrier failed: no reply received.");
                return;
            }
            else
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
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
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
            if (!await EnsureRemoteAsync(args, "Select process job space"))
            {
                return;
            }

            var EqpId = args.Header[CItems.EqpId].ToString();
            var portText = args.Header.TryGetValue(CItems.PortId, out var portValue) ? portValue?.ToString() : null;
            if (!int.TryParse(portText?.Replace("LP", string.Empty, StringComparison.OrdinalIgnoreCase), out var portId)
                || !EnsureLoopProcessEnabled(EqpId, portId, "Select process job space", args))
            {
                return;
            }

            SecsMessage msg = new SecsMessage(16, 21);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Select process job space failed: no reply received.");
                return;
            }
            else
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
                if (!await EnsureRemoteAsync(args, "Create process jobs"))
                {
                    return;
                }

                var EqpId = args.Header[CItems.EqpId].ToString();
                var carrierId = args.Header[CItems.CarrierId].ToString();
                var portId = int.Parse(args.Header[CItems.PortId].ToString());
                if (!EnsureLoopProcessEnabled(EqpId, portId, "Create process jobs", args))
                {
                    return;
                }

                var batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.CARRIERID.Trim() == carrierId.Trim());
                if (batchInfo == null)
                {
                    batchInfo = AppCache.Current.MesBatchInfos.Values.FirstOrDefault(a => a.PORTID.Trim() == portId.ToString().Trim());
                    batchInfo.CARRIERID = carrierId;
                }
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
                if (!EnsureLoopProcessEnabled(EqpId, portId, "Create process jobs", args))
                {
                    return;
                }
                msg.SecsItem = Item.L(new Item[] { Item.U1((byte)0), Item.L(pjItems) });
                var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
                if (reply == null)
                {
                    args.SetResultNG("Create process jobs failed: no reply received.");
                    return;
                }
                else
                {
                    var res = (bool)SecsItemUtil.GetItemValue(reply.SecsItem[1][0]);
                    if (!res)
                    {
                        args.SetResultNG();
                        return;
                    }
                    await MESClient.Send_MapResultAsync(EqpId, portId.ToString(), string.Join(';', slots) + ";");
                }
                args.SetResultOK();
            }
            catch (Exception ex)
            {
                args.SetResultNG($"Create process jobs failed: {ex.Message}");
            }
        }

        private static bool TryGetTransferCJResult(Item replyItem, out int value)
        {
            value = 0;
            if (replyItem == null)
                return false;

            // Standard S14F10 is a scalar ACKC. Some equipment wraps the
            // same value under the object reply list used by S14F9.
            var itemValue = SecsItemUtil.GetItemValue(replyItem);
            if (itemValue != null && int.TryParse(itemValue.ToString(), out value))
                return true;

            if (replyItem.Items?.Length > 2)
            {
                var objectReply = replyItem[2];
                if (objectReply?.Items?.Length > 0)
                {
                    foreach (var candidate in objectReply.Items.Take(2))
                    {
                        var candidateValue = SecsItemUtil.GetItemValue(candidate);
                        if (candidateValue != null && int.TryParse(candidateValue.ToString(), out value))
                            return true;
                    }
                }
                if (objectReply != null)
                {
                    var objectValue = SecsItemUtil.GetItemValue(objectReply);
                    if (objectValue != null && int.TryParse(objectValue.ToString(), out value))
                        return true;
                }
            }
            return false;
        }

        private async Task Send_S14F9_CreateTransferCJ(Arguments args, TransferJobState state, List<int> slots)
        {
            if (!await EnsureRemoteAsync(args, "Create Transfer control job"))
                return;

            if (state.SourceBatchInfo == null || string.IsNullOrWhiteSpace(state.SourceCarrierId)
                || string.IsNullOrWhiteSpace(state.DestinationCarrierId))
            {
                args.SetResultNG("Transfer CJ requires source and destination carriers.");
                return;
            }

            var eqpId = args.Header[CItems.EqpId].ToString();
            if (!EnsureLoopProcessEnabled(eqpId, state.SourcePortId, "Create Transfer control job", args)
                || !EnsureLoopProcessEnabled(eqpId, state.DestinationPortId, "Create Transfer control job", args))
            {
                return;
            }

            var sourceBatch = state.SourceBatchInfo;
            var pjItems = new Item[sourceBatch.BATCHSIZE > 0 ? sourceBatch.BATCHSIZE : sourceBatch.LOTLIST.Count];
            var index = 0;
            foreach (var lot in sourceBatch.LOTLIST)
            {
                pjItems[index++] = Item.L(new Item[] { Item.A(lot.LOT_ID), Item.L(), Item.L() });
            }

            if (!EnsureLoopProcessEnabled(eqpId, state.SourcePortId, "Create Transfer control job", args)
                || !EnsureLoopProcessEnabled(eqpId, state.DestinationPortId, "Create Transfer control job", args))
            {
                return;
            }

            var sourceSlots = Item.L(slots.Select(slot => Item.U1((byte)slot)).ToArray());
            var destinationSlots = Item.L(slots.Select(slot => Item.U1((byte)slot)).ToArray());
            var mtrlOutSpec = Item.L(new Item[]
            {
                Item.L(new Item[] { Item.A(state.SourceCarrierId), sourceSlots }),
                Item.L(new Item[] { Item.A(state.DestinationCarrierId), destinationSlots })
            });

            var cjItem = Item.L(new Item[]
            {
                Item.L(new Item[] { Item.A("ObjID"), Item.A("ControlJobTransfer" + state.SourceCarrierId) }),
                Item.L(new Item[] { Item.A("CarrierInputSpec"), Item.L(new Item[] { Item.A(state.SourceCarrierId) }) }),
                Item.L(new Item[] { Item.A("ProcessingCtrlSpec"), Item.L(pjItems) }),
                Item.L(new Item[] { Item.A("ProcessOrderMgmt"), Item.U1((byte)3) }),
                Item.L(new Item[] { Item.A("StartMethod"), Item.Boolean(true) }),
                Item.L(new Item[] { Item.A("MtrlOutSpec"), Item.L(new Item[] { mtrlOutSpec }) }),
                Item.L(new Item[] { Item.A("PauseEvent"), Item.L() })
            });

            var msg = new SecsMessage(14, 9)
            {
                SecsItem = Item.L(new Item[] { Item.A("Equipment"), Item.A("ControlJob"), cjItem })
            };
            var context = EAPContext.contexts.First(a => a.EquipmentID == eqpId);
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Create Transfer control job failed: no reply received.");
                return;
            }

            if (!TryGetTransferCJResult(reply.SecsItem, out var resultCode) || resultCode != 0)
            {
                args.SetResultNG("Create Transfer control job rejected by equipment.");
                return;
            }
            args.SetResultOK();
        }

        public async Task Send_S14F9_CreateCJ(Arguments args)
        {
            if (!await EnsureRemoteAsync(args, "Create control job"))
            {
                return;
            }

            var batchId = args.Header[CItems.BatchInfo].ToString();
            var batchInfo = AppCache.Current.MesBatchInfos[batchId];
            var EqpId = args.Header[CItems.EqpId].ToString();
            var carrierId = batchInfo.CARRIERID;
            var portId = batchInfo.PORTID;
            if (!int.TryParse(portId, out var parsedPortId)
                || !EnsureLoopProcessEnabled(EqpId, parsedPortId, "Create control job", args))
            {
                return;
            }

            //var portId = AppCache.Current.EqpInfos[EqpId].PortInfo.Values.FirstOrDefault(a => a.CarrierId == carrierId).PortNo.ToString();
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
            if (!EnsureLoopProcessEnabled(EqpId, parsedPortId, "Create control job", args))
            {
                return;
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
            var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);
            if (reply == null)
            {
                args.SetResultNG("Create control job failed: no reply received.");
                return;
            }
            else
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

        private async Task<bool> EnsureRemoteAsync(Arguments args, string action,
            [CallerMemberName] string source = null)
        {
            if (await IsOnRemoteAsync(args, $"{source}/{action}"))
            {
                return true;
            }

            var eqpId = args?.Header != null && args.Header.TryGetValue(CItems.EqpId, out var value)
                ? value?.ToString()
                : string.Empty;
            args?.SetResultNG($"{action} skipped because equipment {eqpId} is not in REMOTE control state.");
            return false;
        }

        private void StopChannelPolling()
        {
            if (_channelMap == null)
            {
                return;
            }

            foreach (var channel in _channelMap.Values)
            {
                if (channel != null)
                {
                    _ = channel.Worker.StopAsync();
                }
            }
        }

        private async Task<bool> SendAutomaticCommandAsync(string eqpId, int portId, SecsMessage message, string commandName, Arguments args)
        {
            if (!await IsOnRemoteAsync(eqpId, commandName))
            {
                MESClient.Send_Message(eqpId, portId.ToString(), $"{commandName} skipped because equipment is not in REMOTE control state.", LogType.WARNING);
                return false;
            }

            try
            {
                var context = EAPContext.contexts?.FirstOrDefault(item => item.EquipmentID == eqpId);
                if (context?.SecsGem == null)
                {
                    MESClient.Send_Message(eqpId, portId.ToString(), $"{commandName} skipped because the SECS context is unavailable.", LogType.ERROR);
                    return false;
                }

                if (!EnsureLoopProcessEnabled(eqpId, portId, commandName, args))
                {
                    return false;
                }

                var reply = await SecsExecution.SendAsync(args, context.SecsGem, message);
                if (reply == null)
                {
                    MESClient.Send_Message(eqpId, portId.ToString(), $"{commandName} failed: no SECS reply received.", LogType.ERROR);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MESClient.Send_Message(eqpId, portId.ToString(), $"{commandName} failed: {ex.Message}", LogType.ERROR);
                return false;
            }
        }

        private async Task<bool> IsOnRemoteAsync(string eqpId, [CallerMemberName] string source = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(eqpId) || AppCache.Current?.EqpInfos == null)
            {
                return false;
            }

            if (!AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo) || eqpInfo == null)
            {
                return false;
            }

            if (eqpInfo.ConnectStatus != ConnectStatus.CONNECTED)
            {
                return false;
            }

            var context = EAPContext.contexts?.FirstOrDefault(item => item.EquipmentID == eqpId);
            if (context?.SecsGem == null)
            {
                return false;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await QueryControlStateAsync(eqpId, source, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!result.Success)
                {
                    MESClient.Send_Message(eqpId, "0", $"{result.Message} Source={source}; automatic processing skipped.", LogType.WARNING);
                    return false;
                }

                PublishControlState(eqpId, result.State.Value);
                return result.State.Value == ControlState.REMOTE;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                MESClient.Send_Message(eqpId, "0", $"S1F3/SVID29 query failed: {ex.Message}; automatic processing skipped.", LogType.WARNING);
                return false;
            }
        }

        private async Task<ControlStateQueryResult> QueryControlStateAsync(string eqpId,
            [CallerMemberName] string source = null, CancellationToken cancellationToken = default)
        {
            var generation = GetControlStateConnectionGeneration(eqpId);
            var traceId = SecsExecution.CurrentTraceId;
            string queryId = null;
            var sw = Stopwatch.StartNew();
            ControlStateQueryResult result = null;
            try
            {
                result = await _controlStateQueries.RunAsync(eqpId, generation,
                    (id, token) => ExecuteControlStateQueryAsync(eqpId, source, token, id), cancellationToken,
                    (id, shared) =>
                    {
                        queryId = id;
                        logger.Info($"SVID29 Wait Begin TraceId={traceId} QueryId={id} EqpId={eqpId} Source={source} Shared={shared}");
                    }).ConfigureAwait(false);
                if (generation != GetControlStateConnectionGeneration(eqpId))
                    result = new(ControlStateQueryStatus.StaleConnection,
                        Message: "SVID29 result discarded because connection or control state changed during the query.");
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = new(ControlStateQueryStatus.Cancelled, Message: "SVID29 caller stopped waiting.");
                return result;
            }
            finally
            {
                logger.Info($"SVID29 Wait End TraceId={traceId} QueryId={queryId} EqpId={eqpId} Source={source} "
                    + $"ElapsedMs={sw.ElapsedMilliseconds} Result={result?.Status}");
            }
        }

        private async Task<ControlStateQueryResult> ExecuteControlStateQueryAsync(string eqpId,
            string source, CancellationToken cancellationToken, string queryId)
        {
            var sw = Stopwatch.StartNew();
            var result = new ControlStateQueryResult(ControlStateQueryStatus.Failed,
                Message: "S1F3/SVID29 query did not complete.");
            // Keep the protocol T3 unchanged. This watchdog also covers a missing native timeout callback.
            var timeout = GetSecsReplyTimeout(eqpId);
            var traceId = SecsExecution.CurrentTraceId;
            logger.Info($"S1F3/SVID29 Begin TraceId={traceId} QueryId={queryId} EqpId={eqpId} Source={source} TimeoutMs={timeout.TotalMilliseconds:F0}");
            try
            {
                var context = EAPContext.contexts?.FirstOrDefault(item => item.EquipmentID == eqpId);
                if (context?.SecsGem == null)
                {
                    result = new(ControlStateQueryStatus.Unavailable,
                        Message: "S1F3/SVID29 query skipped because the SECS context is unavailable.");
                    return result;
                }

                var generation = GetControlStateConnectionGeneration(eqpId);
                result = await ControlStateQuery.ExecuteAsync(context.SecsGem.SendAsync, timeout, cancellationToken, queryId)
                    .ConfigureAwait(false);
                if (generation != GetControlStateConnectionGeneration(eqpId))
                {
                    result = new(ControlStateQueryStatus.StaleConnection,
                        Message: "S1F3/SVID29 query discarded because connection or control state changed during the query.");
                }
                return result;
            }
            catch (Exception ex)
            {
                result = new(ControlStateQueryStatus.Failed,
                    Message: $"S1F3/SVID29 query failed: {ex.Message}", Exception: ex);
                return result;
            }
            finally
            {
                var log = $"S1F3/SVID29 End TraceId={traceId} QueryId={queryId} EqpId={eqpId} Source={source} "
                    + $"Result={result.Status} State={result.State} ElapsedMs={sw.ElapsedMilliseconds} Detail={result.Message}";
                if (result.Success)
                    logger.Info(log);
                else
                    logger.Warn(log + (result.Exception == null ? string.Empty : $" Exception={result.Exception}"));
            }
        }

        private static TimeSpan GetSecsReplyTimeout(string eqpId)
        {
            var t3Seconds = EAPContext.EqpConfigures?.FirstOrDefault(config => config.Name == eqpId)?.T3 ?? 45;
            return TimeSpan.FromSeconds((t3Seconds > 0 ? t3Seconds : 45) + 1d);
        }

        private long GetControlStateConnectionGeneration(string eqpId)
        {
            lock (_connectionStateLock)
            {
                return _controlStateConnectionGenerations.TryGetValue(eqpId, out var generation) ? generation : 0;
            }
        }

        private void InvalidateControlStateQueries(string eqpId)
        {
            lock (_connectionStateLock)
            {
                _controlStateConnectionGenerations.TryGetValue(eqpId, out var generation);
                _controlStateConnectionGenerations[eqpId] = generation + 1;
            }
            _controlStateQueries.Invalidate(eqpId);
        }

        private static void PublishControlState(string eqpId, ControlState state, bool force = false)
        {
            var changed = true;
            if (AppCache.Current?.EqpInfos != null
                && AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo)
                && eqpInfo != null)
            {
                changed = eqpInfo.ControlStatus != state;
                eqpInfo.ControlStatus = state;
            }

            if (force || changed)
            {
                MESClient.Send_ControlState(eqpId, state);
            }
        }

        private async Task<bool> IsOnRemoteAsync(Arguments args, [CallerMemberName] string source = null,
            CancellationToken cancellationToken = default)
        {
            if (args?.Header == null || !args.Header.TryGetValue(CItems.EqpId, out var eqpId))
            {
                args?.SetResultNG("REMOTE check skipped because equipment id is unavailable.");
                return false;
            }

            var remote = await IsOnRemoteAsync(eqpId?.ToString(), source, cancellationToken).ConfigureAwait(false);
            if (!remote)
                args.SetResultNG($"{source} skipped because equipment {eqpId} is not in a confirmed REMOTE state.");
            return remote;
        }

        /// <summary>
        /// 妫€鏌P鏄惁鍏佽杩涜寰幆璺戠墖
        /// </summary>
        private bool IsLoopProcessEnabled(string eqpId, int portId = 0)
        {
            return TryGetLoopProcessState(eqpId, portId, out var state) && state == 0;
        }

        private bool TryGetLoopProcessState(string eqpId, int portId, out int state)
        {
            state = -1;
            var processStates = AppCache.Current?.LoopProcessState;
            if (processStates == null || string.IsNullOrWhiteSpace(eqpId) || portId <= 0)
            {
                return false;
            }

            lock (processStates)
            {
                return processStates.TryGetValue(eqpId, out var portStates)
                    && portStates != null
                    && portStates.TryGetValue(portId, out state);
            }
        }

        private bool EnsureLoopProcessEnabled(string eqpId, int portId, string action, Arguments args)
        {
            if (IsLoopProcessEnabled(eqpId, portId))
            {
                return true;
            }

            var stateText = TryGetLoopProcessState(eqpId, portId, out var state)
                ? state == 2 ? "PAUSED" : state == 1 ? "STOPPED" : "not synchronized"
                : "STOPPED or not synchronized";
            var message = $"{action} skipped because loop process on LP{portId} is {stateText}.";
            MESClient.Send_Message(eqpId, portId.ToString(), message, LogType.WARNING);
            args?.SetResultNG(message);
            return false;
        }

        private static string GetTransferStateKey(string eqpId, int sourcePortId)
        {
            return $"{eqpId}:{sourcePortId}";
        }

        private static string GetTransferCarrierKey(string eqpId, string carrierId)
        {
            return $"{eqpId}:{carrierId?.Trim()}";
        }

        private static MESLotInfoDownloadReply GetBatchInfoForCarrier(string eqpId, string carrierId)
        {
            if (AppCache.Current.TransferBatchInfos != null
                && AppCache.Current.TransferBatchInfos.TryGetValue(GetTransferCarrierKey(eqpId, carrierId), out var transferBatch))
            {
                return transferBatch;
            }

            return AppCache.Current.MesBatchInfos?.Values.FirstOrDefault(batch =>
                string.Equals(batch?.CARRIERID?.Trim(), carrierId?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private TransferJobState GetOrUpdateTransferState(
            string eqpId, int portId, string carrierId, MESLotInfoDownloadReply batchInfo)
        {
            if (string.IsNullOrWhiteSpace(eqpId)
                || !AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo)
                || eqpInfo?.PortInfo == null
                || !eqpInfo.PortInfo.TryGetValue(portId, out var port)
                || port.JobMode != 1
                || port.TransferRole is not (1 or 2)
                || port.TransferPartnerPortNo <= 0
                || !eqpInfo.PortInfo.TryGetValue(port.TransferPartnerPortNo, out var partner)
                || partner.JobMode != 1
                || partner.TransferRole is not (1 or 2)
                || partner.TransferRole == port.TransferRole
                || partner.TransferPartnerPortNo != portId)
            {
                return null;
            }

            var sourcePortId = port.TransferRole == 1 ? portId : port.TransferPartnerPortNo;
            var destinationPortId = port.TransferRole == 1 ? port.TransferPartnerPortNo : portId;
            var key = GetTransferStateKey(eqpId, sourcePortId);
            var state = AppCache.Current.TransferJobStates.GetOrAdd(key, _ => new TransferJobState
            {
                EquipmentId = eqpId,
                SourcePortId = sourcePortId,
                DestinationPortId = destinationPortId
            });

            if (port.TransferRole == 1)
            {
                if (!string.Equals(state.SourceCarrierId, carrierId, StringComparison.OrdinalIgnoreCase))
                {
                    state.SourceCarrierGeneration++;
                    state.SourcePjCreated = false;
                    state.SourcePjCreateInProgress = false;
                    state.SourcePjQueued = false;
                    state.SourceQueuedPjIds.Clear();
                    state.CjCreated = false;
                    state.CjCreateInProgress = false;
                    state.TrackOutCompleted = false;
                    state.Failed = false;
                }
                state.SourceCarrierId = carrierId;
                state.SourceBatchInfo = batchInfo;
                state.SourceBatchId = batchInfo?.BATCHCHID;
            }
            else
            {
                if (!string.Equals(state.DestinationCarrierId, carrierId, StringComparison.OrdinalIgnoreCase))
                {
                    state.DestinationCarrierGeneration++;
                    state.DestinationTrackIn = false;
                    state.DestinationSlotMap = null;
                    state.DestinationSimWaferStatusSet = false;
                    state.CjCreated = false;
                    state.CjCreateInProgress = false;
                    state.TrackOutCompleted = false;
                    state.Failed = false;
                }
                state.DestinationCarrierId = carrierId;
                state.DestinationBatchInfo = batchInfo;
                state.DestinationBatchId = batchInfo?.BATCHCHID;
            }

            state.Failed = false;
            return state;
        }

        private TransferJobState FindTransferStateForPort(string eqpId, int portId, string carrierId = null)
        {
            if (string.IsNullOrWhiteSpace(eqpId) || AppCache.Current.TransferJobStates == null)
                return null;

            if (AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo)
                && eqpInfo?.PortInfo != null
                && eqpInfo.PortInfo.TryGetValue(portId, out var port)
                && port.TransferRole == 1)
            {
                if (AppCache.Current.TransferJobStates.TryGetValue(GetTransferStateKey(eqpId, portId), out var sourceState))
                    return sourceState;
            }

            return AppCache.Current.TransferJobStates.Values.FirstOrDefault(state =>
                state.EquipmentId == eqpId
                && (state.SourcePortId == portId || state.DestinationPortId == portId)
                && (string.IsNullOrWhiteSpace(carrierId)
                    || string.Equals(state.SourceCarrierId, carrierId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state.DestinationCarrierId, carrierId, StringComparison.OrdinalIgnoreCase)));
        }

        private TransferJobState FindTransferStateByCarrier(string eqpId, string carrierId)
        {
            if (string.IsNullOrWhiteSpace(eqpId) || string.IsNullOrWhiteSpace(carrierId)
                || AppCache.Current.TransferJobStates == null)
                return null;

            return AppCache.Current.TransferJobStates.Values.FirstOrDefault(state =>
                state.EquipmentId == eqpId
                && (string.Equals(state.SourceCarrierId, carrierId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state.DestinationCarrierId, carrierId, StringComparison.OrdinalIgnoreCase)));
        }

        private TransferJobState FindTransferStateBySourceBatchId(string eqpId, string batchId)
        {
            if (string.IsNullOrWhiteSpace(eqpId) || string.IsNullOrWhiteSpace(batchId)
                || AppCache.Current.TransferJobStates == null)
                return null;

            return AppCache.Current.TransferJobStates.Values.FirstOrDefault(state =>
                state.EquipmentId == eqpId
                && string.Equals(state.SourceBatchId, batchId, StringComparison.OrdinalIgnoreCase));
        }

        private TransferJobState FindTransferStateBySourcePjId(string eqpId, string pjid)
        {
            if (string.IsNullOrWhiteSpace(eqpId) || string.IsNullOrWhiteSpace(pjid)
                || AppCache.Current.TransferJobStates == null)
                return null;

            return AppCache.Current.TransferJobStates.Values.FirstOrDefault(state =>
                state.EquipmentId == eqpId
                && state.SourceBatchInfo?.LOTLIST?.Any(lot =>
                    string.Equals(lot?.LOT_ID, pjid, StringComparison.OrdinalIgnoreCase)) == true);
        }

        private static bool IsTransferSourceBatch(string eqpId, MESLotInfoDownloadReply batchInfo)
        {
            if (string.IsNullOrWhiteSpace(eqpId) || batchInfo == null
                || !int.TryParse(batchInfo.PORTID, out var portId)
                || !AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo)
                || eqpInfo?.PortInfo == null
                || !eqpInfo.PortInfo.TryGetValue(portId, out var portInfo))
                return false;

            return portInfo.JobMode == 1 && portInfo.TransferRole == 1;
        }

        private static List<string> GetLotIds(MESLotInfoDownloadReply batchInfo)
        {
            return batchInfo?.LOTLIST?
                .Where(lot => lot != null && !string.IsNullOrWhiteSpace(lot.LOT_ID))
                .Select(lot => lot.LOT_ID)
                .ToList() ?? new List<string>();
        }

        private static List<int> GetTransferSlots(MESLotInfoDownloadReply batchInfo)
        {
            return batchInfo?.LOTLIST?
                .Where(lot => lot?.WAFERS != null)
                .SelectMany(lot => lot.WAFERS)
                .Where(wafer => wafer != null && wafer.SLOT > 0)
                .Select(wafer => wafer.SLOT)
                .ToList() ?? new List<int>();
        }

        private static bool IsSourcePort(TransferJobState state, int portId)
        {
            return state != null && state.SourcePortId == portId;
        }

        private async Task<bool> TrackInTransferPortAsync(Arguments args, TransferJobState state, int portId)
        {
            if (state == null)
                return false;

            var source = IsSourcePort(state, portId);
            lock (state)
            {
                if (source && state.SourceTrackIn || !source && state.DestinationTrackIn)
                    return true;

                if (source && state.SourceTrackInInProgress || !source && state.DestinationTrackInInProgress)
                    return false;

                if (source)
                    state.SourceTrackInInProgress = true;
                else
                    state.DestinationTrackInInProgress = true;
            }
            var carrierId = source ? state.SourceCarrierId : state.DestinationCarrierId;
            bool result;
            try
            {
                result = await SecsExecution.RunAsync(args, "MES_TrackIn",
                    () => MESClient.MES_TrackIn(args.Header[CItems.EqpId].ToString(), portId.ToString(), carrierId));
            }
            finally
            {
                lock (state)
                {
                    if (source)
                        state.SourceTrackInInProgress = false;
                    else
                        state.DestinationTrackInInProgress = false;
                }
            }
            if (result)
            {
                var refreshedBatchInfo = GetBatchInfoForCarrier(args.Header[CItems.EqpId].ToString(), carrierId);
                if (refreshedBatchInfo != null)
                {
                    if (source)
                    {
                        state.SourceBatchInfo = refreshedBatchInfo;
                        state.SourceBatchId = refreshedBatchInfo.BATCHCHID;
                    }
                    else
                    {
                        state.DestinationBatchInfo = refreshedBatchInfo;
                        state.DestinationBatchId = refreshedBatchInfo.BATCHCHID;
                    }
                }
                if (source)
                    state.SourceTrackIn = true;
                else
                    state.DestinationTrackIn = true;
            }
            else
            {
                state.Failed = true;
            }
            return result;
        }

        private async Task TryCreateTransferCJAsync(Arguments args, TransferJobState state)
        {
            if (state == null)
                return;

            lock (state)
            {
                if (state.Failed || state.CjCreated || state.CjCreateInProgress
                    || !state.SourcePjCreated || !state.SourcePjQueued || !state.SourceTrackIn || !state.DestinationTrackIn
                    || state.SourceCarrierGeneration <= 0
                    || state.SourceCarrierGeneration != state.DestinationCarrierGeneration
                    || string.IsNullOrWhiteSpace(state.SourceCarrierId)
                    || string.IsNullOrWhiteSpace(state.DestinationCarrierId)
                    || state.SourceBatchInfo == null
                    || state.SourceSlotMap == null || state.SourceSlotMap.Count != 25
                    || state.DestinationSlotMap == null || state.DestinationSlotMap.Count != 25)
                {
                    return;
                }
                state.CjCreateInProgress = true;
            }

            var slots = GetTransferSlots(state.SourceBatchInfo);
            if (slots.Count == 0 || slots.Any(slot => slot > 25) || slots.Distinct().Count() != slots.Count
                || slots.Any(slot => state.SourceSlotMap[slot - 1] != 3 || state.DestinationSlotMap[slot - 1] != 3))
            {
                state.Failed = true;
                state.CjCreateInProgress = false;
                args.SetResultNG("Transfer source batch has no valid wafer slots.");
                return;
            }

            var success = false;
            try
            {
                if (!state.DestinationSimWaferStatusSet)
                {
                    if (!await Send_TOSwitchSimWafer(args, state.DestinationPortId, 0))
                    {
                        state.Failed = true;
                        return;
                    }

                    lock (state)
                    {
                        state.DestinationSimWaferStatusSet = true;
                    }
                }

                await Send_S14F9_CreateTransferCJ(args, state, slots);
                success = args.GetResultCode() == "OK";
            }
            finally
            {
                lock (state)
                {
                    state.CjCreateInProgress = false;
                    if (success)
                        state.CjCreated = true;
                    else
                        state.Failed = true;
                }
            }
        }

        private async Task CompleteTransferTrackOutAsync(Arguments args, TransferJobState state)
        {
            if (state == null)
                return;

            lock (state)
            {
                if (state.TrackOutCompleted || state.TrackOutInProgress
                    || state.SourceBatchInfo == null || state.DestinationBatchInfo == null)
                    return;
                state.TrackOutInProgress = true;
            }
            var success = false;
            try
            {
                var eqpId = args.Header[CItems.EqpId].ToString();
                var sourceLots = GetLotIds(state.SourceBatchInfo);
                var destinationLots = GetLotIds(state.DestinationBatchInfo);
                success = sourceLots.Count > 0 && destinationLots.Count > 0;
                if (success)
                {
                    success = await SecsExecution.RunAsync(args, "MES_LotEnd/Source",
                        () => MESClient.MES_LotEndLots(eqpId, state.SourcePortId.ToString(), state.SourceCarrierId, sourceLots));
                }
                if (success)
                {
                    success = await SecsExecution.RunAsync(args, "MES_LotEnd/Destination",
                        () => MESClient.MES_LotEndLots(eqpId, state.DestinationPortId.ToString(), state.DestinationCarrierId, destinationLots));
                }
                if (success)
                {
                    success = await SecsExecution.RunAsync(args, "MES_TrackOut/Source",
                        () => MESClient.MES_TrackOutLots(eqpId, state.SourcePortId.ToString(), state.SourceCarrierId, sourceLots));
                }
                if (success)
                {
                    success = await SecsExecution.RunAsync(args, "MES_TrackOut/Destination",
                        () => MESClient.MES_TrackOutLots(eqpId, state.DestinationPortId.ToString(), state.DestinationCarrierId, destinationLots));
                }
            }
            finally
            {
                lock (state)
                {
                    state.TrackOutInProgress = false;
                    state.TrackOutCompleted = success;
                    if (!success)
                        state.Failed = true;
                }
            }
        }

        private void CleanupTransferStateForPort(string eqpId, int portId, string carrierId)
        {
            var state = FindTransferStateForPort(eqpId, portId, carrierId);
            if (state == null)
                return;

            if (state.SourcePortId == portId)
            {
                if (!string.IsNullOrWhiteSpace(state.SourceCarrierId))
                    AppCache.Current.TransferBatchInfos?.TryRemove(GetTransferCarrierKey(eqpId, state.SourceCarrierId), out _);
                state.SourceCarrierId = null;
                state.SourceBatchInfo = null;
                state.SourceBatchId = null;
                state.SourceSlotMap = null;
                state.SourceTrackIn = false;
                state.SourcePjCreated = false;
                state.SourcePjQueued = false;
                state.SourceQueuedPjIds.Clear();
                state.DestinationSimWaferStatusSet = false;
            }
            else if (state.DestinationPortId == portId)
            {
                if (!string.IsNullOrWhiteSpace(state.DestinationCarrierId))
                    AppCache.Current.TransferBatchInfos?.TryRemove(GetTransferCarrierKey(eqpId, state.DestinationCarrierId), out _);
                state.DestinationCarrierId = null;
                state.DestinationBatchInfo = null;
                state.DestinationBatchId = null;
                state.DestinationSlotMap = null;
                state.DestinationTrackIn = false;
                state.DestinationSimWaferStatusSet = false;
            }

            if (string.IsNullOrWhiteSpace(state.SourceCarrierId)
                && string.IsNullOrWhiteSpace(state.DestinationCarrierId))
            {
                AppCache.Current.TransferJobStates.TryRemove(GetTransferStateKey(eqpId, state.SourcePortId), out _);
            }
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
                var sessionId = args.Header.ContainsKey(CItems.ConnectionSessionId)
                    ? Convert.ToInt64(args.Header[CItems.ConnectionSessionId])
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
                SecsMessage msg = new SecsMessage(1, 13);
                msg.SecsItem = Item.L();
                var reply = await SecsExecution.SendAsync(args, context.SecsGem, msg);

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

                if (!TrySetConnectionStatus(eqpId, version, sessionId,
                        ConnectStatus.CONNECTED, out var connectionStatusChanged))
                {
                    logger.Warn($"Ignoring stale S1F13 completion. EqpId={eqpId} Version={version} SessionId={sessionId}");
                    return;
                }

                if (connectionStatusChanged)
                    await MESClient.Send_EqpStateAsync(eqpId, EQPState.Connected, version);

                try
                {
                    var result = await QueryControlStateAsync(eqpId);
                    if (result.Success)
                    {
                        PublishControlState(eqpId, result.State.Value, force: true);
                        // A tool may already be in REMOTE when the EAP process is
                        // started (especially when a second tool starts later).
                        // In that case no CEID=10 transition is emitted to trigger
                        // event-report initialization, so initialize it here.
                        if (result.State.Value == ControlState.REMOTE
                            && AppCache.Current.EqpInfos.TryGetValue(eqpId, out var connectedEqp))
                        {
                            await EnsureEventReportsInitializedAsync(args, connectedEqp);
                        }
                    }
                    else
                    {
                        MESClient.Send_Message(eqpId, "0", $"{result.Message} Control-state refresh after connection failed.", LogType.WARNING);
                    }
                }
                catch (Exception ex)
                {
                    MESClient.Send_Message(eqpId, "0", $"S1F3/SVID29 control-state refresh failed after connection: {ex.Message}", LogType.WARNING);
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

        public async Task HsmsDisConnectAsync(Arguments args)
        {
            var eqpId = args.Header[CItems.EqpId].ToString();
            var version = args.Header.ContainsKey(CItems.ConnectionVersion)
                ? Convert.ToInt64(args.Header[CItems.ConnectionVersion])
                : 0;
            var sessionId = args.Header.ContainsKey(CItems.ConnectionSessionId)
                ? Convert.ToInt64(args.Header[CItems.ConnectionSessionId])
                : 0;
            if (!TrySetConnectionStatus(eqpId, version, sessionId,
                    ConnectStatus.DISCONNECTED, out var connectionStatusChanged))
            {
                return;
            }
            InvalidateControlStateQueries(eqpId);
            StopChannelPolling();
            if (connectionStatusChanged)
                await MESClient.Send_EqpStateAsync(eqpId, EQPState.DisConnected, version);
        }

        private bool TrySetConnectionStatus(string eqpId, long version, long sessionId,
            ConnectStatus status, out bool statusChanged)
        {
            statusChanged = false;
            lock (_connectionStateLock)
            {
                if (!_connectionVersions.TryGetValue(eqpId, out var latestVersion)
                    || version > latestVersion
                    || !_connectionSessionIds.TryGetValue(eqpId, out var latestSessionId)
                    || sessionId != latestSessionId)
                    return false;

                if (status == ConnectStatus.DISCONNECTED)
                {
                    if (version != latestVersion || !_connectionStates.TryGetValue(eqpId, out var currentState)
                        || (currentState != ConnectionState.Disabled
                            && currentState != ConnectionState.Reconnecting
                            && currentState != ConnectionState.Retry
                            && currentState != ConnectionState.Disconnected))
                        return false;
                }
                else if (!_connectionStates.TryGetValue(eqpId, out var currentState)
                    || (currentState != ConnectionState.Connected
                        && currentState != ConnectionState.Selecting
                        && currentState != ConnectionState.Selected
                        && currentState != ConnectionState.Communicating))
                {
                    return false;
                }

                if (AppCache.Current?.EqpInfos == null
                    || !AppCache.Current.EqpInfos.TryGetValue(eqpId, out var eqpInfo)
                    || eqpInfo == null)
                    return false;

                statusChanged = eqpInfo.ConnectStatus != status;
                eqpInfo.ConnectStatus = status;
                return true;
            }
        }

        private bool IsLatestConnectionVersion(string eqpId, long version)
        {
            lock (_connectionStateLock)
            {
                return !_connectionVersions.TryGetValue(eqpId, out var latestVersion) || version >= latestVersion;
            }
        }

        private async Task EnsureEventReportsInitializedAsync(Arguments args, EqpInfo equipment)
        {
            if (equipment == null || args?.Header == null
                || !args.Header.TryGetValue(CItems.EqpId, out var equipmentIdValue))
            {
                return;
            }

            var eqpId = equipmentIdValue?.ToString();
            if (string.IsNullOrWhiteSpace(eqpId))
            {
                return;
            }

            var initializationLock = _eventReportInitializationLocks.GetOrAdd(eqpId, _ => new SemaphoreSlim(1, 1));
            await initializationLock.WaitAsync();
            try
            {
                if (equipment.IsInitial)
                {
                    return;
                }

                await HsmsInitial(args);
                if (args.GetResultCode() == "OK")
                {
                    equipment.IsInitial = true;
                }
                else
                {
                    MESClient.Send_Message(eqpId, "0",
                        $"Event-report initialization failed after connection: {args.GetResultMsg()}",
                        LogType.WARNING);
                }
            }
            finally
            {
                initializationLock.Release();
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



