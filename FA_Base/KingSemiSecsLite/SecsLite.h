#pragma once
#include "pch.h"
#include <mutex>
#include <memory>

#include "Secs2Message.h"
#include "TimeoutManager.h"
#include "MessageQueue.h"
#include "HsmsServer.h"

/**
 * @brief SECS协议最大数据长度
 * 
 * SECS-II消息的最大数据长度限制为64KB
 */
#define MAX_DATA_LENGTH (64 * 1024)

/**
 * @brief SECS通信模式枚举
 * 
 * 定义SECS协议的通信模式：HSMS（TCP/IP）或SECS I（串口）
 */
typedef enum {
    SECS_MODE_HSMS = 0,   ///< HSMS模式，基于TCP/IP协议
    SECS_MODE_SECSI = 1   ///< SECS I模式，基于串口通信
} SecsMode;

/**
 * @brief 连接状态回调函数类型
 * 
 * 当连接状态发生变化时调用此回调函数
 * 
 * @param connected 当前连接状态：true表示已连接，false表示已断开
 * @param ipOrPort 远程端IP地址（HSMS模式）或串口号（SECS I模式）
 */
typedef void(__stdcall* OnConnectCallback)(bool connected, const char* ipOrPort);

/**
 * @brief 连接关闭回调函数类型
 * 
 * 当连接关闭时调用此回调函数
 * 
 * @param ipOrPort 远程端IP地址（HSMS模式）或串口号（SECS I模式）
 */
typedef void(__stdcall* OnCloseCallback)(const char* ipOrPort);

/**
 * @brief SECS-II消息接收回调函数类型
 * 
 * 当接收到SECS-II消息时调用此回调函数
 * 
 * @param stream SECS-II流编号
 * @param func SECS-II功能码
 * @param data 接收到的数据指针
 * @param length 数据长度
 * @param msgId 消息ID，用于匹配请求和响应
 */
typedef void(__stdcall* OnRecvCallback)(unsigned char stream, unsigned char func, const unsigned char* data, int length, unsigned int msgId);

/**
 * @brief 原始数据接收回调函数类型
 * 
 * 当接收到原始协议数据时调用此回调函数（用于调试或日志）
 * 
 * @param data 接收到的原始数据指针
 * @param length 数据长度
 */
typedef void(__stdcall* OnRecvRawDataCallback)(const unsigned char* data, int length);

/**
 * @brief 超时回调函数类型
 * 
 * 当消息等待响应超时后调用此回调函数
 * 
 * @param msgId 超时消息的ID
 */
typedef void(__stdcall* OnReplyTimeoutCallback)(LONG_PTR hSecs, unsigned int msgId);

/**
 * @brief 连接成功回调函数类型
 * 
 * 当连接成功建立时调用此回调函数
 * 
 * @param hServer 服务器实例句柄
 * @param usDevId 设备ID
 */
typedef bool(__stdcall* OnConnectSuccessCallback)(LONG_PTR hServer, LONG_PTR hSecs, unsigned short usDevId);

/**
 * @brief 连接断开回调函数类型
 * 
 * 当连接断开时调用此回调函数
 * 
 * @param hSecs 会话句柄
 */
typedef void(__stdcall* OnDisconnectCallback)(LONG_PTR hSecs);

/**
 * @brief 原始数据接收回调函数类型（完整版本）
 * 
 * 当接收到原始SECS-II消息数据时调用此回调函数
 * 
 * @param hSecs 会话句柄
 * @param dwId 消息ID
 * @param ucS SECS-II流编号
 * @param ucF SECS-II功能码
 * @param data 数据指针
 * @param dataLen 数据长度
 */
typedef int(__stdcall* OnReceiveRawDataCallback)(LONG_PTR hSecs, unsigned int dwId, unsigned char ucS, unsigned char ucF, LONG_PTR data, unsigned int dataLen);

/**
 * @brief SECS配置结构体
 * 
 * 用于配置SecsLite的运行参数和回调函数
 */
