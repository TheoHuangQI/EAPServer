#pragma once
#include "SerialModule.h"
#include "MessageQueue.h"
#include <functional>
#include <windows.h>
#include <queue>

/**
 * @brief SECS I 消息类型枚举
 * 
 * 定义 SECS I 协议（SEMI E4）中规定的消息类型。
 * SECS I 是基于串口的半导体设备通信协议。
 */
enum class SECSIMessageType {
    DataMessage = 0x00,      ///< 数据消息（SECS-II消息），承载实际业务数据
    SelectRequest = 0x01,    ///< Select请求消息，用于建立SECS I会话
    SelectResponse = 0x02,   ///< Select响应消息，响应对方的Select请求
    DeselectRequest = 0x03,  ///< Deselect请求消息，用于关闭SECS I会话
    DeselectResponse = 0x04  ///< Deselect响应消息，响应对方的Deselect请求
};

/**
 * @brief SECS I Header 结构（10字节）
 * 
 * SECS I Header 格式（与HSMS Header一致）：
 * - 字节0-1: Device ID（2字节）
 * - 字节2: Message ID（W-bit + 消息类型）
 * - 字节3: 保留
 * - 字节4-5: Block Number（End-bit + 块号）
 * - 字节6-9: System Bytes（4字节）
 * 
 * 对于DataMessage：
 * - stream 在 System Bytes 中编码
 * - function 在 System Bytes 中编码
 */
// Master/Slave 枚举
enum class SECSIMode {
    MASTER = 0,               ///< Master模式（主动发起请求）
    SLAVE = 1                 ///< Slave模式（被动响应）
};

struct SECSIHeader {
    unsigned short deviceId;   ///< 设备ID（2字节）
    unsigned char stream;      ///< SECS-II Stream
    unsigned char function;    ///< SECS-II Function
    bool wbit;                ///< Wait bit
    unsigned int systemBytes;  ///< 系统字节（4字节）
    unsigned short blockNumber;///< Block number (16-bit)
    bool endBit;               ///< End-bit for multi-block
    bool rbit;                 ///< R-bit in deviceId (direction)
    SECSIMode ms;             ///< Master/Slave模式
    
    /**
     * @brief 默认构造函数
     */
    SECSIHeader() : deviceId(0), stream(0), function(0), wbit(false), 
                   systemBytes(0), blockNumber(1), endBit(true), rbit(false), ms(SECSIMode::SLAVE) {}
};

/**
 * @brief SECS I 消息结构
 * 
 * 用于封装 SECS I 协议消息，结构与HsmsMessage一致。
 * SECS I 消息格式：Header(10字节) + Data(N字节)
 * 
 * @note data字段是动态分配的，使用后需要调用FreeMessageData释放
 */
struct SECSIMessage {
    SECSIHeader header;       ///< SECS I Header（10字节，与HSMS格式一致）
    unsigned char* data;      ///< 消息数据指针（动态分配，需手动释放）
    int dataLength;           ///< 数据长度（字节数）
    
    /**
     * @brief 默认构造函数
     */
    SECSIMessage() : data(nullptr), dataLength(0) {}
};

/**
 * @brief SECS I 消息接收回调函数类型
 * 
 * 当接收到SECS I消息时，通过此回调函数通知上层应用
 * 
 * @param msg 接收到的SECS I消息，data字段由底层分配，调用者需要负责释放
 */
typedef std::function<void(const SECSIMessage& msg)> OnSECSIMessageReceived;

/**
 * @brief SECS I 连接状态变更回调函数类型
 * 
 * 当SECS I连接状态发生变化时，通过此回调函数通知上层应用
 * 
 * @param connected 当前连接状态：true表示串口已打开，false表示串口已关闭
 * @param portName 串口名称（如"COM1"）
 */
typedef std::function<void(bool connected, const std::string& portName)> OnSECSIConnectionChanged;

/**
 * @brief SECS I 错误码枚举
 * 
 * 定义 SECS I 协议相关的错误码，便于问题定位和调试。
 * 负数表示错误，0表示成功。
 */
enum class SECSIError {
    SUCCESS = 0,              ///< 操作成功
    
    // 通用错误 (-1 ~ -9)
    NOT_CONNECTED = -1,       ///< 未连接
    INVALID_PARAMETER = -2,   ///< 参数无效
    MEMORY_ERROR = -3,        ///< 内存分配失败
    
    // 编码/解码错误 (-10 ~ -19)
    ENCODE_FAILED = -10,      ///< 消息编码失败
    DECODE_FAILED = -11,      ///< 消息解码失败
    FRAME_TOO_LARGE = -12,    ///< 帧大小超过限制
    
