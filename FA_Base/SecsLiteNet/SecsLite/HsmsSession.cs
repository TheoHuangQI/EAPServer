#define DEBUG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FALogger;
using SecsLite.Model;
using Serilog;

namespace SecsLite
{
	public class HsmsSession
	{
		public enum SecsMessageLogStorageMode
		{
			FileByDate
		}

		private class LocalMessageReplyInfo
		{
			public SecsItem ReplyMessage = null;

			public ManualResetEvent ReplyEvent = new ManualResetEvent(initialState: false);
		}
		public virtual string ClassName => GetType().Name;
		private StringBuilder m_logMsgStringBuffer = new StringBuilder(1048576); //1M

		// 全局获取SECS和FDC日志器（首次调用自动初始化log4net）
		private static readonly ILogger _secsLogger = SerilogConfigHelper.GetSecsLogger();
		private static readonly ILogger _fdcLogger =  SerilogConfigHelper.GetFdcLogger();

		private IntPtr m_sessionHandle = IntPtr.Zero;

		private IntPtr m_passiveServerHandle = IntPtr.Zero;

		private SecsSets m_settings;
		private IntPtr SessionCount = IntPtr.Zero;
		private HsmsConfig m_cfg = null;

		private Dictionary<uint, LocalMessageReplyInfo> m_localMsgReplyInfoDict = new Dictionary<uint, LocalMessageReplyInfo>();

		private const uint LOCAL_MSGID_MIN = 4294966295u;

		private uint m_localMsgId = 4294966295u;

		private object m_localMsgIdLocker = new object();

		private readonly object m_sessionStateLocker = new object();

		private bool m_isConnected = false;

		private ConcurrentQueue<LogObj> log_queue;
		private AutoResetEvent m_reseteventLogEvent = new AutoResetEvent(false);
		private int m_logThreadStarted = 0;

		public bool LogEnabled { get; set; }

		public string LogDataDirectory { get; set; }

		public SecsMessageLogStorageMode LogMode { get; set; }

		public int LogFileReservedDays { get; set; }

		public bool IsConnected => m_isConnected;

		public int LastStartupErrorCode { get; private set; }

		public string LastStartupError { get; private set; } = string.Empty;

		public bool IsSelected
		{
			get
			{
				IntPtr sessionHandle;
				lock (m_sessionStateLocker)
				{
					sessionHandle = m_sessionHandle;
				}
				return sessionHandle != IntPtr.Zero && SecsCLibWrapper.IfSecsSelected(sessionHandle);
			}
		}

		// 日志队列最大容量：超出后丢弃旧日志（推荐10000，可根据内存调整）
		private const int MAX_LOG_QUEUE_COUNT = 10000;

		public HsmsSession()
		{
			LogEnabled = false;
			LogDataDirectory = "";
			LogMode = SecsMessageLogStorageMode.FileByDate;
			LogFileReservedDays = 30;
			GetSecsLogPara();
		}

		private void GetSecsLogPara()
		{
			string path = AppDomain.CurrentDomain.BaseDirectory + "..\\conf\\KingSEMIFA_Sys.ini";
			if (!File.Exists(path))
			{
				return;
			}

			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using StreamReader streamReader = new StreamReader(fileStream, Encoding.Default);
			string empty = string.Empty;
			while ((empty = streamReader.ReadLine()) != null)
			{
				if (empty.Contains("SecsLogEnable") && empty.Split('=').Length > 1)
				{
					LogEnabled = ((empty.Split('=')[1].ToUpper() == "TRUE") ? true : false);
				}
				else if (empty.Contains("SecsLogSaveDays"))
				{
					string[] array = empty.Split('=');
					if (array.Length > 1 && Regex.IsMatch(array[1], "^\\d+$"))
					{
						LogFileReservedDays = Convert.ToInt32(array[1]);
					}
				}
			}
		}

		private string GetNativeLastErrorMessage(IntPtr handle)
		{
			try
			{
				byte[] buffer = new byte[512];
				int len = SecsCLibWrapper.SecsGetLastErrorMessage(handle, buffer, buffer.Length);
				if (len <= 0)
				{
					return string.Empty;
				}
				return Encoding.Default.GetString(buffer, 0, len);
			}
			catch
			{
				return string.Empty;
			}
		}

		

