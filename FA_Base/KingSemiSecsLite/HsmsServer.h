#pragma once
#include "SocketModule.h"
#include "TimeoutManager.h"
#include <atomic>
#include <functional>
#include <string>

/**
 * @brief HSMS消息类型枚举
 * 
 * 定义 HSMS-SS 协议（SEMI E37）中规定的消息类型。
 * HSMS 协议是半导体制造设备通信的标准协议，运行在 TCP/IP 之上。
 */
enum class HsmsMessageType {
    SelectRequest = 1,   ///< Select请求消息，用于建立HSMS会话连接
    SelectResponse = 2,  ///< Select响应消息，响应对方的Select请求
    DeselectRequest = 3, ///< Deselect请求消息，用于主动关闭HSMS会话
    DeselectResponse = 4,///< Deselect响应消息，响应对方的Deselect请求
    LinktestRequest = 5, ///< Linktest请求消息，用于链路测试和保活
    LinktestResponse = 6,///< Linktest响应消息，响应对方的Linktest请求
    Reject = 7,          ///< Reject消息，用于拒绝不符合协议的消息
    Separate = 9,        ///< Separate消息，用于优雅断开连接
    DataMessage = 0      ///< 数据消息（SECS-II消息），承载实际业务数据
};

/**
 * @brief HSMS Header 结构（10字节）
 * 
 * HSMS Header 格式：
 * - 字节1-2: Device ID
 * - 字节3: Stream（仅用于DataMessage，低7位）
 * - 字节4: Function（仅用于DataMessage，低7位）+ W-bit（高1位）
 * - 字节5: PType（协议类型，通常为0）
 * - 字节6: SType（消息类型）
 * - 字节7-10: System Bytes（4字节）
 */
struct HsmsHeader {
    unsigned short deviceId;   ///< 设备ID（2字节）
    unsigned char stream;      ///< SECS-II Stream（仅用于DataMessage，低7位）
    unsigned char function;    ///< SECS-II Function（仅用于DataMessage，低7位）
    bool wbit;                ///< Wait bit（仅用于DataMessage）
    unsigned char pType;      ///< PType（协议类型，通常为0）
    HsmsMessageType sType;    ///< SType（消息类型）
    unsigned int systemBytes;  ///< 系统字节（4字节）
    
    /**
     * @brief 默认构造函数
     */
    HsmsHeader() : deviceId(0), stream(0), function(0), wbit(false), 
                  pType(0), sType(HsmsMessageType::DataMessage), systemBytes(0) {}
};

/**
 * @brief HSMS消息结构
 * 
 * 用于封装 HSMS 协议消息。
 * HSMS 消息格式：Header(10字节) + Data(N字节)
 * 
 * @note data字段是动态分配的，使用后需要调用FreeMessageData释放
 */
struct HsmsMessage {
    HsmsHeader header;         ///< HSMS Header（10字节）
    unsigned char* data;       ///< 消息数据指针（动态分配，需手动释放）
    int dataLength;           ///< 数据长度（字节数）
    
    /**
     * @brief 默认构造函数
     */
    HsmsMessage() : data(nullptr), dataLength(0) {}
};

/**
 * @brief HSMS消息接收回调函数类型
 * 
 * 当接收到HSMS消息时，通过此回调函数通知上层应用
 * 
 * @param msg 接收到的HSMS消息，data字段由底层分配，调用者需要负责释放
 */
typedef std::function<void(const HsmsMessage& msg)> OnHsmsMessageReceived;

/**
 * @brief HSMS连接状态变更回调函数类型
 * 
 * 当HSMS连接状态发生变化时，通过此回调函数通知上层应用
 * 
 * @param connected 当前连接状态：true表示已连接，false表示已断开
 * @param remoteIp 远程端IP地址字符串
 */
typedef std::function<void(bool connected, const std::string& remoteIp)> OnHsmsConnectionChanged;