    // 校验错误 (-20 ~ -29)
    CHECKSUM_MISMATCH = -20,  ///< 校验和不匹配
    CRC_ERROR = -21,          ///< CRC 校验错误
    
    // 超时错误 (-30 ~ -39)
    ENQ_TIMEOUT = -30,        ///< 等待 ENQ 超时
    EOT_TIMEOUT = -31,        ///< 等待 EOT 超时
    ACK_TIMEOUT = -32,        ///< 等待 ACK/NAK 超时
    DATA_TIMEOUT = -33,       ///< 数据接收超时
    T1_TIMEOUT = -34,         ///< T1 超时（字符间超时）
    T2_TIMEOUT = -35,         ///< T2 超时（ENQ→EOT）
    T3_TIMEOUT = -36,         ///< T3 超时（消息响应）
    T4_TIMEOUT = -37,         ///< T4 超时（块间超时）
    
    // 协议错误 (-40 ~ -49)
    PROTOCOL_ERROR = -40,     ///< 协议错误
    INVALID_FRAME_LENGTH = -41, ///< 无效的帧长度
    BLOCK_SEQUENCE_ERROR = -42, ///< 块号不连续
    STATE_ERROR = -43,        ///< 协议状态错误
    DUPLEX_CONFLICT = -44,    ///< 半双工冲突（双方同时发送）
    
    // 硬件/系统错误 (-100 ~ -109)
    SERIAL_ERROR = -100,      ///< 串口错误
    THREAD_ERROR = -101       ///< 线程创建失败
};

/**
 * @brief SECS I 错误回调函数类型
 * 
 * 当SECS I协议操作发生错误时，通过此回调函数通知上层应用
 * 
 * @param errorCode 错误码：串口错误码或SECS I协议错误码
 * @param errorMessage 错误描述信息
 */
typedef std::function<void(int errorCode, const std::string& errorMessage)> OnSECSIError;

/**
 * @brief SECS I 服务器类（同步模式）
 * 
 * 实现 SECS I（SEMI E4）协议层功能，封装 SerialModule 提供的基础串口服务。
 * 该类负责SECS I协议消息的编码、解码、发送和自动响应处理。
 * 
 * **设计特点：**
 * - 完全同步模式，所有收发操作都在调用线程中同步执行
 * - 实现完整的 SECS-I 握手协议（ENQ/EOT/ACK/NAK）
 * - 支持多块消息传输和校验和验证
 * 
 * **SECS I帧格式：**
 * - Length Byte (1 byte): 随后数据长度
 * - Header (10 bytes): 消息头
 * - Data (N bytes): SECS-II数据
 * - Checksum (2 bytes): 16位校验和
 */
class SECSIServer
{
public:
    /**
     * @brief 构造函数
     * 
     * 初始化内部状态和回调函数，创建线程同步锁。
     */
    SECSIServer();
    
    /**
     * @brief 析构函数
     * 
     * 停止服务，关闭串口，释放资源。
     */
    virtual ~SECSIServer();
    
    /**
     * @brief 打开串口并启动SECS I服务
     * 
     * @param config 串口配置参数（串口号、波特率等）
     * @return true表示启动成功，false表示失败
     */
    bool StartServer(const SerialConfig& config);
    
    /**
     * @brief 停止SECS I服务
     * 
     * 关闭串口，重置状态。
     * 
     * @return true表示操作成功
     */
    bool StopServer();
    
    /**
     * @brief 发送SECS I消息（同步）
     * 
     * 将SECS I消息编码后发送，执行完整的ENQ/EOT/ACK握手协议。
     * 
     * @param msg 待发送的SECS I消息
     * @return 成功发送的字节数，失败返回-1
     */
    int SendSECSIMessage(const SECSIMessage& msg);
    
    /**
     * @brief 接收SECS I消息（同步）
     * 
     * 等待并接收完整的SECS I消息，执行完整的握手协议。
     * 
     * @param msg 输出参数，接收到的消息
     * @param timeoutMs 超时时间（毫秒），0表示无限等待
     * @return true表示成功接收，false表示失败或超时
     */
    bool ReceiveSECSIMessage(SECSIMessage& msg, int timeoutMs = 0);
    
    /**
     * @brief 设置SECS I消息接收回调函数
     * 
     * @param callback 回调函数
     */
    void SetSECSIMessageCallback(OnSECSIMessageReceived callback);
    
    /**
     * @brief 设置连接状态变更回调函数
     * 
     * @param callback 回调函数
     */
    void SetConnectionCallback(OnSECSIConnectionChanged callback);
    
