#include "pch.h"
#include "SocketModule.h"
#include "SecsLogger.h"

// 根据平台选择链接的Socket库
#ifdef WINCE
#pragma comment(lib,"ws2.lib")      // Windows CE平台
#else
#pragma comment(lib,"ws2_32.lib")   // Windows XP/7/10/11等通用平台
#endif

// HSMS协议最大消息长度（3字节长度字段，0xFFFFFF = 16,777,215字节 ≈ 16MB）
#define MAX_RECV_BUFFER (16 * 1024 * 1024)  
// 默认初始缓冲区大小（64KB，足够处理大多数消息）
#define DEFAULT_RECV_BUFFER (64 * 1024)

// IP地址转换辅助函数（兼容各版本Windows SDK）
// 手动转换IP地址为字符串，避免使用已弃用的函数
string SocketModule::IpToString(const in_addr& addr) {
    unsigned long ip = ntohl(addr.s_addr);
    char buffer[20];
    sprintf_s(buffer, sizeof(buffer), "%u.%u.%u.%u",
        (ip >> 24) & 0xFF,
        (ip >> 16) & 0xFF,
        (ip >> 8) & 0xFF,
        ip & 0xFF);
    return string(buffer);
}

/**
 * @brief 构造函数
 * 初始化Winsock库（仅首次调用时），初始化成员变量和线程同步锁
 */
SocketModule::SocketModule() 
    : m_isConnected(false), m_remotePort(0), m_socket(INVALID_SOCKET),
      m_listenSocket(INVALID_SOCKET),
      m_onDataReceived(nullptr), m_onConnectionChanged(nullptr), m_onSocketError(nullptr),
      m_t8TimeoutMs(0)
{
    // 静态变量确保Winsock只初始化一次
    static bool initialized = false;
    if (!initialized) {
        WSADATA wsaData;
        WSAStartup(MAKEWORD(2, 2), &wsaData);  // 初始化Winsock 2.2版本
        initialized = true;
    }
    InitializeCriticalSection(&m_cs);  // 初始化临界区锁
}

/**
 * @brief 析构函数
 * 断开所有连接，释放临界区资源
 */
SocketModule::~SocketModule() {
    Disconnect();                    // 确保断开连接
    DeleteCriticalSection(&m_cs);    // 释放临界区
}

/**
 * @brief 启动服务端监听
 * 创建监听Socket，绑定端口，启动接受连接线程
 * @param port 监听端口号
 * @return true表示启动成功，false表示失败
 */
bool SocketModule::Listen(unsigned short port) {
    // 设置Socket地址结构
    sockaddr_in addr;
    addr.sin_family = AF_INET;                              // IPv4协议
    addr.sin_addr.S_un.S_addr = htonl(INADDR_ANY);          // 监听所有网络接口
    addr.sin_port = htons(port);                            // 设置监听端口

    // 创建Socket
    SOCKET listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listenSocket == INVALID_SOCKET) {
        if (m_onSocketError) m_onSocketError(WSAGetLastError(), "Failed to create socket");
        return false;
    }

    // 设置SO_REUSEADDR选项，允许快速重启服务器时重用端口
    int optVal = 1;
    setsockopt(listenSocket, SOL_SOCKET, SO_REUSEADDR, (const char*)&optVal, sizeof(optVal));

    // 绑定Socket到指定端口（使用全局命名空间避免与std::bind冲突）
    if (::bind(listenSocket, (LPSOCKADDR)&addr, sizeof(addr)) == SOCKET_ERROR) {
        if (m_onSocketError) m_onSocketError(WSAGetLastError(), "Failed to bind socket");
        closesocket(listenSocket);
        return false;
    }

    // 开始监听（最大5个等待连接）
    if (listen(listenSocket, 5) == SOCKET_ERROR) {
        if (m_onSocketError) m_onSocketError(WSAGetLastError(), "Failed to listen");
        closesocket(listenSocket);
        return false;
    }

    // 创建接受连接线程
    CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)AcceptThread, this, 0, NULL);
    m_listenSocket = listenSocket;  // 保存监听Socket句柄
    return true;
}