/**
 * @brief HSMS错误回调函数类型
 * 
 * 当HSMS协议操作发生错误时，通过此回调函数通知上层应用
 * 
 * @param errorCode 错误码：Socket错误码或HSMS协议错误码
 * @param errorMessage 错误描述信息
 */
typedef std::function<void(int errorCode, const std::string& errorMessage)> OnHsmsError;

/**
 * @brief HSMS服务器类
 * 
 * 实现 HSMS-SS（SEMI E37）协议层功能，封装 SocketModule 提供的基础Socket服务。
 * 该类负责HSMS协议消息的编码、解码、发送和自动响应处理。
 * 
 * **HSMS协议特点：**
 * - 支持服务端模式（监听端口）和客户端模式（主动连接）
 * - 单一连接：同一时刻只允许一个连接（HSMS协议要求）
 * - 自动响应：对Select、Deselect、Linktest请求自动回复
 * - 消息类型：协议消息（控制消息）和数据消息（SECS-II）
 * 
 * **状态机：**
 * - Not Connected → Connected → Selected → Connected → Not Connected
 *                   ↑(Select)   ↓(Deselect)
 */
class HsmsServer
{
public:
    /**
     * @brief 构造函数
     * 
     * 初始化内部状态和回调函数，创建线程同步锁。
     */
    HsmsServer();
    
    /**
     * @brief 析构函数
     * 
     * 停止服务，断开连接，释放资源。
     */
    virtual ~HsmsServer();
    
    /**
     * @brief 启动HSMS服务端监听
     * 
     * 创建监听Socket并等待客户端连接。HSMS服务端只允许一个客户端连接。
     * 
     * @param port 监听端口号（HSMS默认端口为5000）
     * @return true表示启动成功，false表示失败
     */
    bool StartServer(unsigned short port);
    
    /**
     * @brief 停止HSMS服务
     * 
     * 断开所有连接，停止监听，释放资源。
     * 
     * @return true表示操作成功
     */
    bool StopServer();
    
    /**
     * @brief 作为客户端连接到HSMS服务器
     * 
     * 主动连接到指定IP和端口的HSMS服务器。
     * 
     * @param ip 服务器IP地址（网络字节序）
     * @param port 服务器端口号
     * @return true表示连接成功，false表示失败
     */
    bool ConnectToHost(unsigned long ip, unsigned short port);
    
    /**
     * @brief 断开连接
     * 
     * 发送Separate消息（如果已连接），关闭Socket，重置状态。
     * 
     * @return true表示操作成功
     */
    bool Disconnect();
    
    /**
     * @brief 发送HSMS消息
     * 
     * 将HSMS消息编码后发送到远程端。
     * 
     * @param msg 待发送的HSMS消息
     * @return 成功发送的字节数，失败返回-1
     */
    int SendHsmsMessage(const HsmsMessage& msg);
    
    /**
     * @brief 设置HSMS消息接收回调函数
     * 
     * @param callback 回调函数
     */
    void SetHsmsMessageCallback(OnHsmsMessageReceived callback);
    
    /**
     * @brief 设置连接状态变更回调函数
     * 
     * @param callback 回调函数
     */
    void SetConnectionCallback(OnHsmsConnectionChanged callback);
    
    /**
     * @brief 设置错误回调函数
     * 
     * @param callback 回调函数
     */
    void SetErrorCallback(OnHsmsError callback);
    
    /**
     * @brief 发送Select请求消息
     * 
     * 请求建立HSMS会话，将连接状态从Connected变为Selected。
     * 
     * @return true表示发送成功，false表示失败
     */
    bool SendSelectRequest();
    
    /**
     * @brief 发送Select响应消息
     * 
     * 响应对方的Select请求，接受会话建立。
     * 
     * @return true表示发送成功，false表示失败
     */
    bool SendSelectResponse(const HsmsMessage& msg);
    
    /**
     * @brief 发送Deselect请求消息
     * 
     * 请求关闭HSMS会话，将连接状态从Selected变为Connected。
     * 
     * @return true表示发送成功，false表示失败
     */
    bool SendDeselectRequest();
    
