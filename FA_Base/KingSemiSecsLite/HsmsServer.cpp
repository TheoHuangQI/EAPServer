#include "pch.h"
#include "HsmsServer.h"
#include "SecsLogger.h"
#include "HsmsUtils.h"

const int MAX_RECEIVE_BUFFER_SIZE = 1024 * 1024;

string BytesToHexString(const unsigned char* data, int length) {
    stringstream ss;
    ss << hex << setfill('0');
    for (int i = 0; i < length; i++) {
        ss << setw(2) << (int)data[i] << " ";
        if ((i + 1) % 16 == 0) {
            ss << "\n";
        }
    }
    return ss.str();
}

HsmsServer::HsmsServer()
    : m_isSelected(false),
      m_onMessageReceived(nullptr), m_onConnectionChanged(nullptr), m_onError(nullptr),
      m_pTimeoutManager(nullptr),
      m_controlMsgIdCounter(0xE0000000),
      m_pendingControlMsgId(0),
      m_pendingControlType(HsmsMessageType::DataMessage),
      m_onControlTimeout(nullptr),
      m_linkTestRunning(true),
      m_linkTestIntervalMs(0),
      m_lastActivityTime(std::chrono::steady_clock::now()),
      m_isDataIdleCallback(nullptr),
      m_t7TimerRunning(false),
      m_lastDisconnectTime()
{
    InitializeCriticalSection(&m_cs);

    m_socketModule.SetDataReceivedCallback([this](const unsigned char* data, int length, const string& remoteIp) {
        this->OnSocketDataReceived(data, length, remoteIp);
    });

    m_socketModule.SetConnectionCallback([this](bool connected, const string& remoteIp) {
        this->OnSocketConnectionChanged(connected, remoteIp);
    });

    m_socketModule.SetErrorCallback([this](int errorCode, const string& errorMessage) {
        this->OnSocketError(errorCode, errorMessage);
    });

    // 启动LinkTest线程
    m_linkTestThread = std::thread(&HsmsServer::LinkTestLoop, this);
}

HsmsServer::~HsmsServer() {
    // 先停止LinkTest线程
    m_linkTestRunning = false;
    if (m_linkTestThread.joinable())
    {
        m_linkTestThread.join();
    }

    StopServer();
    DeleteCriticalSection(&m_cs);
}

bool HsmsServer::StartServer(unsigned short port) {
    return m_socketModule.Listen(port);
}

bool HsmsServer::StopServer() {
    EnterCriticalSection(&m_cs);
    m_isSelected = false;
    LeaveCriticalSection(&m_cs);
    return Disconnect();
}

bool HsmsServer::ConnectToHost(unsigned long ip, unsigned short port) {
    // T5 连接间隔保护
    if (m_pTimeoutManager != nullptr)
    {
        unsigned int t5ms = m_pTimeoutManager->GetTimeoutMs(5);
        if (t5ms > 0)
        {
            auto now = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                now - m_lastDisconnectTime).count();
            if (elapsed < static_cast<long long>(t5ms))
            {
                auto waitMs = t5ms - static_cast<unsigned int>(elapsed);
                SecsLogger::Debug("T5: waiting %ums before reconnect", waitMs);
                std::this_thread::sleep_for(std::chrono::milliseconds(waitMs));
            }
        }
    }

    return m_socketModule.Connect(ip, port);
}

bool HsmsServer::Disconnect() {
    // 清理未完成的T6定时器
    if (m_pTimeoutManager != nullptr && m_pendingControlMsgId != 0)
    {
        m_pTimeoutManager->StopTimer(m_pendingControlMsgId);
        m_pendingControlMsgId = 0;
        m_pendingControlType = HsmsMessageType::DataMessage;
    }

    // 清理T7定时器
    StopT7Timer();

    // 用于T5保护
    m_lastDisconnectTime = std::chrono::steady_clock::now();

    EnterCriticalSection(&m_cs);
    m_isSelected = false;
    LeaveCriticalSection(&m_cs);
    return m_socketModule.Disconnect();
}

