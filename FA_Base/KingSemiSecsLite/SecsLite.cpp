#include "pch.h"
#include "SecsLite.h"
#include "HsmsServer.h"
#include "SECSIServer.h"
#include "HsmsUtils.h"
#include "ErrorManager.h"
#include "SecsLogger.h"

// Helper: get current local time as "HH:mm:ss.mmm"
static void get_timestamp(char* buf, int len) {
    SYSTEMTIME st;
    GetLocalTime(&st);
    sprintf_s(buf, len, "%02d:%02d:%02d.%03d", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
}

static int NormalizeSecsErrorFunction(int result) {
    switch (result) {
        case -1:
            return 1;   // Unrecognized Device ID
        case -3:
            return 3;   // Unrecognized Stream Type
        case -5:
            return 5;   // Unrecognized Function Type
        case -7:
            return 7;   // Illegal Data
        // case -9:
        //  return 9;   // Transaction Timer Timeout
        case -11:
            return 11;  // Data Too Long
        default:
            return 0;
    }
}

SecsLite::SecsLite()
    : m_pHsmsServer(nullptr),
      m_pSecsiServer(nullptr),
      m_pTimeoutManager(nullptr),
      m_isInitialized(false),
      m_isConnected(false),
      m_lastError(0),
      m_nextMsgId(1),
      m_hSecsHandle(reinterpret_cast<LONG_PTR>(this)) {
    memset(m_lastErrorMessage, 0, sizeof(m_lastErrorMessage));
}

SecsLite::~SecsLite() {
    CloseServer();
}

bool SecsLite::Initialize(const SecsSets* pSets) {
    if (pSets == nullptr) {
        SetError(-1, "Invalid configuration");
        return false;
    }

    memcpy_s(&m_config, sizeof(m_config), pSets, sizeof(SecsSets));

    SecsLogger::Debug("SecsLite::Initialize: LinkTestInterval from C# = %u ms", m_config.LinkTestInterval);

    if (m_config.mode == SECS_MODE_HSMS) {
        m_pHsmsServer = new HsmsServer();
        if (m_pHsmsServer == nullptr) {
            SetError(-2, "Failed to create HSMS server");
            return false;
        }
    } else if (m_config.mode == SECS_MODE_SECSI) {
        m_pSecsiServer = new SECSIServer();
        if (m_pSecsiServer == nullptr) {
            SetError(-3, "Failed to create SECS I server");
            return false;
        }
    } else {
        SetError(-4, "Invalid mode");
        return false;
    }

    m_pTimeoutManager = new TimeoutManager();
    if (m_pTimeoutManager != nullptr) {
        // 对于SECS I模式，使用SECS I的T3值；对于HSMS模式，使用HSMS的T3值
        unsigned int t3_value = (m_config.mode == SECS_MODE_SECSI && m_config.t3_secsi > 0) ?
                                m_config.t3_secsi : (m_config.t3 ? m_config.t3 : 30000);

        m_pTimeoutManager->SetTimeout(
            t3_value,                           // T3: 设备响应超时
            m_config.t5 ? m_config.t5 : 10000,  // T5: Select请求超时，行业默认10秒
            m_config.t6 ? m_config.t6 : 5000,   // T6: Reject超时，行业默认5秒
            m_config.t7 ? m_config.t7 : 10000,  // T7: Separate超时，行业默认10秒
            m_config.t8 ? m_config.t8 : 5000    // T8: 字间隔超时，行业默认5秒
        );
    }

    // ======= 配置 T6 超时 =========
    if (m_config.mode == SECS_MODE_HSMS && m_pHsmsServer != nullptr && m_pTimeoutManager != nullptr) {
        m_pHsmsServer->SetTimeoutManager(m_pTimeoutManager);

        m_pHsmsServer->SetControlTimeoutCallback([this]() {
            SecsLogger::Error("T6 control timeout Triggered, disconnecting...");
            this->Disconnect();
        });
    }

    // 设置需要记录的日志级别
    SecsLogger::SetMinLevel(SecsLogLevel::Info);

    // ===== 配置LinkTest周期保活 + T8 ===========
    if (m_config.mode == SECS_MODE_HSMS && m_pHsmsServer != nullptr)
    {
        // 设置LinkTest间隔
        m_pHsmsServer->SetLinkTestInterval(m_config.LinkTestInterval);

        // 无T3等待时才允许发LinkTest
        m_pHsmsServer->SetDataIdleCheckCallback([this]() -> bool
        {
            return !this->HasPendingMessages();
        });
        m_pHsmsServer->SetT8Timeout(m_config.t8 ? m_config.t8 : 5000);
    }

    m_isInitialized = true;
    return true;
}

bool SecsLite::StartupServer() {
    if (!m_isInitialized) {
        SetError(-1, "Not initialized");
        return false;
    }

    if (m_config.mode == SECS_MODE_HSMS) {
        if (m_pHsmsServer == nullptr) {
            SetError(-2, "HSMS server not created");
            return false;
        }

        m_pHsmsServer->SetExpectedDeviceId(m_config.usDeviceID);

        m_pHsmsServer->SetConnectionCallback([this](bool connected, const std::string& ip) {
            this->OnHsmsConnected(connected, ip);
        });

        m_pHsmsServer->SetHsmsMessageCallback([this](const HsmsMessage& msg) {
            this->OnHsmsMessageReceived(msg);
        });

        // 初始化 ErrorManager
        m_pErrorManager = std::make_unique<ErrorManager>(m_pHsmsServer, m_config.usDeviceID, this);
        // 注册回调
        m_pHsmsServer->SetDeviceIdMismatchCallback([this](const HsmsMessage& msg) {
            if (m_pErrorManager) {
                cout << "Send S9F1" << endl;
                m_pErrorManager->SendError(1, msg);
            }
            });

        bool result = m_pHsmsServer->StartServer(m_config.usLocalPort);
        if (!result) {
            SetError(-3, "Failed to start HSMS server");
        }
        return result;
    } else if (m_config.mode == SECS_MODE_SECSI) {
        if (m_pSecsiServer == nullptr) {
            SetError(-4, "SECS I server not created");
            return false;
        }

        m_pSecsiServer->SetConnectionCallback([this](bool connected, const std::string& port) {
            this->OnSecsiConnected(connected, port);
        });

        m_pSecsiServer->SetSECSIMessageCallback([this](const SECSIMessage& msg) {
            this->OnSecsiMessageReceived(msg);
        });

        SerialConfig serialConfig;
        serialConfig.portName = m_config.szComPort;
        serialConfig.baudRate = m_config.baudRate ? m_config.baudRate : 9600;
        serialConfig.dataBits = 8;
        serialConfig.stopBits = 1;
        serialConfig.parity = 'N';
        // 将 SECS-I 超时配置传给 SerialModule
        serialConfig.t1 = m_config.t1 > 0 ? m_config.t1 : serialConfig.t1;
        serialConfig.t2 = m_config.t2 > 0 ? m_config.t2 : serialConfig.t2;

        bool result = m_pSecsiServer->StartServer(serialConfig);
        if (!result) {
            SetError(-5, "Failed to start SECS I server");
            return result;
        }

        // 设置SECS I超时参数
        if (m_config.t1 > 0) {
            m_pSecsiServer->SetT1Timeout(m_config.t1);
        }
        if (m_config.t2 > 0) {
            m_pSecsiServer->SetT2Timeout(m_config.t2);
        }
        if (m_config.t3_secsi > 0) {
            m_pSecsiServer->SetT3Timeout(m_config.t3_secsi);
        }
        if (m_config.t4 > 0) {
            m_pSecsiServer->SetT4Timeout(m_config.t4);
        }

        return result;
    }

    SetError(-6, "Invalid mode");
    return false;
}

void SecsLite::CloseServer() {
    Disconnect();

    if (m_pHsmsServer != nullptr) {
        delete m_pHsmsServer;
        m_pHsmsServer = nullptr;
    }

    if (m_pSecsiServer != nullptr) {
        delete m_pSecsiServer;
        m_pSecsiServer = nullptr;
    }

    if (m_pTimeoutManager != nullptr) {
        delete m_pTimeoutManager;
        m_pTimeoutManager = nullptr;
    }

    m_isInitialized = false;
}

bool SecsLite::Connect() {
    if (!m_isInitialized) {
        SetError(-1, "Not initialized");
        return false;
    }

    if (m_config.mode == SECS_MODE_HSMS) {
        if (m_pHsmsServer == nullptr) {
            SetError(-2, "HSMS server not created");
            return false;
        }

        m_pHsmsServer->SetExpectedDeviceId(m_config.usDeviceID);

        m_pHsmsServer->SetConnectionCallback([this](bool connected, const std::string& ip) {
            this->OnHsmsConnected(connected, ip);
        });

        m_pHsmsServer->SetHsmsMessageCallback([this](const HsmsMessage& msg) {
            this->OnHsmsMessageReceived(msg);
        });

        // 初始化 ErrorManager
        m_pErrorManager = std::make_unique<ErrorManager>(m_pHsmsServer, m_config.usDeviceID, this);
        // 注册回调
        m_pHsmsServer->SetDeviceIdMismatchCallback([this](const HsmsMessage& msg) {
            if (m_pErrorManager) {
                m_pErrorManager->SendError(1, msg);
            }
            });

        bool result = m_pHsmsServer->ConnectToHost(m_config.sRemoteIP, m_config.usRemotePort);
        if (!result) {
            SetError(-3, "Failed to connect to HSMS server");
        }

        return result;
    } else if (m_config.mode == SECS_MODE_SECSI) {
        return StartupServer();
    }

    SetError(-4, "Invalid mode");
    return false;
}

void SecsLite::Disconnect() {
    if (m_config.mode == SECS_MODE_HSMS && m_pHsmsServer != nullptr) {
        m_pHsmsServer->Disconnect();
    } else if (m_config.mode == SECS_MODE_SECSI && m_pSecsiServer != nullptr) {
        m_pSecsiServer->StopServer();
    }

    m_isConnected = false;

    if (m_pTimeoutManager != nullptr) {
        std::lock_guard<std::mutex> lock(m_pendingMutex);
        for (const auto& pair : m_pendingMessages) {
            m_pTimeoutManager->StopTimer(pair.first);
        }
        m_pendingMessages.clear();
    }
}

int SecsLite::SendSecsMessage(unsigned char stream, unsigned char func,
                          const unsigned char* dataBuffer, int dataLen,
                          unsigned int flag, unsigned int msgId) {
    if (!m_isInitialized) {
        SetError(-1, "Not initialized");
        return -1;
    }

    if (!m_isConnected) {
        SetError(-2, "Not connected");
        return -1;
    }
    unsigned int systemBytes;
    if (flag == 0) {
        systemBytes = msgId;
    } else {
        std::lock_guard<std::mutex> lock(m_msgIdMutex);
        if (m_nextMsgId > msgId) {
            m_nextMsgId = 1;
        }
        systemBytes = m_nextMsgId++;
    }

    if (m_config.mode == SECS_MODE_HSMS) {
        if (m_pHsmsServer == nullptr) {
            SetError(-3, "HSMS server not created");
            return -1;
        }

        // 直接使用C#端已编码的数据
        HsmsMessage msg;
        msg.header.sType = HsmsMessageType::DataMessage;
        msg.header.deviceId = m_config.usDeviceID;
        msg.header.systemBytes = systemBytes;
        msg.header.stream = stream;
        msg.header.function = func;
        msg.header.wbit = (flag == 1);
        msg.header.pType = 0;
        if (dataBuffer != nullptr && dataLen > 0) {
            msg.data = new unsigned char[dataLen];
            memcpy_s(msg.data, dataLen, dataBuffer, dataLen);
            msg.dataLength = dataLen;
        } else {
            msg.data = nullptr;
            msg.dataLength = 0;
        }

        // T3超时
        if (msg.header.wbit && (m_pTimeoutManager != nullptr))
        {
            PendingMessage pending;
            pending.msgId = systemBytes;
            pending.timeoutType = 3;
            // 记录开始时间
            pending.startTime = std::chrono::steady_clock::now();
            // 记录原始消息头
            pending.Header = HsmsUtils::PackHeader(msg);
            {
                std::lock_guard<std::mutex> lock(m_pendingMutex);
                m_pendingMessages[systemBytes] = pending;
            }
            // 启动定时器
            // 此处可以添加T3超时开始计时日志
            m_pTimeoutManager->StartTimer(systemBytes, pending.timeoutType,
                [this](unsigned int id) { this->OnTimeout(id); });
        }

        int result = m_pHsmsServer->SendHsmsMessage(msg);
        HsmsServer::FreeMessageData(msg);

        if (result <= 0) {
            SetError(-4, "Failed to send HSMS message");
            return -1;
        }

        return systemBytes;
    }
    else if (m_config.mode == SECS_MODE_SECSI) {
        if (m_pSecsiServer == nullptr) {
            SetError(-5, "SECS I server not created");
            return -1;
        }

        // 直接使用C#端已编码的数据（结构与HSMS一致）
        SECSIMessage msg;
        msg.header.deviceId = m_config.usDeviceID;
        msg.header.stream = stream;
        msg.header.function = func;
        msg.header.wbit = (flag == 1);
        msg.header.systemBytes = systemBytes;
        msg.header.ms = m_config.ms==0?SECSIMode::MASTER: SECSIMode::SLAVE;
        msg.header.blockNumber = 1;
        msg.header.endBit = true;
        if (dataBuffer != nullptr && dataLen > 0) {
            msg.data = new unsigned char[dataLen];
            memcpy_s(msg.data, dataLen, dataBuffer, dataLen);
            msg.dataLength = dataLen;
        } else {
            msg.data = nullptr;
            msg.dataLength = 0;
        }
        int result = m_pSecsiServer->SendSECSIMessage(msg);
        SECSIServer::FreeMessageData(msg);

        if (result <= 0) {
            SetError(-6, "Failed to send SECS I message");
            return -1;
        }

        return systemBytes;
    }

    SetError(-7, "Invalid mode");
    return -1;
}

void SecsLite::OnTimeout(unsigned int msgId) {
    PendingMessage pending;

    {
        std::lock_guard<std::mutex> lock(m_pendingMutex);
        auto it = m_pendingMessages.find(msgId);
        if (it != m_pendingMessages.end()) {

            auto end = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(end - it->second.startTime).count();
            // 此处应添加超时触发日志
            it->second.timeoutType;

            pending = std::move(it->second);
            m_pendingMessages.erase(it);
        }
    }

    // 发送S9F9消息
    if (!pending.Header.empty() && m_pErrorManager) {
        m_pErrorManager->SendError(9, pending.Header, msgId);
    }

    if (m_config.delegateOnReplyTimeout) {
        m_config.delegateOnReplyTimeout(m_hSecsHandle, msgId);
    }
}

// 使用systemBytes作为消息的唯一标识
void SecsLite::HandleReply(unsigned int systemBytes) {
    std::lock_guard<std::mutex> lock(m_pendingMutex);
    auto it = m_pendingMessages.find(systemBytes);
    if (it != m_pendingMessages.end())
    {
        if (m_pTimeoutManager != nullptr)
        {
            m_pTimeoutManager->StopTimer(systemBytes);
        }
        m_pendingMessages.erase(it);
    }
}

bool SecsLite::IsConnected() const {
    if (m_config.mode == SECS_MODE_HSMS && m_pHsmsServer != nullptr) {
        return m_pHsmsServer->IsConnected();
    } else if (m_config.mode == SECS_MODE_SECSI && m_pSecsiServer != nullptr) {
        return m_pSecsiServer->IsConnected();
    }
    return false;
}

bool SecsLite::IsSelected() const {
    if (m_config.mode == SECS_MODE_HSMS && m_pHsmsServer != nullptr) {
        return m_pHsmsServer->IsSelected();
    } else if (m_config.mode == SECS_MODE_SECSI && m_pSecsiServer != nullptr) {
        return m_pSecsiServer->IsConnected();
    }
    return false;
}

int SecsLite::GetLastError() const {
    return m_lastError;
}

const char* SecsLite::GetLastErrorMessage() const {
    return m_lastErrorMessage;
}

void SecsLite::SetError(int errorCode, const char* message) {
    m_lastError = errorCode;
    if (message != nullptr) {
        strncpy_s(m_lastErrorMessage, sizeof(m_lastErrorMessage), message, _TRUNCATE);
    }
}

void SecsLite::OnHsmsConnected(bool connected, const std::string& ip) {
    m_isConnected = connected;
    if (connected) {
        if (m_pHsmsServer != nullptr) {
            // 只有 Active 模式（hsmsMode = 1）才发送 Select Request
            if (m_config.hsmsMode == 1) {
                m_pHsmsServer->SendSelectRequest();
            }
        }
        if (m_config.delegateOnConnect) {
            bool result = m_config.delegateOnConnect(m_hSecsHandle, m_hSecsHandle, m_config.usDeviceID);
            if (!result) {
                Disconnect();
            }
        }
    } else {
        if (m_config.delegateOnClose) {
            m_config.delegateOnClose(m_hSecsHandle);
        }
    }
}

void SecsLite::OnHsmsDisconnected(const std::string& ip) {
    m_isConnected = false;

    if (m_config.delegateOnClose) {
        m_config.delegateOnClose(m_hSecsHandle);
    }
}

void SecsLite::OnHsmsMessageReceived(const HsmsMessage& msg) {
    if (msg.header.sType != HsmsMessageType::DataMessage) {
        return;
    }

    unsigned char stream = msg.header.stream;
    unsigned char func = msg.header.function;

    // 处理回复（用于超时管理）
    HandleReply(msg.header.systemBytes);

    // 直接传递原始数据给回调，不再进行SECS-II解码
    if (m_config.delegateOnRecv) {
        m_config.delegateOnRecv(stream, func, msg.data, msg.dataLength, msg.header.systemBytes);
    }

    if (m_config.delegateOnRecvRawData) {
        LONG_PTR dataPtr = reinterpret_cast<LONG_PTR>(msg.data);

        // 接受C#回调的返回值，如果是错误码则通过ErrorManager发错误消息
        int result = m_config.delegateOnRecvRawData(
            m_hSecsHandle,
            msg.header.systemBytes,
            stream,
            func,
            dataPtr,
            static_cast<unsigned int>(msg.dataLength)
        );
        int errorFunc = NormalizeSecsErrorFunction(result);

        SecsLogger::Debug("C# delegateOnRecvRawData returned result=%d for S%dF%d MsgId = %u", result, stream, func, msg.header.systemBytes);

        if (errorFunc != 0) {
            if (m_pErrorManager) 
            {
                bool sendResult = m_pErrorManager->SendError(errorFunc, msg);
                if (sendResult) 
                {
                    SecsLogger::Warn("Sent S9F%d for C# result=%d. Original=S%dF%d MsgId=%u",
                    errorFunc, result, stream, func, msg.header.systemBytes);
                }
                else 
                {
                    SecsLogger::Error("Failed to send S9F%d for C# result=%d. Original=S%dF%d MsgId=%u",
                    errorFunc, result, stream, func, msg.header.systemBytes);
                }
            } 
            else 
            {
                // 记录：ErrorManager 未初始化，无法发送 S9Fx
                SecsLogger::Error("Failed to send S9F%d for C# result=%d. Original=S%dF%d MsgId=%u",
                errorFunc, result, stream, func, msg.header.systemBytes);
            }
        } 
        else if (result != 0) 
        {
            // 记录：C# 返回了未知错误码 result，不发送 S9Fx
            SecsLogger::Warn("Unknown or ignored C# result=%d. No S9Fx sent. S%dF%d MsgId=%u",
            result, stream, func, msg.header.systemBytes);
        }
    }
}

void SecsLite::OnHsmsRawDataReceived(const unsigned char* data, int length) {
    // 此方法用于原始数据回调，但现在使用完整版本的回调，所以这里可以留空或删除
}

void SecsLite::OnSecsiConnected(bool connected, const std::string& port) {
    m_isConnected = connected;

    if (connected) {
        // 对于 SECS-I 不应在连接时自动发送 SelectRequest（HSMS 专用行为）。
        // 仅通知上层连接建立，由上层决定是否发送 Select。
        if (m_config.delegateOnConnect) {
            bool result = m_config.delegateOnConnect(m_hSecsHandle, m_hSecsHandle, m_config.usDeviceID);
            if (!result) {
                Disconnect();
            }
        }
    } else {
        if (m_config.delegateOnClose) {
            m_config.delegateOnClose(m_hSecsHandle);
        }
    }
}

void SecsLite::OnSecsiDisconnected(const std::string& port) {
    m_isConnected = false;

    if (m_config.delegateOnClose) {
        m_config.delegateOnClose(m_hSecsHandle);
    }
}

void SecsLite::OnSecsiMessageReceived(const SECSIMessage& msg) {
    // 使用新的结构，与HSMS一致
    unsigned char stream = msg.header.stream;
    unsigned char func = msg.header.function;
    char _ts[32]; get_timestamp(_ts, sizeof(_ts));

    // 打印 Stream 和 Function
    printf("%s [SECS-I] Received Message: Stream=0x%02X, Function=0x%02X", _ts, stream, func);

    // 如果 data 不为 null，打印数据内容
    if (msg.data != nullptr && msg.dataLength > 0) {
        printf(", DataLength=%d, Data=", msg.dataLength);
        const int maxPrintBytes = 64;
        int bytesToPrint = (msg.dataLength > maxPrintBytes) ? maxPrintBytes : msg.dataLength;

        for (int i = 0; i < bytesToPrint; i++) {
            printf("%02X ", msg.data[i]);
        }

        if (msg.dataLength > maxPrintBytes) {
            printf("... (truncated, total %d bytes)", msg.dataLength);
        }
    }
    printf("\n");

    // 处理回复（用于超时管理）
    //HandleReply(stream, func);
    // 直接传递原始数据给回调，不再进行SECS-II解码
    // 即使 data 为 null 或 dataLength 为 0，也应该触发回调（有些消息只有头信息）
    if (m_config.delegateOnRecv) {
        m_config.delegateOnRecv(stream, func, msg.data, msg.dataLength, 0);
    }

    if (m_config.delegateOnRecvRawData) {
        LONG_PTR dataPtr = reinterpret_cast<LONG_PTR>(msg.data);
        m_config.delegateOnRecvRawData(m_hSecsHandle, msg.header.systemBytes, stream, func, dataPtr, static_cast<unsigned int>(msg.dataLength));
    }
}

void SecsLite::OnSecsiRawDataReceived(const unsigned char* data, int length) {
    // 此方法用于原始数据回调，但现在使用完整版本的回调，所以这里可以留空或删除
}

unsigned int SecsLite::GetNextMsgId() {
    std::lock_guard<std::mutex> lock(m_msgIdMutex);
    unsigned int id = m_nextMsgId++;
    if (m_nextMsgId == 0) m_nextMsgId = 1;
    return id;
}

bool SecsLite::HasPendingMessages() const {
    std::lock_guard<std::mutex> lock(m_pendingMutex);
    return !m_pendingMessages.empty();
}

extern "C" {

__declspec(dllexport) void* __stdcall CreateSecs(SecsSets* pSets) {
    SecsLite* pSecs = new SecsLite();
    if (pSecs == nullptr) {
        return nullptr;
    }

    if (!pSecs->Initialize(pSets)) {
        delete pSecs;
        return nullptr;
    }

    return pSecs;
}

__declspec(dllexport) void __stdcall DestroySecs(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs != nullptr) {
        delete pSecs;
    }
}

__declspec(dllexport) bool __stdcall SecsStartupServer(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr) {
        return false;
    }
    return pSecs->StartupServer();
}