    /**
     * @brief 发送Deselect响应消息
     * 
     * 响应对方的Deselect请求。
     * 
     * @param accepted 是否接受关闭会话请求：true=接受，false=拒绝
     * @return true表示发送成功，false表示失败
     */
    bool SendDeselectResponse(const HsmsMessage& msg);
    
    /**
     * @brief 发送Linktest请求消息
     * 
     * 请求链路测试，用于检测连接是否正常。
     * 
     * @return true表示发送成功，false表示失败
     */
    bool SendLinktestRequest();
    
    /**
     * @brief 发送Linktest响应消息
     * 
     * 响应对方的Linktest请求。
     * 
     * @return true表示发送成功，false表示失败
     */
    bool SendLinktestResponse(const HsmsMessage& msg);
    
    /**
     * @brief 发送Separate消息
     * 
     * 通知对方即将断开连接，实现优雅断开。
     * 
     * @return true表示发送成功，false表示失败
     */
    bool SendSeparate();

    bool SendReject(const HsmsMessage& rejectMsg);
    
    /**
     * @brief 获取当前连接状态
     * 
     * @return true表示已建立TCP连接，false表示未连接
     */
    bool IsConnected() const;
    
    /**
     * @brief 获取当前HSMS会话状态
     * 
     * @return true表示已建立HSMS会话（Selected状态），false表示未建立
     */
    bool IsSelected() const;
    
    /**
     * @brief 释放HSMS消息数据
     * 
     * 释放消息中动态分配的数据内存，避免内存泄漏。
     * 
     * @param msg 需要释放数据的消息
     */
    static void FreeMessageData(HsmsMessage& msg);
    
    /**
     * @brief 获取远程端IP地址
     * 
     * @return 远程端IP地址字符串
     */
    std::string GetRemoteIp() const;
    
    /**
     * @brief 获取远程端端口号
     * 
     * @return 远程端端口号
     */
    unsigned short GetRemotePort() const;

    using DeviceIdMismatchCallback = std::function<void(const HsmsMessage&)>;

    void SetDeviceIdMismatchCallback(DeviceIdMismatchCallback callback);

    // 设置超时管理器，用于T6超时
    void SetTimeoutManager(TimeoutManager* pTm);
    
    // 设置控制消息超时回调
    void SetControlTimeoutCallback(std::function<void()> callback);

    // 设置LinkTest保活间隔
    void SetLinkTestInterval(unsigned int intervalMs);

    // 设置检查回调
    void SetDataIdleCheckCallback(std::function<bool()> callback);

    void SetT8Timeout(int ms) {
        m_socketModule.SetT8Timeout(ms);
    }

    void SetExpectedDeviceId(unsigned short id) { 
        EnterCriticalSection(&m_cs);
        m_expectedDeviceId = id;
        LeaveCriticalSection(&m_cs);
    }

protected:
    /**
     * @brief Socket数据接收处理函数
     * 
     * 接收底层Socket数据并进行HSMS协议解码。
     * 
     * @param data 接收到的数据指针
     * @param length 数据长度
     * @param remoteIp 远程端IP地址
     */
    void OnSocketDataReceived(const unsigned char* data, int length, const std::string& remoteIp);
    
    /**
     * @brief Socket连接状态变更处理函数
     * 
     * 处理连接状态变更并更新HSMS会话状态。
     * 
     * @param connected 当前连接状态
     * @param remoteIp 远程端IP地址
     */
    void OnSocketConnectionChanged(bool connected, const std::string& remoteIp);
    
    /**
     * @brief Socket错误处理函数
     * 
     * 将底层Socket错误传递给上层。
     * 
     * @param errorCode 错误码
     * @param errorMessage 错误描述
     */
    void OnSocketError(int errorCode, const std::string& errorMessage);
    