typedef struct {
    //HSMS配置
    unsigned int t3;                  ///< T3超时：设备响应超时（默认30秒，SEMI标准）
    unsigned int t5;                  ///< T5超时：选择超时（默认10秒，SEMI标准）
    unsigned int t6;                  ///< T6超时：Reject超时（默认5秒，SEMI标准）
    unsigned int t7;                  ///< T7超时：分离超时（默认10秒，SEMI标准）
    unsigned int t8;                  ///< T8超时：链路测试超时（默认5秒，SEMI标准）
    unsigned int LinkTestInterval;    ///< 链路测试间隔时间（秒）
    unsigned int sLocalIP;            ///< 本地IP地址（网络字节序，仅HSMS模式）
    unsigned short usLocalPort;       ///< 本地端口号（仅HSMS模式）
    unsigned int sRemoteIP;           ///< 远程IP地址（网络字节序，仅HSMS客户端模式）
    unsigned short usRemotePort;      ///< 远程端口号（仅HSMS客户端模式）
    unsigned short usDeviceID;        ///< 设备ID（用于HSMS Select Response）
    unsigned char mode;               ///< 通信模式：0=HSMS，1=SECS I
    unsigned char hsmsMode;           ///< HSMS子模式：0=Passive，1=Active

    //SECSI 配置
    char szComPort[16];               ///< 串口号（如"COM1"，仅SECS I模式）
    unsigned int baudRate;            ///< 波特率（仅SECS I模式）
    unsigned int ms;                  ///< Master模式（主动发起请求）< Slave模式（被动响应）,  默认作为Slave（设备端），R-bit将由ms字段决定
    unsigned int t1;                  ///< T1超时：发送ENQ到接收EOT的最大时间（毫秒，SECS I标准）
    unsigned int t2;                  ///< T2超时：发送EOT到接收ACK/NAK的最大时间（毫秒，SECS I标准）
    unsigned int t3_secsi;            ///< T3超时：发送消息到接收响应的最大时间（毫秒，SECS I标准）
    unsigned int t4;                  ///< T4超时：接收完整消息的最大时间（毫秒，SECS I标准）
    
    //回调函数
    OnConnectSuccessCallback delegateOnConnect;       ///< 连接成功回调
    OnRecvCallback delegateOnRecv;                    ///< SECS-II消息接收回调（简化版）
    OnReceiveRawDataCallback delegateOnRecvRawData;   ///< 原始数据接收回调（完整版）
    OnDisconnectCallback delegateOnClose;             ///< 连接断开回调
    OnReplyTimeoutCallback delegateOnReplyTimeout;    ///< 响应超时回调
} SecsSets;

class ErrorManager;
/**
 * @brief SecsLite主类
 * 
 * 整合HSMS和SECS I协议功能的核心类，提供统一的接口供上层应用调用。
 * 该类作为DLL的主入口，为C#业务层提供SECS协议通信能力。
 * 
 * **架构设计：**
 * - 支持HSMS（TCP/IP）和SECS I（串口）两种通信模式
 * - 封装HsmsServer和SECSIServer，提供统一的上层接口
 * - 管理超时机制（T3-T8）和消息ID分配
 * - 维护待响应消息队列，处理超时重传
 * 
 * **使用流程：**
 * 1. 调用Initialize()初始化配置
 * 2. 调用StartupServer()启动服务端监听 或 Connect()主动连接
 * 3. 使用SendSecsMessage()发送SECS-II消息
 * 4. 通过回调函数接收消息和状态变更
 * 5. 关闭时调用CloseServer()或Disconnect()
 */
class SecsLite {
public:
    /**
     * @brief 构造函数
     * 
     * 初始化成员变量，创建超时管理器和消息队列。
     */
    SecsLite();
    
    /**
     * @brief 析构函数
     * 
     * 释放资源，关闭连接，清理内存。
     */
    ~SecsLite();
    
    /**
     * @brief 初始化配置
     * 
     * 设置通信模式、超时参数、网络/串口配置和回调函数。
     * 
     * @param pSets 配置结构体指针
     * @return true表示初始化成功，false表示失败
     */
    bool Initialize(const SecsSets* pSets);
    
    /**
     * @brief 启动服务端监听
     * 
     * 根据配置的通信模式，启动HSMS服务端监听或打开串口等待连接。
     * 
     * @return true表示启动成功，false表示失败
     */
    bool StartupServer();
    
    /**
     * @brief 关闭服务
     * 
     * 停止监听，断开连接，释放资源。
     */
    void CloseServer();
    
