#include "pch.h"
#include "SerialModule.h"

// 根据平台选择链接的库
#ifdef WINCE
#pragma comment(lib,"coredll.lib")
#else
#pragma comment(lib,"kernel32.lib")
#endif

/**
 * @brief 构造函数
 * 初始化成员变量和线程同步锁
 */
SerialModule::SerialModule()
        : m_isOpen(false), m_hSerial(INVALID_HANDLE_VALUE) {
    InitializeCriticalSection(&m_cs);
}

/**
 * @brief 析构函数
 * 关闭串口并释放资源
 */
SerialModule::~SerialModule() {
    Close();
    DeleteCriticalSection(&m_cs);
}

/**
 * @brief 打开串口连接（同步模式）
 * @param config 串口配置
 * @return true表示打开成功，false表示失败
 */
bool SerialModule::Open(const SerialConfig& config) {
    // 如果已打开，先关闭
    if (m_isOpen) {
        Close();
    }

    // 打开串口（同步模式，不使用 FILE_FLAG_OVERLAPPED）
    m_hSerial = CreateFileA(
        config.portName.c_str(),
        GENERIC_READ | GENERIC_WRITE,
        0,
        NULL,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,  // 同步模式，不使用异步标志
        NULL
    );

    if (m_hSerial == INVALID_HANDLE_VALUE) {
        if (m_onError) {
            m_onError(GetLastError(), std::string("Failed to open serial port: ") + config.portName);
        }
        return false;
    }

    // 配置串口参数
    DCB dcb;
    ZeroMemory(&dcb, sizeof(dcb));
    dcb.DCBlength = sizeof(dcb);
    if (!GetCommState(m_hSerial, &dcb)) {
        CloseHandle(m_hSerial);
        m_hSerial = INVALID_HANDLE_VALUE;
        if (m_onError) m_onError(GetLastError(), "Failed to get comm state");
        return false;
    }

    dcb.BaudRate = config.baudRate;
    dcb.ByteSize = config.dataBits;
    dcb.StopBits = (config.stopBits == 1) ? ONESTOPBIT : TWOSTOPBITS;
    dcb.Parity = NOPARITY;
    if (config.parity == 'E' || config.parity == 'e') dcb.Parity = EVENPARITY;
    else if (config.parity == 'O' || config.parity == 'o') dcb.Parity = ODDPARITY;
    
    if (!SetCommState(m_hSerial, &dcb)) {
        CloseHandle(m_hSerial);
        m_hSerial = INVALID_HANDLE_VALUE;
        if (m_onError) m_onError(GetLastError(), "Failed to set comm state");
        return false;
    }

    // 设置超时（同步模式）
    COMMTIMEOUTS timeouts;
    timeouts.ReadIntervalTimeout = config.t1;
    timeouts.ReadTotalTimeoutConstant = config.t1;
    timeouts.ReadTotalTimeoutMultiplier = 0;
    timeouts.WriteTotalTimeoutConstant = config.t2;
    timeouts.WriteTotalTimeoutMultiplier = 0;
    if (!SetCommTimeouts(m_hSerial, &timeouts)) {
        CloseHandle(m_hSerial);
        m_hSerial = INVALID_HANDLE_VALUE;
        if (m_onError) m_onError(GetLastError(), "Failed to set timeouts");
        return false;
    }

    // 清空缓冲区
    PurgeComm(m_hSerial, PURGE_RXCLEAR | PURGE_TXCLEAR);

    m_portName = config.portName;
    m_isOpen = true;

    if (m_onConnectionChanged) {
        m_onConnectionChanged(true, m_portName);
    }

    return true;
}

/**
 * @brief 关闭串口连接
 * @return true表示操作成功
 */
bool SerialModule::Close() {
    EnterCriticalSection(&m_cs);
    m_isOpen = false;

    if (m_hSerial != INVALID_HANDLE_VALUE) {
        CloseHandle(m_hSerial);
        m_hSerial = INVALID_HANDLE_VALUE;
    }

    LeaveCriticalSection(&m_cs);

    // 通知上层连接已断开
    if (m_onConnectionChanged) {
        m_onConnectionChanged(false, m_portName);
    }

    return true;
}

/**
 * @brief 发送数据（同步）
 * @param data 待发送数据指针
 * @param length 数据长度
 * @return 成功发送的字节数，失败返回-1
 */