    /**
     * @brief 设置错误回调函数
     * 
     * @param callback 回调函数
     */
    void SetErrorCallback(OnSECSIError callback);
    
    /**
     * @brief 获取当前连接状态
     * 
     * @return true表示串口已打开，false表示串口已关闭
     */
    bool IsConnected() const;
    
    /**
     * @brief 获取当前SECS I会话状态
     * 
     * @return true表示已建立会话（Selected状态），false表示未建立
     */
    bool IsSelected() const;
    
    /**
     * @brief 获取当前串口号
     * 
     * @return 串口名称字符串（如"COM1"）
     */
    std::string GetPortName() const;
    
    /**
     * @brief 释放SECS I消息数据
     * 
     * 释放消息中动态分配的数据内存，避免内存泄漏。
     * 
     * @param msg 需要释放数据的消息
     */
    static void FreeMessageData(SECSIMessage& msg);
    
    /**
     * @brief 设置T1超时时间
     * 
     * T1是发送ENQ到接收EOT的最大时间（毫秒）
     * 
     * @param timeout 超时时间（毫秒）
     */
    void SetT1Timeout(int timeout);
    
    /**
     * @brief 设置T2超时时间
     * 
     * T2是发送消息到接收ACK/NAK的最大时间（毫秒）
     * 
     * @param timeout 超时时间（毫秒）
     */
    void SetT2Timeout(int timeout);
    
    /**
     * @brief 设置T3超时时间
     * 
     * T3是发送消息到接收响应的最大时间（毫秒）
     * 
     * @param timeout 超时时间（毫秒）
     */
    void SetT3Timeout(int timeout);
    
    /**
     * @brief 设置T4超时时间
     * 
     * T4是接收完整消息的最大时间（毫秒）
     * 
     * @param timeout 超时时间（毫秒）
     */
    void SetT4Timeout(int timeout);
    
    /**
     * @brief 获取T1超时时间
     * 
     * @return T1超时时间（毫秒）
     */
    int GetT1Timeout() const;
    
    /**
     * @brief 获取T2超时时间
     * 
     * @return T2超时时间（毫秒）
     */
    int GetT2Timeout() const;
    
    /**
     * @brief 获取T3超时时间
     * 
     * @return T3超时时间（毫秒）
     */
    int GetT3Timeout() const;
    
    /**
     * @brief 获取T4超时时间
     * 
     * @return T4超时时间（毫秒）
     */
    int GetT4Timeout() const;
    
    /**
     * @brief 设置Master/Slave模式
     * 
     * @param isMaster true表示Master模式，false表示Slave模式
     */
    void SetMasterMode(bool isMaster);

protected:
    bool m_isMaster;                             ///< 是否为Master模式
    
    // SECS-I 超时配置
    int m_t1Timeout;                            ///< T1 超时时间 (ms) - 字符间超时
    int m_t2Timeout;                            ///< T2 超时时间 (ms) - ENQ→EOT
    int m_t3Timeout;                            ///< T3 超时时间 (ms) - 消息响应
    int m_t4Timeout;                            ///< T4 超时时间 (ms) - 块间超时
    
    // 重传配置
    int m_maxRetries;                           ///< 最大重试次数
    int m_baseBackoffMs;                        ///< 基础退避时间 (ms)
    int m_maxBackoffMs;                         ///< 最大退避时间 (ms)
    
    // 协议状态机（SECS-I 规范六个阶段）
    enum class ProtocolState {
        IDLE,               ///< Idle State: 当前无任何消息接收或发送
        LINE_CONTROL,       ///< Line Control: 建立传输方向，解决冲突，处理重试
        SEND,               ///< Send State: 发送长度字节、数据字节、校验和
        RECEIVE,            ///< Receive State: 接收长度字节、数据+校验和
        RECEIVE_COMPLETION, ///< Receive Completion: 验证校验和，发送ACK/NAK
        SEND_COMPLETION     ///< Send Completion: 等待ACK，处理重试
    };
    ProtocolState m_protocolState;              ///< 当前协议状态
    
    /**
     * @brief 计算指数退避时间
     * @param attempt 当前重试次数（从0开始）
     * @return 退避时间（毫秒）
     */
    int CalculateBackoff(int attempt) const;
    
    // 消息接收状态跟踪
    DWORD m_lastDataReceivedTime;               ///< 最后接收数据的时间戳
    
    /**
     * @brief 发送单个数据块（完整的SECS-I协议流程）
     * @param msg 待发送的消息块
     * @return 成功发送的字节数，失败返回-1
     */
    int SendSingleBlock(const SECSIMessage& msg);
    