    /**
     * @brief 作为客户端连接
     * 
     * 根据配置的通信模式，主动连接到HSMS服务器或打开串口。
     * 
     * @return true表示连接成功，false表示失败
     */
    bool Connect();
    
    /**
     * @brief 断开连接
     * 
     * 优雅断开连接，释放相关资源。
     */
    void Disconnect();
    
    /**
     * @brief 发送原始消息
     * 
     * 发送已编码的SECS-II消息数据。
     * 
     * @param stream SECS-II流编号
     * @param func SECS-II功能码
     * @param dataBuffer 已编码的消息数据
     * @param dataLen 数据长度
     * @param flag 标志位（如Wait bit等）
     * @param msgId 消息ID
     * @return 成功发送的字节数，失败返回-1
     */
    int SendSecsMessage(unsigned char stream, unsigned char func, 
                    const unsigned char* dataBuffer, int dataLen, 
                    unsigned int flag, unsigned int msgId);
    
    /**
     * @brief 获取当前连接状态
     * 
     * @return true表示已连接，false表示未连接
     */
	    bool IsConnected() const;

	    bool IsSelected() const;
    
    /**
     * @brief 获取最后错误码
     * 
     * @return 错误码，0表示无错误
     */
    int GetLastError() const;
    
    /**
     * @brief 获取最后错误信息
     * 
     * @return 错误描述字符串
     */
    const char* GetLastErrorMessage() const;
    
    unsigned int GetNextMsgId();

    // 用于LinkTest判断数据传输层是否空闲
    bool HasPendingMessages() const;

private:
    class HsmsServer* m_pHsmsServer;    ///< HSMS服务器实例（HSMS模式）
    class SECSIServer* m_pSecsiServer;  ///< SECS I服务器实例（SECS I模式）
    SecsSets m_config;                  ///< 当前配置
    bool m_isInitialized;               ///< 是否已初始化
    bool m_isConnected;                 ///< 当前连接状态
    int m_lastError;                    ///< 最后错误码
    char m_lastErrorMessage[256];       ///< 最后错误描述
    LONG_PTR m_hSecsHandle;             ///< 会话句柄（用于传递给C#回调）
    
    TimeoutManager* m_pTimeoutManager;  ///< 超时管理器
    unsigned int m_nextMsgId;           ///< 下一个消息ID
    std::mutex m_msgIdMutex;            ///< 消息ID互斥锁

    std::unique_ptr<ErrorManager> m_pErrorManager;
    
    /**
     * @brief 待响应消息结构
     * 
     * 用于跟踪等待响应的消息，配合超时机制使用
     */
    struct PendingMessage {
        unsigned int msgId;      
        //Secs2Message msg;        ///< 原始消息
        unsigned int timeoutType; ///< 超时类型（T3/T5/T6/T7/T8）
        std::chrono::steady_clock::time_point startTime;   // 记录发送时刻
        std::vector<unsigned char> Header;
    };
    
    std::map<unsigned int, PendingMessage> m_pendingMessages; ///< 待响应消息队列
    mutable std::mutex m_pendingMutex;   ///< 待响应消息队列互斥锁
    
    /**
     * @brief 超时处理函数
     * 
     * 当消息等待响应超时时被调用。
     * 
     * @param msgId 超时消息的ID
     */
    void OnTimeout(unsigned int msgId);
    
    /**
     * @brief 处理响应消息
     * 
     * 匹配响应消息与待响应消息，更新状态并移除已完成的消息。
     * 
     * @param stream SECS-II流编号
     * @param func SECS-II功能码
     */
    //void HandleReply(unsigned char stream, unsigned char func);
    void HandleReply(unsigned int systemBytes);
    
    /**
     * @brief HSMS连接状态变更处理
     * 
     * @param connected 是否已连接
     * @param ip 远程端IP地址
     */
    void OnHsmsConnected(bool connected, const std::string& ip);
    
    /**
     * @brief HSMS连接断开处理
     * 
     * @param ip 远程端IP地址
     */
    void OnHsmsDisconnected(const std::string& ip);
    
    /**
     * @brief HSMS消息接收处理
     * 
     * 解析HSMS消息并转换为SECS-II消息，通知上层应用。
     * 
     * @param msg 接收到的HSMS消息
     */
    void OnHsmsMessageReceived(const class HsmsMessage& msg);
    