int SerialModule::SendData(const unsigned char* data, int length) {
    if (!m_isOpen || m_hSerial == INVALID_HANDLE_VALUE || data == nullptr || length <= 0) {
        if (m_onError) m_onError(-1, "Invalid parameters or not open");
        return -1;
    }

    DWORD bytesWritten = 0;
    if (!WriteFile(m_hSerial, data, length, &bytesWritten, NULL)) {
        if (m_onError) m_onError(GetLastError(), "Failed to write to serial port");
        return -1;
    }

    return (int)bytesWritten;
}

/**
 * @brief 接收数据（同步）
 * @param buffer 接收缓冲区指针
 * @param bufferSize 接收缓冲区大小
 * @param timeoutMs 超时时间（毫秒），0表示无限等待
 * @return 成功接收的字节数，失败返回-1
 */
int SerialModule::ReceiveData(unsigned char* buffer, int bufferSize, int timeoutMs) {
    if (!m_isOpen || m_hSerial == INVALID_HANDLE_VALUE || buffer == nullptr || bufferSize <= 0) {
        if (m_onError) m_onError(-1, "Invalid parameters or not open");
        return -1;
    }

    // 如果指定了超时时间，临时修改超时设置
    if (timeoutMs > 0) {
        COMMTIMEOUTS timeouts;
        timeouts.ReadIntervalTimeout = timeoutMs;
        timeouts.ReadTotalTimeoutConstant = timeoutMs;
        timeouts.ReadTotalTimeoutMultiplier = 0;
        timeouts.WriteTotalTimeoutConstant = 0;
        timeouts.WriteTotalTimeoutMultiplier = 0;
        SetCommTimeouts(m_hSerial, &timeouts);
    }

    DWORD bytesRead = 0;
    if (!ReadFile(m_hSerial, buffer, bufferSize, &bytesRead, NULL)) {
        int error = GetLastError();
        if (error == ERROR_TIMEOUT) {
            // 超时不是错误，返回0表示没有读取到数据
            return 0;
        }
        if (m_onError) m_onError(error, "Failed to read from serial port");
        return -1;
    }

    return (int)bytesRead;
}

/**
 * @brief 同步读取一个字节
 * @param byte 输出参数，存储读取的字节
 * @param timeoutMs 超时时间（毫秒），0表示无限等待
 * @return true表示读取成功，false表示失败或超时
 */
bool SerialModule::ReadByte(unsigned char& byte, int timeoutMs) {
    if (!m_isOpen || m_hSerial == INVALID_HANDLE_VALUE) {
        if (m_onError) m_onError(-1, "Serial port not open");
        return false;
    }

    // 如果指定了超时时间，临时修改超时设置
    if (timeoutMs > 0) {
        COMMTIMEOUTS timeouts;
        timeouts.ReadIntervalTimeout = timeoutMs;
        timeouts.ReadTotalTimeoutConstant = timeoutMs;
        timeouts.ReadTotalTimeoutMultiplier = 0;
        timeouts.WriteTotalTimeoutConstant = 0;
        timeouts.WriteTotalTimeoutMultiplier = 0;
        SetCommTimeouts(m_hSerial, &timeouts);
    }

    DWORD bytesRead = 0;
    if (!ReadFile(m_hSerial, &byte, 1, &bytesRead, NULL)) {
        int error = GetLastError();
        if (error == ERROR_TIMEOUT) {
            // 超时
            return false;
        }
        if (m_onError) m_onError(error, "Failed to read byte from serial port");
        return false;
    }

    return (bytesRead == 1);
}

/**
 * @brief 设置数据接收回调函数
 * @param callback 回调函数指针
 */
void SerialModule::SetDataReceivedCallback(OnSerialDataReceived callback) {
    EnterCriticalSection(&m_cs);
    m_onDataReceived = callback;
    LeaveCriticalSection(&m_cs);
}

/**
 * @brief 设置连接状态变更回调函数
 * @param callback 回调函数指针
 */
void SerialModule::SetConnectionCallback(OnSerialConnectionChanged callback) {
    EnterCriticalSection(&m_cs);
    m_onConnectionChanged = callback;
    LeaveCriticalSection(&m_cs);
}

/**
 * @brief 设置错误回调函数
 * @param callback 回调函数指针
 */
void SerialModule::SetErrorCallback(OnSerialError callback) {
    EnterCriticalSection(&m_cs);
    m_onError = callback;
    LeaveCriticalSection(&m_cs);
}

/**
 * @brief 获取当前连接状态
 * @return true表示已连接，false表示未连接
 */
bool SerialModule::IsOpen() const {
    return m_isOpen;
}

/**
 * @brief 获取当前串口号
 * @return 串口号字符串
 */
std::string SerialModule::GetPortName() const {
    return m_portName;
}