		private void logSecsMessage(object obj)
		{
			while (true)
			{
				try
				{
					m_reseteventLogEvent.WaitOne();
					lock (m_logMsgStringBuffer)
					{
						if (log_queue.Count > 0)
						{
							LogObj log;
							bool res= log_queue.TryDequeue(out log);
                            if (res && log != null)
                            {
								m_logMsgStringBuffer.Append(log.Time);
								string text = (log.fromHost ? "H->E" : "E->H");
								m_logMsgStringBuffer.Append($"S{log.stream}F{log.func} [{text}, {log.msgId}]\r\n");
								SecsUtil.SecsMessageToSML(log.item, m_logMsgStringBuffer, 1);
								m_logMsgStringBuffer.Append("\r\n");
								if (m_logMsgStringBuffer.Length > 0)
								{
									if ((log.stream == 1 && log.func == 3) || (log.stream == 1 && log.func == 4) || (log.stream == 6 && log.func == 1) || (log.stream == 6 && log.func == 2))
									{
										_fdcLogger.Information(m_logMsgStringBuffer.ToString());
									}
									else
									{
										_secsLogger.Information(m_logMsgStringBuffer.ToString());
									}
									m_logMsgStringBuffer.Clear();
								}
							}
							
						}
						if (log_queue.Count > 0)
						{
							m_reseteventLogEvent.Set();
						}
					}
				}
				catch (Exception ex)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS Log error: {ex.StackTrace + "\r\n" + ex.Message}");
				}
			}
		}