    /**
     * @brief HSMS原始数据接收处理
     * 
     * @param data 原始数据
     * @param length 数据长度
     */
    void OnHsmsRawDataReceived(const unsigned char* data, int length);
    
    /**
     * @brief SECS I连接状态变更处理
     * 
     * @param connected 是否已连接
     * @param port 串口号
     */
    void OnSecsiConnected(bool connected, const std::string& port);
    
    /**
     * @brief SECS I连接断开处理
     * 
     * @param port 串口号
     */
    void OnSecsiDisconnected(const std::string& port);
    
    /**
     * @brief SECS I消息接收处理
     * 
     * 解析SECS I消息并转换为SECS-II消息，通知上层应用。
     * 
     * @param msg 接收到的SECS I消息
     */
    void OnSecsiMessageReceived(const class SECSIMessage& msg);
    
    /**
     * @brief SECS I原始数据接收处理
     * 
     * @param data 原始数据
     * @param length 数据长度
     */
    void OnSecsiRawDataReceived(const unsigned char* data, int length);
    
    /**
     * @brief 设置错误信息
     * 
     * @param errorCode 错误码
     * @param message 错误描述
     */
    void SetError(int errorCode, const char* message);
};

/**
 * @brief C语言导出接口
 * 
 * 提供C兼容的DLL导出函数，供C#等托管语言调用。
 */
extern "C" {
    /**
     * @brief 创建SecsLite实例
     * 
     * @param pSets 配置结构体指针
     * @return SecsLite实例指针，失败返回NULL
     */
    __declspec(dllexport) void* __stdcall CreateSecs(SecsSets* pSets);
    
    /**
     * @brief 销毁SecsLite实例
     * 
     * @param hSecs SecsLite实例指针
     */
    __declspec(dllexport) void __stdcall DestroySecs(void* hSecs);
    
    /**
     * @brief 启动服务端监听
     * 
     * @param hSecs SecsLite实例指针
     * @return true表示成功，false表示失败
     */
    __declspec(dllexport) bool __stdcall SecsStartupServer(void* hSecs);
    
    /**
     * @brief 关闭服务
     * 
     * @param hSecs SecsLite实例指针
     */
    __declspec(dllexport) void __stdcall SecsCloseServer(void* hSecs);
    
    /**
     * @brief 作为客户端连接
     * 
     * @param hSecs SecsLite实例指针
     * @return true表示成功，false表示失败
     */
    __declspec(dllexport) bool __stdcall SecsConnect(void* hSecs);
    
    /**
     * @brief 断开连接
     * 
     * @param hSecs SecsLite实例指针
     */
    __declspec(dllexport) void __stdcall SecsDisconnect(void* hSecs);
    
    /**
     * @brief 发送消息
     * 
     * @param hSecs SecsLite实例指针
     * @param stream SECS-II流编号
     * @param func SECS-II功能码
     * @param dataBuffer 消息数据
     * @param dataLen 数据长度
     * @param flag 标志位
     * @param msgId 消息ID
     * @return 成功发送的字节数，失败返回-1
     */
    __declspec(dllexport) int __stdcall SecsSendMessageBuffer(void* hSecs, 
                                                             unsigned char stream, 
                                                             unsigned char func, 
                                                             const unsigned char* dataBuffer, 
                                                             int dataLen, 
                                                             unsigned int flag, 
                                                             unsigned int msgId);
    
    /**
     * @brief 检查连接状态
     * 
     * @param hSecs SecsLite实例指针
     * @return true表示已连接，false表示未连接
     */
	    __declspec(dllexport) bool __stdcall IfSecsconnect(void* hSecs);

	    __declspec(dllexport) bool __stdcall IfSecsSelected(void* hSecs);
    
    /**
     * @brief 获取最后错误码
     * 
     * @param hSecs SecsLite实例指针
     * @return 错误码
     */
    __declspec(dllexport) int __stdcall SecsGetLastError(void* hSecs);
    
    /**
     * @brief 获取最后错误信息
     * 
     * @param hSecs SecsLite实例指针
     * @param buffer 输出缓冲区
     * @param bufferSize 缓冲区大小
     * @return 错误信息长度
     */
    __declspec(dllexport) int __stdcall SecsGetLastErrorMessage(void* hSecs, char* buffer, int bufferSize);
}