__declspec(dllexport) void __stdcall SecsCloseServer(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs != nullptr) {
        pSecs->CloseServer();
    }
}

__declspec(dllexport) bool __stdcall SecsConnect(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr) {
        return false;
    }
    return pSecs->Connect();
}

__declspec(dllexport) void __stdcall SecsDisconnect(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs != nullptr) {
        pSecs->Disconnect();
    }
}

__declspec(dllexport) int __stdcall SecsSendMessageBuffer(void* hSecs,
                                                         unsigned char stream,
                                                         unsigned char func,
                                                         const unsigned char* dataBuffer,
                                                         int dataLen,
                                                         unsigned int flag,
                                                         unsigned int msgId) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr) {
        return -1;
    }
    return pSecs->SendSecsMessage(stream, func, dataBuffer, dataLen, flag, msgId);
}

__declspec(dllexport) bool __stdcall IfSecsconnect(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr) {
        return false;
    }
    return pSecs->IsConnected();
}

__declspec(dllexport) bool __stdcall IfSecsSelected(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr) {
        return false;
    }
    return pSecs->IsSelected();
}

__declspec(dllexport) int __stdcall SecsGetLastError(void* hSecs) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr) {
        return -1;
    }
    return pSecs->GetLastError();
}

__declspec(dllexport) int __stdcall SecsGetLastErrorMessage(void* hSecs, char* buffer, int bufferSize) {
    SecsLite* pSecs = (SecsLite*)hSecs;
    if (pSecs == nullptr || buffer == nullptr || bufferSize <= 0) {
        return 0;
    }

    const char* message = pSecs->GetLastErrorMessage();
    strncpy_s(buffer, bufferSize, message, _TRUNCATE);
    return (int)strlen(buffer);
}

} // extern "C"