		public void Start(HsmsConfig cfg)
		{
			LastStartupErrorCode = 0;
			LastStartupError = string.Empty;
			if (log_queue == null)
			{
				log_queue = new ConcurrentQueue<LogObj>();
			}
			if (Interlocked.CompareExchange(ref m_logThreadStarted, 1, 0) == 0)
			{
				new Thread(() => logSecsMessage(this)) { IsBackground = true }.Start();
			}
			m_cfg = cfg;
			try
			{
				IPAddress iPAddress = IPAddress.Parse(cfg.LocalIP);
				m_settings = new SecsSets
				{
					mode = 0,
					hsmsMode = (byte)cfg.Mode,
					t3 = cfg.T3,
					t5 = cfg.T5,
					t6 = cfg.T6,
					t7 = cfg.T7,
					t8 = cfg.T8,
					t1 = 1000,
					t2 = 2000,
					t3_secsi = 45000,
					t4 = 45000,
					LinkTestInterval = cfg.LinkTestInterval,
					sLocalIP = BitConverter.ToUInt32(iPAddress.GetAddressBytes(), 0),
					usLocalPort = cfg.LocalPort,
					usDeviceID = cfg.DeviceID,
					szComPort="COM5",
					baudRate=9600,
					ms= (byte)SECSIMode.SLAVE,
					delegateOnConnect = sessionOnConnect,
					delegateOnClose = sessionOnDisonnect,
					delegateOnRecv = null,
					delegateOnRecvRawData = sessionOnReceiveRawData,
					delegateOnReplyTimeout = sessionOnReplyTimeout
				};
                if (m_settings.mode == 0)
                {
					if (cfg.Mode == HsmsMode.Active)
					{
						IPAddress iPAddress2 = IPAddress.Parse(cfg.RemoteIP);
						m_settings.sRemoteIP = BitConverter.ToUInt32(iPAddress2.GetAddressBytes(), 0);
						m_settings.usRemotePort = cfg.RemotePort;
						m_sessionHandle = SecsCLibWrapper.CreateSecs(ref m_settings);
						if (m_sessionHandle == IntPtr.Zero)
						{
							LastStartupErrorCode = 1;
							LastStartupError = GetNativeLastErrorMessage(m_sessionHandle);
							Console.WriteLine($"Device[{cfg.DeviceID}] start ACTIVE mode at [{cfg.RemoteIP}:{cfg.RemotePort}] failed. ");
							Console.WriteLine("CreateSecs failed. ");
							Console.WriteLine($"Device[{cfg.DeviceID}] native error: {LastStartupError}");
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS error: {SecsResult.ConfigError}");
							return;
						}
						Console.WriteLine($"[Device:{cfg.DeviceID}] Connecting to {cfg.RemoteIP}:{m_settings.usRemotePort}");
						bool flag = SecsCLibWrapper.SecsConnect(m_sessionHandle);
						if (flag)
						{
							Console.WriteLine($"[Device:{cfg.DeviceID}] TCP connected. Address={cfg.RemoteIP}:{m_settings.usRemotePort}");
						}
						if (!flag)
						{
							var nativeError = GetNativeLastErrorMessage(m_sessionHandle);
							LastStartupErrorCode = 1;
							LastStartupError = nativeError;
							Console.WriteLine($"[Device:{cfg.DeviceID}] HSMS connection failed. Address={cfg.RemoteIP}:{m_settings.usRemotePort}, Error=ERROR 1, Reason={nativeError}");
							FALoggerClass.Inst().Warn($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS warning: {SecsResult.ConnectFailed}, Device={cfg.DeviceID}, Address={cfg.RemoteIP}:{m_settings.usRemotePort}, Reason={nativeError}");
							SecsCLibWrapper.DestroySecs(m_sessionHandle);
							m_sessionHandle = IntPtr.Zero;
							return;
						}
					}
					else
					{
						Console.WriteLine($"Device[{cfg.DeviceID}] start PASSIVE mode at [{cfg.LocalIP}:{cfg.LocalPort}] ... ");
						m_passiveServerHandle = SecsCLibWrapper.CreateSecs(ref m_settings);
						if (m_passiveServerHandle == IntPtr.Zero)
						{
							LastStartupErrorCode = 1;
							LastStartupError = GetNativeLastErrorMessage(m_passiveServerHandle);
							Console.WriteLine($"Device[{cfg.DeviceID}] start PASSIVE mode at [{cfg.LocalIP}:{cfg.LocalPort}] failed. ERROR 1 ");
							Console.WriteLine($"Device[{cfg.DeviceID}] native error: {LastStartupError}");
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS error: {SecsResult.ConfigError}");
							return;
						}
						if (!SecsCLibWrapper.SecsStartupServer(m_passiveServerHandle))
						{
							LastStartupErrorCode = 2;
							LastStartupError = GetNativeLastErrorMessage(m_passiveServerHandle);
							Console.WriteLine($"Device[{cfg.DeviceID}] start PASSIVE mode at [{cfg.LocalIP}:{cfg.LocalPort}] failed. ERROR 2 ");
							Console.WriteLine($"Device[{cfg.DeviceID}] native error: {LastStartupError}");
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS error: {SecsResult.StartPassiveFailed}");
							SecsCLibWrapper.DestroySecs(m_passiveServerHandle);
							m_passiveServerHandle = IntPtr.Zero;
							return;
						}
						Console.WriteLine($"Device[{cfg.DeviceID}] start PASSIVE mode at [{cfg.LocalIP}:{cfg.LocalPort}] success. ");
					}
				}
                else
                {
					Console.WriteLine($"Device[{cfg.DeviceID}] start SECS I mode at [{cfg.LocalIP}:{cfg.LocalPort}] ... ");
					 m_sessionHandle = SecsCLibWrapper.CreateSecs(ref m_settings);
					if (m_sessionHandle == IntPtr.Zero)
					{
						LastStartupErrorCode = 1;
						LastStartupError = GetNativeLastErrorMessage(m_sessionHandle);
						Console.WriteLine($"Device[{cfg.DeviceID}] start PASSIVE mode at [{cfg.LocalIP}:{cfg.LocalPort}] failed. ERROR 1 ");
						Console.WriteLine($"Device[{cfg.DeviceID}] native error: {LastStartupError}");
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS error: {SecsResult.ConfigError}");
						return;
					}
					if (!SecsCLibWrapper.SecsStartupServer(m_sessionHandle))
					{
						LastStartupErrorCode = 2;
						LastStartupError = GetNativeLastErrorMessage(m_sessionHandle);
						Console.WriteLine($"Device[{cfg.DeviceID}] start SECS I at [{cfg.LocalIP}:{cfg.LocalPort}] failed. ERROR 2 ");
						Console.WriteLine($"Device[{cfg.DeviceID}] native error: {LastStartupError}");
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS error: {SecsResult.StartPassiveFailed}");
						SecsCLibWrapper.DestroySecs(m_sessionHandle);
						m_sessionHandle = IntPtr.Zero;
						return;
					}
					Console.WriteLine($"Device[{cfg.DeviceID}] start SECS I mode at [{cfg.LocalIP}:{cfg.LocalPort}] success. ");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("SecsGEM startup failed. error info: ");
				Console.WriteLine(ex.Message);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: SECS error: {SecsResult.UnhandledError}");
			}
		}

		public SecsResult Stop()
		{
			if (m_cfg == null)
			{
				return SecsResult.Success;
			}
			IntPtr sessionHandle;
			IntPtr passiveServerHandle;
			bool shouldNotifyDisconnect;
			lock (m_sessionStateLocker)
			{
				sessionHandle = m_sessionHandle;
				passiveServerHandle = m_passiveServerHandle;
				shouldNotifyDisconnect = m_isConnected || sessionHandle != IntPtr.Zero || passiveServerHandle != IntPtr.Zero;
				m_isConnected = false;
				SessionCount = IntPtr.Zero;
				m_sessionHandle = IntPtr.Zero;
				m_passiveServerHandle = IntPtr.Zero;
			}
			if (m_cfg.Mode == HsmsMode.Active)
			{
				if (sessionHandle != IntPtr.Zero)
				{
					SecsCLibWrapper.SecsDisconnect(sessionHandle);
					SecsCLibWrapper.DestroySecs(sessionHandle);
				}
			}
			else
			{
				if (passiveServerHandle != IntPtr.Zero)
				{
					SecsCLibWrapper.SecsCloseServer(passiveServerHandle);
					SecsCLibWrapper.DestroySecs(passiveServerHandle);
				}
				if (sessionHandle != IntPtr.Zero)
				{
					SecsCLibWrapper.SecsDisconnect(sessionHandle);
					SecsCLibWrapper.DestroySecs(sessionHandle);
				}
			}
			if (shouldNotifyDisconnect)
			{
				OnDisconnect();
			}
			return SecsResult.Success;
		}

		public uint SendMessage(byte stream, byte func, SecsItem item, bool needReply)
		{
			uint num = 0u;
			try
			{
				if(item != null)
                {
					IntPtr intPtr = Marshal.AllocHGlobal(item.GetItemDataSize());
					int num2 = SecsItem.FillToBuffer(intPtr, item);
					Debug.Assert(num2 == item.GetItemDataSize());
					if (SecsCLibWrapper.IfSecsconnect(m_sessionHandle))
					{
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Eap连接正常!，send Event");
						num = SecsCLibWrapper.SecsSendMessageBuffer(m_sessionHandle, stream, func, intPtr, num2, needReply ? 1u : 2u, uint.MaxValue);
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: stream={stream} func={func} messageid={num}");
						Marshal.FreeHGlobal(intPtr);
						if (LogEnabled)
						{
							LogObj log = new LogObj
							{
								Time= DateTime.Now.ToString("HH:mm:ss.fff "),
								stream = stream,
								func = func,
								item = item,
								msgId=num,
								fromHost = false,
							};
							messageEnqueue(log);
						}
						return num;
					}
                    else
                    {
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Eap连接失败！！！");
						return num;
					}
				}
                else
                {
					IntPtr intPtr = IntPtr.Zero;
					if (SecsCLibWrapper.IfSecsconnect(m_sessionHandle))
                    {
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Eap连接正常!，send Event");
						num = SecsCLibWrapper.SecsSendMessageBuffer(m_sessionHandle, stream, func, intPtr, 0, needReply ? 1u : 2u, uint.MaxValue);
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: stream={stream} func={func} messageid={num}");
						Marshal.FreeHGlobal(intPtr);
						if (LogEnabled)
						{
							LogObj log = new LogObj
							{
								Time = DateTime.Now.ToString("HH:mm:ss.fff "),
								stream = stream,
								func = func,
								item = item,
								msgId = num,
								fromHost = false,
							};
							messageEnqueue(log);
						}
						return num;
					}
                    else
                    {
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Eap连接失败！！！");
						return num;
					}
				}
			}
			catch (Exception)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: stream={stream} func={func}Message  send Fail!! ");
			}
			return num;
		}

		public uint ReplyMessage(byte stream, byte func, SecsItem item, uint messageId)
		{
			
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: stream={stream} func={func} messageid={messageId} ");
			if (messageId >= 4294966295u)
			{
				lock (m_localMsgReplyInfoDict)
				{
					if (m_localMsgReplyInfoDict.ContainsKey(messageId))
					{
						LocalMessageReplyInfo localMessageReplyInfo = m_localMsgReplyInfoDict[messageId];
						m_localMsgReplyInfoDict.Remove(messageId);
						localMessageReplyInfo.ReplyMessage = item;
						localMessageReplyInfo.ReplyEvent.Set();
						FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:  m_localMsgReplyInfoDict is Contains messageid, stream={stream} func={func} messageid={messageId} ");
						return 0u;
					}
				}
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]:Reply-Message-(Message>=LOCAL_MSGID_MIN), stream={stream} func={func} messageid={messageId} ");
				return 0u;
			}
			if (item == null)
			{
				uint num = SecsCLibWrapper.SecsSendMessageBuffer(m_sessionHandle, stream, func, IntPtr.Zero, 0, 0u, messageId);
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Reply-Message-(item=null),stream={stream} func={func} messageid={messageId} ");
				if (LogEnabled)
				{
					LogObj log = new LogObj
					{
						Time = DateTime.Now.ToString("HH:mm:ss.fff "),
						stream = stream,
						func = func,
						item = item,
						msgId = messageId,
						fromHost = false,
					};
					messageEnqueue(log);
					
				}
				return num;
			}
			uint num2 = 0u;
			try
			{
				IntPtr intPtr = Marshal.AllocHGlobal(item.GetItemDataSize());
				int num3 = SecsItem.FillToBuffer(intPtr, item);
				Debug.Assert(num3 == item.GetItemDataSize());
				num2 = SecsCLibWrapper.SecsSendMessageBuffer(m_sessionHandle, stream, func, intPtr, num3, 0u, messageId);
				Marshal.FreeHGlobal(intPtr);
				if (LogEnabled)
				{
					LogObj log = new LogObj
					{
						Time = DateTime.Now.ToString("HH:mm:ss.fff "),
						stream = stream,
						func = func,
						item = item,
						msgId = messageId,
						fromHost = false,
					};
					messageEnqueue(log);
				}
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Reply-Message,stream={stream} func={func} messageid={messageId} ");
			}
			catch (Exception)
			{
				FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Reply-Message Error Message reply Fail,stream={stream} func={func} messageid={messageId} ");
			}
			return num2;
		}