    /**
     * @brief HSMS消息编码
     * 
     * 将HsmsMessage结构转换为HSMS协议字节流。
     * HSMS消息格式：
     * - Length (4 bytes): 消息总长度（大端序）
     * - Header (10 bytes): 消息头
     *   - Session ID (2 bytes): 会话ID
     *   - Stream (1 byte) + Function (1 byte): SECS-II流和功能码
     *   - W-bit (1 byte): Wait bit
     *   - System Bytes (2 bytes): 系统字节
     *   - Type (1 byte): 消息类型
     *   - Device ID (1 byte): 设备ID
     * - Data (N bytes): SECS-II数据
     * 
     * @param msg 待编码的消息
     * @param buffer 输出缓冲区
     * @param bufferSize 缓冲区大小
     * @return 编码后的消息长度，失败返回-1
     */
    int EncodeHsmsMessage(const HsmsMessage& msg, unsigned char* buffer, int bufferSize);
    
    /**
     * @brief HSMS消息解码
     * 
     * 将接收到的字节流解析为HsmsMessage结构。
     * 注意：SocketModule已经预先读取了4字节长度字段，
     * 这里接收的数据直接是 Header + Data，不包含长度字段。
     * 
     * @param data 待解码的数据（Header + Data）
     * @param length 数据长度
     * @param msg 输出的消息结构
     * @return true表示解码成功，false表示失败
     */
    bool DecodeHsmsMessage(const unsigned char* data, int length, HsmsMessage& msg);
    
    /**
     * @brief 处理接收到的HSMS消息
     * 
     * 根据消息类型执行相应的协议逻辑：
     * - SelectRequest: 自动回复SelectResponse
     * - DeselectRequest: 自动回复DeselectResponse
     * - LinktestRequest: 自动回复LinktestResponse
     * - DataMessage: 通知上层应用
     * 
     * @param msg 接收到的消息
     */
    void ProcessReceivedMessage(const HsmsMessage& msg);

protected:
    SocketModule m_socketModule;                ///< Socket模块实例，提供底层TCP通信
    OnHsmsMessageReceived m_onMessageReceived;  ///< HSMS消息接收回调
    OnHsmsConnectionChanged m_onConnectionChanged; ///< 连接状态变更回调
    OnHsmsError m_onError;                      ///< 错误回调
    
    bool m_isSelected;                          ///< HSMS会话状态：true=Selected，false=未Selected
    mutable CRITICAL_SECTION m_cs;              ///< 线程同步锁（mutable允许在const方法中修改）
    
    static const int HSMS_HEADER_SIZE = 10;     ///< HSMS消息头长度（字节）
    static const int HSMS_MAX_MESSAGE_SIZE = 16 * 1024 * 1024; ///< HSMS最大消息长度（16MB）
private:
    DeviceIdMismatchCallback m_onDeviceIdMismatch;
    unsigned short m_expectedDeviceId = 0;

    // ========== T5 连接间隔 ===============
    std::chrono::steady_clock::time_point m_lastDisconnectTime;

    // ========== T6 超时相关 ===============
    TimeoutManager* m_pTimeoutManager;      
    unsigned int m_controlMsgIdCounter;
    unsigned int m_pendingControlMsgId;
    HsmsMessageType m_pendingControlType;
    std::function<void()> m_onControlTimeout;

    // 启动T6定时器
    void StartT6Timer(HsmsMessageType controlType);

    // 停止T6定时器
    void StopT6Timer(unsigned int msgId);

    // T6超时处理
    void OnT6Timeout(unsigned int msgId);

    // ============ T7 超时相关 ================
    static const unsigned int T7_MSG_ID = 0x7FFFFFFF;
    bool m_t7TimerRunning;

    void StartT7Timer();
    void StopT7Timer();

    // =========== LinkTest 保活相关 ============
    std::thread m_linkTestThread;
    std::atomic<bool> m_linkTestRunning;    // 线程运行标志
    unsigned int m_linkTestIntervalMs;
    std::chrono::steady_clock::time_point m_lastActivityTime;
    std::function<bool()> m_isDataIdleCallback;

    // LinkTest 主循环
    void LinkTestLoop();
};