/**
 * @brief 接受连接线程函数
 * 持续监听并接受客户端连接，每接受一个连接启动一个接收线程
 * @param param SocketModule实例指针
 * @return 线程退出码
 */
unsigned int __stdcall SocketModule::AcceptThread(void* param) {
    SocketModule* module = (SocketModule*)param;
    sockaddr_in clientAddr;
    int addrLen = sizeof(clientAddr);

    // 持续接受连接
    while (true) {
        // 接受客户端连接
        SOCKET clientSock = accept(module->m_listenSocket, (LPSOCKADDR)&clientAddr, &addrLen);
        if (clientSock == INVALID_SOCKET) {
            break;  // 监听Socket关闭时退出
        }
        // 保护共享资源
        EnterCriticalSection(&module->m_cs);
        // 如果已有连接，拒绝新连接（HSMS协议只允许一个连接）
        if (module->m_isConnected && module->m_socket != INVALID_SOCKET) {
            closesocket(clientSock);  // 直接关闭新连接，不接受
            LeaveCriticalSection(&module->m_cs);
            continue;  // 继续等待下一个连接请求
        }

        // 将IP地址转换为字符串
        string ipStr = SocketModule::IpToString(clientAddr.sin_addr);

        // 更新连接状态
        module->m_socket = clientSock;
        module->m_remoteIp = ipStr;
        module->m_remotePort = ntohs(clientAddr.sin_port);
        module->m_isConnected = true;

        // 复制回调函数指针（避免持锁调用回调）
        OnConnectionChanged callback = module->m_onConnectionChanged;
        string remoteIp = module->m_remoteIp;
        LeaveCriticalSection(&module->m_cs);
        // 通知上层连接已建立
        if (callback) {
            callback(true, remoteIp);
        }

        // 创建接收线程上下文
        RecvContext* rc = new RecvContext();
        rc->module = module;
        rc->sock = clientSock;
        rc->startup = CreateEvent(NULL, FALSE, FALSE, NULL);
        // 启动接收线程
        CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)RecvThread, rc, 0, NULL);
        WaitForSingleObject(rc->startup, INFINITE);  // 等待线程启动完成
        CloseHandle(rc->startup);
    }
    return 0;
}

/**
 * @brief 接收数据线程函数
 * 持续从客户端接收数据，解码后通过回调通知上层
 * @param param RecvContext结构指针
 * @return 线程退出码
 */