		private void messageEnqueue(LogObj log)
        {
            if (log == null) return;
            try
            {
                // 原有队列容量限制逻辑...
                if (log_queue.Count >= MAX_LOG_QUEUE_COUNT)
                {
                    log_queue.TryDequeue(out _);
                    FALoggerClass.Inst().Warn($"日志队列超出最大容量{MAX_LOG_QUEUE_COUNT}，已丢弃旧日志！");
					return;
                }
                log_queue.Enqueue(log);
                m_reseteventLogEvent.Set();
            }
            finally
            {
                m_reseteventLogEvent.Set();
            }
        }

		private uint generateLocalMessageId()
		{
			lock (m_localMsgIdLocker)
			{
				m_localMsgId++;
				if (m_localMsgId < 4294966295u)
				{
					m_localMsgId = 4294966295u;
				}
				return m_localMsgId;
			}
		}

		public SecsItem ProcessMessage(byte stream, byte func, SecsItem item, bool needReply, bool IsEapSend = true)
		{
			uint num = generateLocalMessageId();
			Console.WriteLine($"ProcessMessage: msg_id = {num}");
			if (!needReply)
			{
				OnReceiveMessage(stream, func, item, num, IsEapSend);
				return null;
			}
			LocalMessageReplyInfo localMessageReplyInfo = new LocalMessageReplyInfo();
			lock (m_localMsgReplyInfoDict)
			{
				m_localMsgReplyInfoDict[num] = localMessageReplyInfo;
			}
			if (OnReceiveMessage(stream, func, item, num, IsEapSend) != 0)
			{
				return null;
			}
			if (localMessageReplyInfo.ReplyEvent.WaitOne())
			{
			}
			return localMessageReplyInfo.ReplyMessage;
		}

