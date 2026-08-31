#pragma once
#include "pch.h"
#include <functional>
#include <string>

/**
 * @brief 数据接收回调函数类型定义
 * 
 * 当Socket接收到数据时，通过此回调函数通知上层应用
 * 
 * @param data 接收到的数据指针，由底层分配，调用者无需释放
 * @param length 接收到的数据长度（字节数）
 * @param remoteIp 远程端的IP地址字符串
 */
typedef std::function<void(const unsigned char* data, int length, const std::string& remoteIp)> OnDataReceived;

/**
 * @brief 连接状态变更回调函数类型定义
 * 
 * 当Socket连接状态发生变化时，通过此回调函数通知上层应用
 * 
 * @param connected 当前连接状态：true表示已建立连接，false表示连接已断开
 * @param remoteIp 远程端的IP地址字符串
 */
typedef std::function<void(bool connected, const std::string& remoteIp)> OnConnectionChanged;

/**
 * @brief Socket错误回调函数类型定义
 * 
 * 当Socket操作发生错误时，通过此回调函数通知上层应用
 * 
 * @param errorCode 错误码：Windows Socket API错误码或自定义错误码
 * @param errorMessage 错误描述信息，提供详细的错误原因
 */
typedef std::function<void(int errorCode, const std::string& errorMessage)> OnSocketError;

/**
 * @brief 接收线程上下文结构
 * 
 * 用于在接收线程中传递必要的模块指针和Socket句柄，
 * 使得静态线程函数能够访问类成员
 */
struct RecvContext {
    class SocketModule* module;  ///< SocketModule实例指针，用于访问类成员
    SOCKET sock;                 ///< 客户端Socket句柄，用于接收数据
    HANDLE startup;              ///< 线程启动完成事件，用于同步线程启动状态
};

/**
 * @brief Socket模块基类
 * 
 * 封装基础的TCP Socket功能，支持服务端监听模式和客户端连接模式。
 * 该类提供数据收发、连接管理和状态回调功能，是HSMS协议实现的基础传输层。
 * 
 * **设计特点：**
 * - 支持服务端模式（Listen）和客户端模式（Connect）
 * - 线程安全设计，使用CRITICAL_SECTION保护共享资源
 * - 异步数据接收，通过回调机制通知上层
 * - 动态缓冲区管理，支持大消息传输（最大16MB，HSMS协议上限）
 */
class SocketModule
{
public:
    /**
     * @brief 构造函数
     * 
     * 初始化Winsock库（如果尚未初始化），创建线程同步锁，
     * 初始化成员变量为默认值。
     */
    SocketModule();
    
    /**
     * @brief 析构函数
     * 
     * 断开所有连接，关闭Socket句柄，释放线程同步锁资源，
     * 清理Winsock库（如果是最后一个使用者）。
     */
    virtual ~SocketModule();
    
    /**
     * @brief 启动服务端监听
     * 
     * 创建监听Socket，绑定到指定端口，并开始接受客户端连接。
     * 启动独立的Accept线程持续监听连接请求。
     * 
     * @param port 监听端口号（范围：1-65535）
     * @return true表示启动成功，false表示失败（端口占用、权限不足等）
     */
    bool Listen(unsigned short port);
    
    /**
     * @brief 作为客户端连接到远程服务器
     * 
     * 创建客户端Socket，连接到指定IP地址和端口的远程服务器。
     * 连接成功后启动接收线程监听数据。
     * 
     * @param ip 远程服务器IP地址（网络字节序，使用inet_addr转换）
     * @param port 远程服务器端口号
     * @return true表示连接成功，false表示失败（网络不可达、连接拒绝等）
     */
    bool Connect(unsigned long ip, unsigned short port);
    
    /**
     * @brief 断开连接
     * 
     * 关闭当前连接的Socket和监听Socket（如果存在），
     * 停止接收线程，重置连接状态。
     * 
     * @return true表示操作成功（即使没有连接也返回true）
     */
    bool Disconnect();
    
    /**
     * @brief 发送数据
     * 
     * 将数据发送到已连接的Socket。此函数是线程安全的，
     * 通过CRITICAL_SECTION保护发送操作。
     * 
     * @param data 待发送数据指针，调用者负责数据的生命周期
     * @param length 数据长度（字节数）
     * @return 成功发送的字节数；失败返回-1（连接断开、发送超时等）
     */
    int SendData(const unsigned char* data, int length);
    
    /**
     * @brief 设置数据接收回调函数
     * 
     * 设置当接收到数据时调用的回调函数。上层应用通过此回调获取数据。
     * 
     * @param callback 数据接收回调函数
     */
    void SetDataReceivedCallback(OnDataReceived callback);
    
    /**
     * @brief 设置连接状态变更回调函数
     * 
     * 设置当连接状态发生变化时调用的回调函数。
     * 
     * @param callback 连接状态变更回调函数
     */
    void SetConnectionCallback(OnConnectionChanged callback);
    
    /**
     * @brief 设置错误回调函数
     * 
     * 设置当发生错误时调用的回调函数。
     * 
     * @param callback 错误回调函数
     */
    void SetErrorCallback(OnSocketError callback);
    
    /**
     * @brief 获取当前连接状态
     * 
     * @return true表示已连接，false表示未连接
     */
    bool IsConnected() const;
    
    /**
     * @brief 获取远程端IP地址
     * 
     * @return 远程端IP地址字符串（如"192.168.1.100"）
     */
    std::string GetRemoteIp() const;
    
    /**
     * @brief 获取远程端端口号
     * 
     * @return 远程端端口号
     */
    unsigned short GetRemotePort() const;

    // 设置T8字符间超时
    void SetT8Timeout(int timeoutMs);

protected:
    /**
     * @brief 接受连接线程函数
     * 
     * 静态线程函数，持续监听监听Socket上的连接请求。
     * 当有新连接到来时，接受连接并启动接收线程。
     * 
     * @param param SocketModule实例指针（作为void*传递）
     * @return 线程退出码（0表示正常退出）
     */
    static unsigned int __stdcall AcceptThread(void* param);
    
    /**
     * @brief 接收数据线程函数
     * 
     * 静态线程函数，持续从客户端Socket接收数据。
     * 支持动态缓冲区扩展，最大可达16MB（HSMS协议上限）。
     * 
     * @param param RecvContext结构指针（包含module和socket信息）
     * @return 线程退出码（0表示正常退出）
     */
    static unsigned int __stdcall RecvThread(void* param);
    
    /**
     * @brief IP地址转换函数
     * 
     * 将网络字节序的IP地址转换为字符串格式。
     * 兼容Windows XP及更早版本（不支持inet_ntop的情况）。
     * 
     * @param addr IP地址结构
     * @return IP地址字符串
     */
    static std::string IpToString(const in_addr& addr);

protected:
    OnDataReceived m_onDataReceived;       ///< 数据接收回调函数
    OnConnectionChanged m_onConnectionChanged; ///< 连接状态变更回调函数
    OnSocketError m_onSocketError;         ///< 错误回调函数
    
    bool m_isConnected;                    ///< 当前连接状态：true=已连接，false=未连接
    std::string m_remoteIp;                ///< 远程端IP地址字符串
    unsigned short m_remotePort;           ///< 远程端端口号
    
    SOCKET m_socket;                       ///< 当前连接Socket句柄（客户端连接或已接受的连接）
    SOCKET m_listenSocket;                 ///< 监听Socket句柄（服务端模式下使用）
    
    mutable CRITICAL_SECTION m_cs;         ///< 线程同步锁，保护共享资源访问（mutable允许在const方法中修改）

private:
    int m_t8TimeoutMs;
};