unsigned int __stdcall SocketModule::RecvThread(void* param) {
    RecvContext* rc = (RecvContext*)param;
    SetEvent(rc->startup);  // 通知主线程本线程已启动

    // 设置T8字节间接收超时
    if (rc->module->m_t8TimeoutMs > 0)
    {
        int timeout = rc->module->m_t8TimeoutMs;
        setsockopt(rc->sock, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout));
    }

    // 使用动态分配的缓冲区，初始大小为64KB（足够处理大多数消息）
    int bufferSize = DEFAULT_RECV_BUFFER;
    unsigned char* buf = new unsigned char[bufferSize];

    // 持续接收数据
    while (true) {
        // 循环接收4字节长度，确保完整
        int bytesReceived = 0;
        while (bytesReceived < 4) {
            int ret = recv(rc->sock, (char*)(buf + bytesReceived), 4 - bytesReceived, 0);
            if (ret == SOCKET_ERROR)
            {
                if (WSAGetLastError() == WSAETIMEDOUT)
                {
                    continue;   // 空闲超时，正常，重试
                }
                goto cleanup;   // 其他错误
            }
            if (ret == 0) 
            {
                goto cleanup;  // 对端关闭连接
            }
            bytesReceived += ret;
        }
        
        // 转换为主机字节序
        int dataLen = ntohl(*(int*)buf);
        // 校验数据长度（不能超过HSMS协议最大限制16MB）
        if (dataLen <= 0 || dataLen > MAX_RECV_BUFFER) {
            // 通知错误：消息过大或无效
            if (rc->module->m_onSocketError) {
                rc->module->m_onSocketError(-1, dataLen > MAX_RECV_BUFFER ? "Message exceeds HSMS maximum size (16MB)" : "Invalid message length");
            }
            goto cleanup;
        }

        // 如果当前缓冲区不够大，动态扩展（精确扩展到所需大小）
        if (dataLen > bufferSize) {
            delete[] buf;
            buf = new unsigned char[dataLen];
            bufferSize = dataLen;
        }
        
        // 循环接收实际数据，确保完整
        bytesReceived = 0;
        while (bytesReceived < dataLen) {
            int ret = recv(rc->sock, (char*)(buf + bytesReceived), dataLen - bytesReceived, 0);
            if (ret == SOCKET_ERROR)
            {
                if (WSAGetLastError() == WSAETIMEDOUT)
                {
                    // T8 超时
                    if (rc->module->m_onSocketError)
                    {
                        rc->module->m_onSocketError(-4, "T8 timeout: message reception stalled");
                    }
                    goto cleanup;
                }
                SecsLogger::Warn("Socket actual data recv failed. ret=%d", ret);
                goto cleanup;
            }
            if (ret == 0) 
            {
                goto cleanup;  // 连接断开或出错
            }
            bytesReceived += ret;
        }
        // 保护共享资源
        EnterCriticalSection(&rc->module->m_cs);
        OnDataReceived callback = rc->module->m_onDataReceived;
        string remoteIp = rc->module->m_remoteIp;
        LeaveCriticalSection(&rc->module->m_cs);

        SecsLogger::RawRecv("HSMS", buf, dataLen);

        // 通知上层有数据到达 
         if (callback) {
             callback(buf, dataLen, remoteIp);  // 传入正确的数据长度，而不是最后一次recv的返回值
         }
    }

cleanup:
    // 清理动态分配的缓冲区
    delete[] buf;

    // 连接断开，更新状态
    EnterCriticalSection(&rc->module->m_cs);
    rc->module->m_isConnected = false;
    OnConnectionChanged callback = rc->module->m_onConnectionChanged;
    string remoteIp = rc->module->m_remoteIp;
    LeaveCriticalSection(&rc->module->m_cs);

    // 通知上层连接已断开
    if (callback) {
        callback(false, remoteIp);
    }

    // 释放资源
    closesocket(rc->sock);
    delete rc;
    return 0;
}

/**
 * @brief 作为客户端连接到远程服务器
 * 创建Socket并连接到指定IP和端口
 * @param ip 服务器IP地址（网络字节序）
 * @param port 服务器端口号
 * @return true表示连接成功，false表示失败
 */
bool SocketModule::Connect(unsigned long ip, unsigned short port) {
    if (ip == 0 || port == 0) return false;

    // 设置服务器地址结构
    sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_addr.S_un.S_addr = ip;
    addr.sin_port = htons(port);

    // 创建Socket
    SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (sock == INVALID_SOCKET) {
        if (m_onSocketError) m_onSocketError(WSAGetLastError(), "Failed to create socket");
        return false;
    }

    // 连接到服务器
    if (connect(sock, (LPSOCKADDR)&addr, sizeof(addr)) == SOCKET_ERROR) {
        if (m_onSocketError) m_onSocketError(WSAGetLastError(), "Failed to connect");
        closesocket(sock);
        return false;
    }

    // 将IP地址转换为字符串
    string ipStr = SocketModule::IpToString(addr.sin_addr);

    // 更新连接状态
    EnterCriticalSection(&m_cs);
    m_socket = sock;
    m_remoteIp = ipStr;
    m_remotePort = port;
    m_isConnected = true;

    OnConnectionChanged callback = m_onConnectionChanged;
    string remoteIp = m_remoteIp;
    LeaveCriticalSection(&m_cs);

    // 通知上层连接已建立
    if (callback) {
        callback(true, remoteIp);
    }

    // 启动接收线程
    RecvContext* rc = new RecvContext();
    rc->module = this;
    rc->sock = sock;
    rc->startup = CreateEvent(NULL, FALSE, FALSE, NULL);
    CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)RecvThread, rc, 0, NULL);
    WaitForSingleObject(rc->startup, INFINITE);
    CloseHandle(rc->startup);

    return true;
}