		public virtual bool OnConnect(HsmsSessionInfo info)
		{
			Console.WriteLine("connected. ");
			FALoggerClass.Inst().Info($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: EAP_Connect");
			lock (m_sessionStateLocker)
			{
				m_sessionHandle = info.sessionHandle;
			}
            return true;
		}

		public virtual void OnDisconnect()
		{
		}

		public virtual SecsErrorCode OnReceiveMessage(byte stream, byte func, SecsItem item, uint messageId, bool IsEapSend = true)
		{
			if (LogEnabled)
			{
				LogObj log = new LogObj
				{
					Time = DateTime.Now.ToString("HH:mm:ss.fff "),
					stream = stream,
					func = func,
					item = item,
					msgId = messageId,
					fromHost = true,
				};
				messageEnqueue(log);
			}
			return SecsErrorCode.Success;
		}

		public virtual void OnMessageReplyTimeout(uint messageId)
		{
		}

		private bool sessionOnConnect(IntPtr hServer, IntPtr hSecs, ushort usDevId)
		{
			Console.WriteLine($"connected. device_id = {usDevId}");
			HsmsSessionInfo info = new HsmsSessionInfo
			{
				DeviceId = usDevId,
				sessionHandle = hSecs
			};
			lock (m_sessionStateLocker)
			{
				if (m_isConnected)
				{
					return false;
				}
				SessionCount = hSecs;
			}
				bool connected = OnConnect(info);
				lock (m_sessionStateLocker)
				{
					if (SessionCount != hSecs)
					{
						if (m_sessionHandle == hSecs)
						{
							m_sessionHandle = IntPtr.Zero;
						}
						return false;
					}
					if (connected)
					{
						m_isConnected = true;
						return true;
					}
					SessionCount = IntPtr.Zero;
					if (m_sessionHandle == hSecs)
					{
						m_sessionHandle = IntPtr.Zero;
					}
					m_isConnected = false;
					return false;
				}
			}

		private void sessionOnDisonnect(IntPtr hSecs)
		{
			bool shouldNotify = false;
			lock (m_sessionStateLocker)
			{
				if (hSecs == SessionCount || hSecs == m_sessionHandle)
				{
					if (hSecs == SessionCount)
					{
						SessionCount = IntPtr.Zero;
					}
					if (hSecs == m_sessionHandle)
					{
						m_sessionHandle = IntPtr.Zero;
					}
					m_isConnected = false;
					shouldNotify = true;
				}
			}
			if (shouldNotify)
			{
				OnDisconnect();
			}
		}

		private uint sessionOnReceiveMessage(IntPtr hSecs, uint dwId, byte ucS, byte ucF, IntPtr data)
		{
			return (uint)OnReceiveMessage(ucS, ucF, null, dwId);
		}

		private int sessionOnReceiveRawData(IntPtr hSecs, uint dwId, byte ucS, byte ucF, IntPtr data, uint dataLen)
		{
			SecsItem item = null;
			int num = SecsItem.LoadFromBuffer(data, dataLen, ref item);
			if (num != dataLen)
			{
				return -7;
			}
			if (item != null && dataLen != item.GetItemDataSize())
			{
				Console.WriteLine($"msg[{ucS},{ucF}] length mismatch. {dataLen} / {item.GetItemDataSize()}");
			}
			return (int)OnReceiveMessage(ucS, ucF, item, dwId);
            //switch (secsErrorCode)
            //{
            //    case SecsErrorCode.ErrorDeviceId:
            //        ReplyMessage(9, 1, item, dwId);
            //        break;
            //    case SecsErrorCode.ErrorStreamType:
            //        ReplyMessage(9, 3, item, dwId);
            //        break;
            //    case SecsErrorCode.ErrorFunctionType:
            //        ReplyMessage(9, 5, item, dwId);
            //        break;
            //    case SecsErrorCode.ErrorIllegalData:
            //        ReplyMessage(9, 7, item, dwId);
            //        break;
            //    case SecsErrorCode.ErrorTransTimeout:
            //        ReplyMessage(9, 9, item, dwId);
            //        break;
            //    case SecsErrorCode.ErrorDataTooLong:
            //        ReplyMessage(9, 11, item, dwId);
            //        break;
            //}
		}

		private void sessionOnReplyTimeout(IntPtr hSecs, uint msgId)
		{
			OnMessageReplyTimeout(msgId);
		}
	}
}