int HsmsServer::SendHsmsMessage(const HsmsMessage& msg) {
    if (!m_socketModule.IsConnected()) {
        if (m_onError) m_onError(-1, "Not connected");
        return -1;
    }

    // 先计算需要的编码长度
    int estimatedLength = 14 + (msg.data != nullptr ? msg.dataLength : 0);  // 4字节长度 + 10字节Header + 数据

    // 限制最大缓冲区大小为16MB（HSMS理论最大值）
    if (estimatedLength > 16 * 1024 * 1024) {
        if (m_onError) m_onError(-2, "Message too large (exceeds 16MB limit)");
        return -1;
    }

    // 分配足够大的缓冲区
    unsigned char* buffer = new unsigned char[estimatedLength];

    int encodedLength = EncodeHsmsMessage(msg, buffer, estimatedLength);

    if (encodedLength <= 0) {
        delete[] buffer;
        if (m_onError) m_onError(-2, "Failed to encode HSMS message");
        return -1;
    }

    SecsLogger::RawSend("HSMS", buffer, encodedLength);
    int result = m_socketModule.SendData(buffer, encodedLength);
    delete[] buffer;

    if (result > 0)
    {
        m_lastActivityTime = std::chrono::steady_clock::now();
    }

    return result;
}

void HsmsServer::SetHsmsMessageCallback(OnHsmsMessageReceived callback) {
    EnterCriticalSection(&m_cs);
    m_onMessageReceived = callback;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::SetConnectionCallback(OnHsmsConnectionChanged callback) {
    EnterCriticalSection(&m_cs);
    m_onConnectionChanged = callback;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::SetErrorCallback(OnHsmsError callback) {
    EnterCriticalSection(&m_cs);
    m_onError = callback;
    LeaveCriticalSection(&m_cs);
}

bool HsmsServer::SendSelectRequest() {
    HsmsMessage msg;
    msg.header.sType = HsmsMessageType::SelectRequest;
    msg.header.deviceId = 0;
    msg.header.systemBytes = m_controlMsgIdCounter++;
    msg.data = nullptr;
    msg.dataLength = 0;

    StartT6Timer(HsmsMessageType::SelectRequest);
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendSelectResponse(const HsmsMessage& revMsg) {
    HsmsMessage msg;

    msg.header.deviceId = revMsg.header.deviceId;
    msg.header.systemBytes = revMsg.header.systemBytes;
    msg.header.stream = revMsg.header.stream;
    msg.header.function = revMsg.header.function;
    msg.header.pType = revMsg.header.pType;
    msg.header.sType = HsmsMessageType::SelectResponse;
    msg.data = nullptr;
    msg.dataLength = 0;
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendDeselectRequest() {
    HsmsMessage msg;
    msg.header.sType = HsmsMessageType::DeselectRequest;
    msg.header.deviceId = 0;
    msg.header.systemBytes = m_controlMsgIdCounter++;
    msg.data = nullptr;
    msg.dataLength = 0;

    StartT6Timer(HsmsMessageType::DeselectRequest);
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendDeselectResponse(const HsmsMessage& revMsg) {
    HsmsMessage msg;
    msg.header.deviceId = revMsg.header.deviceId;
    msg.header.systemBytes = revMsg.header.systemBytes;
    msg.header.stream = revMsg.header.stream;
    msg.header.function = revMsg.header.function;
    msg.header.pType = revMsg.header.pType;
    msg.header.sType = HsmsMessageType::DeselectResponse;
    msg.data = nullptr;
    msg.dataLength = 0;
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendLinktestRequest() {
    HsmsMessage msg;
    msg.header.sType = HsmsMessageType::LinktestRequest;
    msg.header.deviceId = 0;
    msg.header.systemBytes = m_controlMsgIdCounter++;
    msg.data = nullptr;
    msg.dataLength = 0;

    StartT6Timer(HsmsMessageType::LinktestRequest);
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendLinktestResponse(const HsmsMessage& revMsg) {
    HsmsMessage msg;
    msg.header.deviceId = revMsg.header.deviceId;
    msg.header.systemBytes = revMsg.header.systemBytes;
    msg.header.stream = revMsg.header.stream;
    msg.header.function = revMsg.header.function;
    msg.header.pType = revMsg.header.pType;
    msg.header.sType = HsmsMessageType::LinktestResponse;
    msg.data = nullptr;
    msg.dataLength = 0;
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendSeparate() {
    HsmsMessage msg;
    msg.header.sType = HsmsMessageType::Separate;
    msg.header.deviceId = 0;
    msg.header.systemBytes = 0;
    msg.data = nullptr;
    msg.dataLength = 0;
    return SendHsmsMessage(msg) > 0;
}

bool HsmsServer::SendReject(const HsmsMessage& rejectedMsg) {
    HsmsMessage msg;
    msg.header.sType = HsmsMessageType::Reject;
    msg.header.deviceId = rejectedMsg.header.deviceId;
    msg.header.systemBytes = rejectedMsg.header.systemBytes;
    msg.header.pType = 0;
    msg.header.wbit = false;

    // Data部分
    std::vector<unsigned char> header = HsmsUtils::PackHeader(rejectedMsg);
    msg.data = new unsigned char[header.size()];
    memcpy_s(msg.data, header.size(), header.data(), header.size());
    msg.dataLength = static_cast<int>(header.size());

    SecsLogger::Warn("HSMS: Sending Reject for SType=%d (msgId=%u)",
        static_cast<int>(rejectedMsg.header.sType), rejectedMsg.header.systemBytes);

    int result = SendHsmsMessage(msg);
    FreeMessageData(msg);
    return result > 0;
}

bool HsmsServer::IsConnected() const {
    return m_socketModule.IsConnected();
}

bool HsmsServer::IsSelected() const {
    EnterCriticalSection(&m_cs);
    bool result = m_isSelected;
    LeaveCriticalSection(&m_cs);
    return result;
}

void HsmsServer::FreeMessageData(HsmsMessage& msg) {
    if (msg.data != nullptr) {
        delete[] msg.data;
        msg.data = nullptr;
        msg.dataLength = 0;
    }
}

void HsmsServer::OnSocketDataReceived(const unsigned char* data, int length, const string& remoteIp) {
    if (length < 10) {  // 至少需要10字节Header（SocketModule已去掉前4字节长度）
        if (m_onError) m_onError(-3, "Message too short");
        return;
    }

    HsmsMessage msg;
    if (DecodeHsmsMessage(data, length, msg)) 
    {
        // 收到有效消息，更新最近活动时间
        m_lastActivityTime = std::chrono::steady_clock::now();

        ProcessReceivedMessage(msg);
        FreeMessageData(msg);
    }
    else 
    {
        if (m_onError) m_onError(-3, "Failed to decode HSMS message");
    }
}

void HsmsServer::OnSocketConnectionChanged(bool connected, const string& remoteIp) {
    EnterCriticalSection(&m_cs);

    if (connected) 
    {
        m_isSelected = false;
        LeaveCriticalSection(&m_cs);
        StartT7Timer();
        EnterCriticalSection(&m_cs);
    }
    else 
    {
        m_isSelected = false;
        m_lastDisconnectTime = std::chrono::steady_clock::now();
        LeaveCriticalSection(&m_cs);
        StopT7Timer();
        EnterCriticalSection(&m_cs);
    }

    OnHsmsConnectionChanged callback = m_onConnectionChanged;
    LeaveCriticalSection(&m_cs);

    if (callback) {
        callback(connected, remoteIp);
    }
}

void HsmsServer::OnSocketError(int errorCode, const string& errorMessage) {
    EnterCriticalSection(&m_cs);
    OnHsmsError callback = m_onError;
    LeaveCriticalSection(&m_cs);

    if (callback) {
        callback(errorCode, errorMessage);
    }
}

int HsmsServer::EncodeHsmsMessage(const HsmsMessage& msg, unsigned char* buffer, int bufferSize) {
    if (buffer == nullptr || bufferSize < 14) {  // 至少需要4字节长度 + 10字节Header
        return -1;
    }

    int pos = 0;

    // 计算总长度 = 10字节Header + SECS-II数据长度
    int totalLength = 10 + (msg.data != nullptr ? msg.dataLength : 0);

    // Device ID (2 bytes) - 字节1-2
    buffer[pos++] = (msg.header.deviceId >> 8) & 0xFF;
    buffer[pos++] = msg.header.deviceId & 0xFF;

    // Reserved (2 bytes) - 字节3-4
    // 对于DataMessage，包含Stream、W-bit和Function信息
    if (msg.header.sType == HsmsMessageType::DataMessage) {
        // 第3字节: W-bit (高1位) + Stream (低7位)
        buffer[pos++] = (msg.header.wbit ? 0x80 : 0x00) | (msg.header.stream & 0x7F);
        // 第4字节: Function
        buffer[pos++] = msg.header.function;
    } else {
        // 非DataMessage（控制消息），Reserved字段（第3、4字节）都为0
        buffer[pos++] = 0x00;
        buffer[pos++] = 0x00;
    }

    // PType (1 byte) - 字节5
    buffer[pos++] = msg.header.pType;

    // SType (1 byte) - 字节6 - 消息类型
    buffer[pos++] = (unsigned char)msg.header.sType;

    // System Bytes (4 bytes) - 字节7-10
    buffer[pos++] = (msg.header.systemBytes >> 24) & 0xFF;
    buffer[pos++] = (msg.header.systemBytes >> 16) & 0xFF;
    buffer[pos++] = (msg.header.systemBytes >> 8) & 0xFF;
    buffer[pos++] = msg.header.systemBytes & 0xFF;

    // Data (N bytes) - SECS-II 编码的数据
    if (msg.data != nullptr && msg.dataLength > 0) {
        if (pos + msg.dataLength > bufferSize) {
            return -1;
        }
        memcpy_s(buffer + pos, bufferSize - pos, msg.data, msg.dataLength);
        pos += msg.dataLength;
    }
    return pos;
}

bool HsmsServer::DecodeHsmsMessage(const unsigned char* data, int length, HsmsMessage& msg) {
    // SocketModule已去掉前4字节长度，所以接收到的数据是 [10字节Header] + [SECS-II数据]
    if (data == nullptr || length < 10) {  // 至少需要10字节Header
        return false;
    }

    int pos = 0;
    // Device ID (2 bytes) - 位置0-1
    unsigned short high = data[pos++];
    unsigned short low = data[pos++];
    msg.header.deviceId = (high << 8) | low;

    // Reserved (2 bytes) - 位置2-3
    // 对于DataMessage，这里包含Stream、W-bit和Function信息
    unsigned char reserved1 = data[pos++];  // 第3字节: W-bit + Stream
    unsigned char reserved2 = data[pos++];  // 第4字节: Function

    // PType (1 byte) - 位置4
    msg.header.pType = data[pos++];

    // SType (1 byte) - 位置5 - 消息类型
    msg.header.sType = (HsmsMessageType)data[pos++];

    // 如果是DataMessage，从Reserved中获取Stream、W-bit和Function
    if (msg.header.sType == HsmsMessageType::DataMessage) {
        msg.header.wbit = (reserved1 & 0x80) != 0;   // W-bit 在第3字节的高1位
        msg.header.stream = reserved1 & 0x7F;        // Stream 在第3字节的低7位
        msg.header.function = reserved2;              // Function 在第4字节
    } else {
        msg.header.stream = 0;
        msg.header.function = 0;
        msg.header.wbit = false;
    }

    // System Bytes (4 bytes) - 位置6-9
    uint8_t byte0 = data[pos++];
    uint8_t byte1 = data[pos++];
    uint8_t byte2 = data[pos++];
    uint8_t byte3 = data[pos++];
    msg.header.systemBytes = (byte0 << 24) | (byte1 << 16) | (byte2 << 8) | byte3;

    // 解析Data - SECS-II编码的数据 (从位置10开始)
    int dataLength = length - 10;  // 减去10字节Header
    if (dataLength > 0) {
        msg.data = new unsigned char[dataLength];
        memcpy_s(msg.data, dataLength, data + pos, dataLength);
        msg.dataLength = dataLength;
    } else {
        msg.data = nullptr;
        msg.dataLength = 0;
    }

    return true;
}

void HsmsServer::ProcessReceivedMessage(const HsmsMessage& msg) {
    // Device ID 校验
    bool isDataMessage = (msg.header.sType == HsmsMessageType::DataMessage);
    // cout << "isDataMessage = " << isDataMessage << endl;
    if (isDataMessage) {
        // 非 Select 请求，或 Select 请求但 Device ID 不是 0xFFFF，则校验
        if (msg.header.deviceId != m_expectedDeviceId) {
            //此处应记录错误日志
            SecsLogger::Warn("HSMS device id mismatch. recvDeviceId=%u expectedDeviceId=%u S%dF%d MsgId=%u",
                msg.header.deviceId,
                m_expectedDeviceId,
                msg.header.stream,
                msg.header.function,
                msg.header.systemBytes);
            // 调用回调
            if (m_onDeviceIdMismatch) {
                SecsLogger::Debug("Calling device ID mismatch callback");
                m_onDeviceIdMismatch(msg);
            }

            return;
        }
    }

    EnterCriticalSection(&m_cs);

    bool needSendResponse = false;
    HsmsMessageType responseType = HsmsMessageType::DataMessage;
    bool accept = true;

    // 保存收到的Request的Device ID和System Bytes，用于Response
    switch (msg.header.sType) {
        case HsmsMessageType::SelectRequest:
            responseType = HsmsMessageType::SelectResponse;
            accept = true;
            needSendResponse = true;
            m_isSelected = true;
            StopT7Timer();
            break;

        case HsmsMessageType::SelectResponse:
            StopT6Timer(msg.header.systemBytes);
            m_isSelected = (msg.header.deviceId == 0 || msg.header.deviceId == 0xFFFF);
            if (m_isSelected) StopT7Timer();
            break;

        case HsmsMessageType::DeselectRequest:
            responseType = HsmsMessageType::DeselectResponse;
            accept = true;
            needSendResponse = true;
            m_isSelected = false;
            StartT7Timer();
            break;

        case HsmsMessageType::DeselectResponse:
            StopT6Timer(msg.header.systemBytes);
            m_isSelected = false;
            StartT7Timer();
            break;

        case HsmsMessageType::LinktestRequest:
            responseType = HsmsMessageType::LinktestResponse;
            accept = true;
            needSendResponse = true;
            break;
        case HsmsMessageType::LinktestResponse:
            StopT6Timer(msg.header.systemBytes);
            break;
        case HsmsMessageType::Separate:
            m_isSelected = false;
            break;

        default:
            // 仅对非DataMessage的未知SType发送Reject
            // DataMessage(0) 正常传给C#处理
            if (msg.header.sType != HsmsMessageType::DataMessage
                && msg.header.sType != HsmsMessageType::Reject)
            {
                SendReject(msg);
            }
            break;
    }

    OnHsmsMessageReceived callback = m_onMessageReceived;
    LeaveCriticalSection(&m_cs);

    // 发送自动响应
    if (needSendResponse) {
        if (responseType == HsmsMessageType::SelectResponse) {
            SendSelectResponse(msg);
        } else if (responseType == HsmsMessageType::DeselectResponse) {
            SendDeselectResponse(msg);
        } else if (responseType == HsmsMessageType::LinktestResponse) {
            SendLinktestResponse(msg);
        }
    }

    // 始终调用消息回调（如果注册了）
    if (callback) {
        callback(msg);
    }
}

void HsmsServer::SetDeviceIdMismatchCallback(DeviceIdMismatchCallback callback) {
    EnterCriticalSection(&m_cs);
    m_onDeviceIdMismatch = callback;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::SetTimeoutManager(TimeoutManager* pTm) {
    EnterCriticalSection(&m_cs);
    m_pTimeoutManager = pTm;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::SetControlTimeoutCallback(std::function<void()> callback) {
    EnterCriticalSection(&m_cs);
    m_onControlTimeout = callback;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::StartT6Timer(HsmsMessageType controlType) {
    if (m_pTimeoutManager == nullptr) return;

    if (m_pendingControlMsgId != 0)
    {
        m_pTimeoutManager->StopTimer(m_pendingControlMsgId);
        m_pendingControlMsgId = 0;
    }

    unsigned int currentMsgId = m_controlMsgIdCounter - 1;
    m_pendingControlMsgId = currentMsgId;
    m_pendingControlType = controlType;

    m_pTimeoutManager->StartTimer(currentMsgId, 6,
        [this](unsigned int msgId) {
            this->OnT6Timeout(msgId);
        });
}

void HsmsServer::StopT6Timer(unsigned int msgId) {
    if (m_pTimeoutManager == nullptr) return;

    if (m_pendingControlMsgId !=0 && m_pendingControlMsgId == msgId) 
    {
        m_pTimeoutManager->StopTimer(m_pendingControlMsgId);
        m_pendingControlMsgId = 0;
        m_pendingControlType = HsmsMessageType::DataMessage;
    }
}

void HsmsServer::OnT6Timeout(unsigned int msgId) {
    if (msgId != m_pendingControlMsgId) return;

    const char* ctrlTypeStr = "Unknown";
    switch (m_pendingControlType)
    {
        case HsmsMessageType::SelectRequest:
            ctrlTypeStr = "Select";
            break;
        case HsmsMessageType::DeselectRequest:
            ctrlTypeStr = "Deselect";
            break;
        case HsmsMessageType::LinktestRequest:
            ctrlTypeStr = "Linktest";
            break;
        default: break;
    }
    unsigned int t6ms = m_pTimeoutManager ? m_pTimeoutManager->GetTimeoutMs(6) : 0;
    SecsLogger::Error("T6 timeout Control message '%s' (msgId=%u) no response within %ums",
        ctrlTypeStr, msgId, m_pTimeoutManager ? t6ms : -1);
    // 日志中的超时值可进一步优化，这里先记录事件
    
    m_pendingControlMsgId = 0;
    m_pendingControlType = HsmsMessageType::DataMessage;

    if (m_onControlTimeout) {
        m_onControlTimeout();
    }
}

void HsmsServer::SetLinkTestInterval(unsigned int intervalMs)
{
     SecsLogger::Debug("HsmsServer::SetLinkTestInterval called with intervalMs=%u", intervalMs);

    EnterCriticalSection(&m_cs);
    m_linkTestIntervalMs = intervalMs;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::SetDataIdleCheckCallback(std::function<bool()> callback)
{
    EnterCriticalSection(&m_cs);
    m_isDataIdleCallback = callback;
    LeaveCriticalSection(&m_cs);
}

void HsmsServer::LinkTestLoop()
{
    while (m_linkTestRunning)
    {
        // 每秒检查一次
        std::this_thread::sleep_for(std::chrono::seconds(1));

        // 检查是否启用
        unsigned int interval = m_linkTestIntervalMs;
        if (interval == 0) continue;

        // 检查是否Selected 是否T6
        bool canSend = false;
        EnterCriticalSection(&m_cs);
        if (m_isSelected && m_pendingControlMsgId == 0)
        {
            auto now = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                now - m_lastActivityTime).count();
            if (elapsed >= static_cast<long long>(m_linkTestIntervalMs))
            {
                canSend = true;
            }
        }
        LeaveCriticalSection(&m_cs);

        if(!canSend) continue;

        // 检查是否T3
        if (m_isDataIdleCallback && !m_isDataIdleCallback())
        {
            continue;
        }

        // 所有条件满足，发送LinkTest请求
        SecsLogger::Debug("LinkTest: Sending LinktestRequest(idle for %ums)"
            ,m_linkTestIntervalMs);
        SendLinktestRequest();
    }
}

void HsmsServer::StartT7Timer()
{
    if (m_pTimeoutManager == nullptr) return;

    EnterCriticalSection(&m_cs);
    if (m_t7TimerRunning)
    {
        LeaveCriticalSection(&m_cs);
        return;
    }
    m_t7TimerRunning = true;
    LeaveCriticalSection(&m_cs);

    m_pTimeoutManager->StartTimer(T7_MSG_ID, 7, [this](unsigned int)
        {
            SecsLogger::Error("T7 timeout: NOT SELECTED state exceeded, disconnecting...");
            if (m_onControlTimeout)
            {
                m_onControlTimeout();
            }
        });
}

void HsmsServer::StopT7Timer()
{
    if (m_pTimeoutManager == nullptr) return;

    EnterCriticalSection(&m_cs);
    if (!m_t7TimerRunning)
    {
        LeaveCriticalSection(&m_cs);
        return;
    }
    m_t7TimerRunning = false;
    LeaveCriticalSection(&m_cs);

    m_pTimeoutManager->StopTimer(T7_MSG_ID);
}