/**
 * @brief 断开连接
 * 关闭所有Socket，重置连接状态
 * @return true表示操作成功
 */
bool SocketModule::Disconnect() {
    EnterCriticalSection(&m_cs);
    
    // 关闭客户端连接Socket
    if (m_socket != INVALID_SOCKET) {
        closesocket(m_socket);
        m_socket = INVALID_SOCKET;
    }
    
    // 关闭监听Socket
    if (m_listenSocket != INVALID_SOCKET) {
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
    }
    
    bool wasConnected = m_isConnected;
    m_isConnected = false;
    
    OnConnectionChanged callback = m_onConnectionChanged;
    string remoteIp = m_remoteIp;
    LeaveCriticalSection(&m_cs);

    // 通知上层连接已断开
    if (wasConnected && callback) {
        callback(false, remoteIp);
    }
    return true;
}

/**
 * @brief 发送数据
 * 将数据发送到已连接的Socket
 * @param data 待发送数据指针
 * @param length 数据长度
 * @return 成功发送的字节数，失败返回-1
 */
int SocketModule::SendData(const unsigned char* data, int length) {
    // 参数校验
    if (data == nullptr || length <= 0) {
        return -1;
    }

    // 获取连接状态（保护共享资源）
    EnterCriticalSection(&m_cs);
    bool connected = m_isConnected;
    SOCKET sock = m_socket;
    LeaveCriticalSection(&m_cs);

    // 检查连接状态
    if (!connected || sock == INVALID_SOCKET) {
        return -1;
    }

    // 将长度和数据拼接在一起发送
    int totalLength = length + sizeof(int);
    unsigned char* buffer = new unsigned char[totalLength];
    
    // 先写入4字节长度（网络字节序）
    int size = htonl(length);
    memcpy_s(buffer, totalLength, &size, sizeof(int));
    
    // 再写入实际数据
    memcpy_s(buffer + sizeof(int), totalLength - sizeof(int), data, length);

    // 一次性发送
    if (send(sock, (char*)buffer, totalLength, 0) == SOCKET_ERROR) {
        delete[] buffer;
        if (m_onSocketError) m_onSocketError(WSAGetLastError(), "Failed to send data");
        return -1;
    }
    
    delete[] buffer;
    return length;
}

/**
 * @brief 设置数据接收回调函数
 * @param callback 回调函数指针
 */
void SocketModule::SetDataReceivedCallback(OnDataReceived callback) {
    EnterCriticalSection(&m_cs);
    m_onDataReceived = callback;
    LeaveCriticalSection(&m_cs);
}

/**
 * @brief 设置连接状态变更回调函数
 * @param callback 回调函数指针
 */
void SocketModule::SetConnectionCallback(OnConnectionChanged callback) {
    EnterCriticalSection(&m_cs);
    m_onConnectionChanged = callback;
    LeaveCriticalSection(&m_cs);
}

/**
 * @brief 设置错误回调函数
 * @param callback 回调函数指针
 */
void SocketModule::SetErrorCallback(OnSocketError callback) {
    EnterCriticalSection(&m_cs);
    m_onSocketError = callback;
    LeaveCriticalSection(&m_cs);
}

/**
 * @brief 获取当前连接状态
 * @return true表示已连接，false表示未连接
 */
bool SocketModule::IsConnected() const {
    EnterCriticalSection(&m_cs);
    bool result = m_isConnected;
    LeaveCriticalSection(&m_cs);
    return result;
}

/**
 * @brief 获取远程端IP地址
 * @return 远程端IP地址字符串
 */
string SocketModule::GetRemoteIp() const {
    EnterCriticalSection(&m_cs);
    string result = m_remoteIp;
    LeaveCriticalSection(&m_cs);
    return result;
}

void SocketModule::SetT8Timeout(int timeoutMs){
    m_t8TimeoutMs = timeoutMs;
}