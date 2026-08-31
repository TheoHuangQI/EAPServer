#pragma once
#include "pch.h"
#include <functional>
#include <string>

/**
 * @brief 串口数据接收回调函数类型定义
 * 
 * 当串口接收到数据时，通过此回调函数通知上层应用
 * 
 * @param data 接收到的数据指针，由底层分配，调用者无需释放
 * @param length 接收到的数据长度（字节数）
 * @param portName 串口名称（如"COM1"）
 */
typedef std::function<void(const unsigned char* data, int length, const std::string& portName)> OnSerialDataReceived;

/**
 * @brief 串口连接状态变更回调函数类型定义
 * 
 * 当串口连接状态发生变化时，通过此回调函数通知上层应用
 * 
 * @param connected 当前连接状态：true表示串口已打开，false表示串口已关闭
 * @param portName 串口名称（如"COM1"）
 */
typedef std::function<void(bool connected, const std::string& portName)> OnSerialConnectionChanged;

/**
 * @brief 串口错误回调函数类型定义
 * 
 * 当串口操作发生错误时，通过此回调函数通知上层应用
 * 
 * @param errorCode 错误码：Windows API错误码或自定义错误码
 * @param errorMessage 错误描述信息，提供详细的错误原因
 */
typedef std::function<void(int errorCode, const std::string& errorMessage)> OnSerialError;




/**
 * @brief 串口配置结构
 * 
 * 用于配置串口通信参数，遵循 SECS I 协议规范
 * 
 * **SECS I 推荐配置：**
 * - 波特率：常用 9600, 19200, 38400, 57600, 115200
 * - 数据位：8位（标准）
 * - 停止位：1位（标准）
 * - 奇偶校验：无（'N'）
 */
struct SerialConfig {
    std::string portName;      ///< 串口号（如 "COM1", "COM2", "/dev/ttyS0"）
    int baudRate;              ///< 波特率（常用：9600, 19200, 38400, 57600, 115200）
    int dataBits;              ///< 数据位（5, 6, 7, 8），SECS I标准为8
    int stopBits;              ///< 停止位（1, 2），SECS I标准为1
    char parity;               ///< 奇偶校验（'N'=无, 'O'=奇, 'E'=偶），SECS I标准为'N'
    int t1;                    ///< T1 超时时间（毫秒），Inter-character timeout
    int t2;                    ///< T2 超时时间（毫秒），Protocol timeout（ENQ->EOT 等）
    
    /**
     * @brief 默认构造函数
     * 
     * 设置默认参数为 SECS I 标准配置：
     * - 串口号：COM1
     * - 波特率：9600
     * - 数据位：8
     * - 停止位：1
     * - 奇偶校验：无
     */
    SerialConfig() : portName("COM1"), baudRate(9600), dataBits(8), stopBits(1), parity('N'), t1(1000), t2(1000) {}
};

/**
 * @brief 串口模块类
 * 
 * 封装基础的串口通信功能，支持数据收发和状态管理。
 * 该类是 SECS I 协议实现的基础传输层，使用 Windows API 进行同步串口操作。
 * 
 * **设计特点：**
 * - 完全同步模式，发送和接收都在调用线程中同步执行
 * - 支持标准串口配置参数
 * - 线程安全设计，使用CRITICAL_SECTION保护共享资源
 * - 支持 Windows 平台的串口操作
 */
class SerialModule
{
public:
    /**
     * @brief 构造函数
     * 
     * 初始化成员变量为默认值，创建线程同步锁。
     */
    SerialModule();
    
    /**
     * @brief 析构函数
     * 
     * 关闭串口连接，释放线程同步锁资源。
     */
    virtual ~SerialModule();
    
    /**
     * @brief 打开串口连接
     * 
     * 根据配置参数打开指定的串口设备，配置通信参数。
     * 
     * @param config 串口配置参数
     * @return true表示打开成功，false表示失败（端口不存在、权限不足等）
     */
    bool Open(const SerialConfig& config);
    
    /**
     * @brief 关闭串口连接
     * 
     * 关闭串口句柄，重置连接状态。
     * 
     * @return true表示操作成功（即使串口未打开也返回true）
     */
    bool Close();
    
    /**
     * @brief 发送数据（同步）
     * 
     * 将数据同步发送到已打开的串口，阻塞直到发送完成。
     * 
     * @param data 待发送数据指针，调用者负责数据的生命周期
     * @param length 数据长度（字节数）
     * @return 成功发送的字节数；失败返回-1（串口未打开、发送失败等）
     */
    int SendData(const unsigned char* data, int length);
    
    /**
     * @brief 接收数据（同步）
     * 
     * 从串口同步接收数据，阻塞直到有数据到达或超时。
     * 
     * @param buffer 接收缓冲区指针，由调用者分配
     * @param bufferSize 接收缓冲区大小（字节数）
     * @param timeoutMs 超时时间（毫秒），0表示无限等待
     * @return 成功接收的字节数；失败返回-1（串口未打开、超时等）
     */
    int ReceiveData(unsigned char* buffer, int bufferSize, int timeoutMs = 0);
    
    /**
     * @brief 同步读取一个字节
     * 
     * 从串口同步读取一个字节，阻塞直到有数据到达或超时。
     * 
     * @param byte 输出参数，存储读取的字节
     * @param timeoutMs 超时时间（毫秒），0表示无限等待
     * @return true表示读取成功，false表示失败或超时
     */
    bool ReadByte(unsigned char& byte, int timeoutMs = 0);
    
    /**
     * @brief 设置数据接收回调函数
     * 
     * 设置当串口接收到数据时调用的回调函数。上层应用通过此回调获取数据。
     * 
     * @param callback 数据接收回调函数
     */
    void SetDataReceivedCallback(OnSerialDataReceived callback);
    
    /**
     * @brief 设置连接状态变更回调函数
     * 
     * 设置当串口连接状态发生变化时调用的回调函数。
     * 
     * @param callback 连接状态变更回调函数
     */
    void SetConnectionCallback(OnSerialConnectionChanged callback);
    
    /**
     * @brief 设置错误回调函数
     * 
     * 设置当串口操作发生错误时调用的回调函数。
     * 
     * @param callback 错误回调函数
     */
    void SetErrorCallback(OnSerialError callback);

    /**
     * @brief 获取当前串口状态
     * 
     * @return true表示串口已打开，false表示串口已关闭
     */
    bool IsOpen() const;
    
    /**
     * @brief 获取当前串口号
     * 
     * @return 串口名称字符串（如"COM1"）
     */
    std::string GetPortName() const;

protected:
    OnSerialDataReceived m_onDataReceived;       ///< 数据接收回调函数
    OnSerialConnectionChanged m_onConnectionChanged; ///< 连接状态变更回调函数
    OnSerialError m_onError;                     ///< 错误回调函数
    
    bool m_isOpen;                               ///< 当前串口状态：true=已打开，false=已关闭
    std::string m_portName;                      ///< 当前串口名称
    
    HANDLE m_hSerial;                            ///< 串口句柄（Windows HANDLE类型）
    
    CRITICAL_SECTION m_cs;                       ///< 线程同步锁，保护共享资源访问
    
    // SECS-I 控制字符定义
    static const unsigned char SECSI_ENQ = 0x05;  // Enquiry - 请求发送
    static const unsigned char SECSI_ACK = 0x06;  // Acknowledge - 确认接收
    static const unsigned char SECSI_NAK = 0x15;  // Negative Acknowledge - 否定确认
    static const unsigned char SECSI_EOT = 0x04;  // End of Transmission - 传输结束
};