    /**
     * @brief Receive State: 接收消息（完整流程）
     * @param msg 输出参数，接收到的消息
     * @return true表示成功，false表示失败
     */
    bool ReceiveMessage(SECSIMessage& msg);
    
    /**
     * @brief 发送NAK
     */
    void SendNAK();
    
    /**
     * @brief 组装完整消息
     * 
     * @param sourceId 源ID
     * @return 组装完成的消息
     */
    SECSIMessage AssembleCompleteMessage(int sourceId);
    
    /**
     * @brief 检查消息大小并分块
     * 
     * @param msg 待发送的消息
     * @return 分块后的消息列表
     */
    std::vector<SECSIMessage> SplitMessageIntoBlocks(const SECSIMessage& msg);
    
    /**
     * @brief SECS I消息编码
     * 
     * @param msg 待编码的消息
     * @param buffer 输出缓冲区
     * @param bufferSize 缓冲区大小
     * @return 编码后的消息长度，失败返回-1
     */
    int EncodeSECSIMessage(const SECSIMessage& msg, unsigned char* buffer, int bufferSize);
    
    /**
     * @brief SECS I消息解码
     * 
     * @param data 待解码的数据
     * @param length 数据长度
     * @param msg 输出的消息结构
     * @return true表示解码成功，false表示失败
     */
    bool DecodeSECSIMessage(const unsigned char* data, int length, SECSIMessage& msg);
    
    /**
     * @brief 处理接收到的SECS I消息
     * 
     * @param msg 接收到的消息
     */
    void ProcessReceivedMessage(const SECSIMessage& msg);
    
    /**
     * @brief 计算校验和
     * 
     * @param data 数据指针
     * @param length 数据长度
     * @return 校验和
     */
    unsigned short CalculateChecksum(const unsigned char* data, int length);
    
    /**
     * @brief 计算CRC校验和
     * 
     * @param data 数据指针
     * @param length 数据长度
     * @return CRC校验和（16位）
     */
    unsigned short CalculateCRC(const unsigned char* data, int length);
    
    /**
     * @brief 检查T4超时
     * 
     * @return true表示未超时，false表示已超时
     */
    bool CheckT4Timeout() const;

protected:
    SerialModule m_serialModule;                ///< 串口模块实例
    OnSECSIMessageReceived m_onMessageReceived; ///< SECS I消息接收回调
    OnSECSIConnectionChanged m_onConnectionChanged; ///< 连接状态变更回调
    OnSECSIError m_onError;                     ///< 错误回调
    
    bool m_isSelected;                          ///< SECS I会话状态
    mutable CRITICAL_SECTION m_cs;              ///< 线程同步锁
    CRITICAL_SECTION m_sendCs;                  ///< 发送互斥锁
    
    unsigned char m_frameBuffer[64 * 1024];     ///< 帧接收缓冲区
    int m_frameBufferPos;                       ///< 当前缓冲区位置
    bool m_useCRC;                               ///< 使用 CRC-16 校验（默认 true）
    
    // 接收线程相关
    HANDLE m_receiveThread;                     ///< 接收线程句柄
    unsigned int m_receiveThreadId;             ///< 接收线程ID
    bool m_receiveThreadRunning;                ///< 接收线程运行标志
    
    // 发送队列和发送线程相关
    MessageQueue<SECSIMessage> m_sendQueue;     ///< 发送消息队列
    HANDLE m_sendThread;                        ///< 发送线程句柄
    unsigned int m_sendThreadId;                ///< 发送线程ID
    bool m_sendThreadRunning;                   ///< 发送线程运行标志
    CRITICAL_SECTION m_queueCs;                 ///< 发送队列锁
    
    /**
     * @brief 接收线程入口函数
     * @param param SECSIServer实例指针
     * @return 线程退出码
     */
    static unsigned int __stdcall ReceiveThreadFunc(void* param);
    
    /**
     * @brief 发送线程入口函数
     * @param param SECSIServer实例指针
     * @return 线程退出码
     */
    static unsigned int __stdcall SendThreadFunc(void* param);
    
    /**
     * @brief 发送消息（内部，执行完整协议流程）
     * @param msg 待发送的消息
     * @return 成功发送的字节数，失败返回-1
     */
    int DoSendSECSIMessage(const SECSIMessage& msg);

public:
    /**
     * @brief 设置是否使用 CRC 校验（默认 true）
     */
    void SetUseCRC(bool useCRC) { m_useCRC = useCRC; }
};