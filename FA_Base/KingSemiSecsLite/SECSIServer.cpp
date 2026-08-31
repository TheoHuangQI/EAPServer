#include "pch.h"
#include "SECSIServer.h"
#include <cstdio>
#include <vector>
#include <windows.h>
#include <map>
#include <algorithm>

static void get_timestamp(char* buf, int len) {
    SYSTEMTIME st;
    GetLocalTime(&st);
    sprintf_s(buf, len, "%02d:%02d:%02d.%03d", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
}

#define SECSI_ENQ 0x05
#define SECSI_ACK 0x06
#define SECSI_NAK 0x15
#define SECSI_EOT 0x04

SECSIServer::SECSIServer() 
    : m_isSelected(false), 
      m_onMessageReceived(nullptr), m_onConnectionChanged(nullptr), m_onError(nullptr),
      m_frameBufferPos(0),
      m_t1Timeout(1000), m_t2Timeout(5000), m_t3Timeout(30000), m_t4Timeout(10000),
      m_lastDataReceivedTime(0),
      m_useCRC(true),
      m_receiveThread(NULL), m_receiveThreadRunning(false),
      m_sendThread(NULL), m_sendThreadRunning(false),
      m_maxRetries(3), m_baseBackoffMs(100), m_maxBackoffMs(2000),
      m_protocolState(ProtocolState::IDLE),
      m_isMaster(false) {
    InitializeCriticalSection(&m_cs);
    InitializeCriticalSection(&m_sendCs);
    InitializeCriticalSection(&m_queueCs);
    
    m_serialModule.SetConnectionCallback([this](bool connected, const std::string& port) {
        if (m_onConnectionChanged) {
            m_onConnectionChanged(connected, port);
        }
    });
}

SECSIServer::~SECSIServer() {
    StopServer();
    DeleteCriticalSection(&m_sendCs);
    DeleteCriticalSection(&m_cs);
    DeleteCriticalSection(&m_queueCs);
}

void SECSIServer::SetMasterMode(bool isMaster) {
    m_isMaster = isMaster;
}

int SECSIServer::CalculateBackoff(int attempt) const {
    int backoff = m_baseBackoffMs * (1 << attempt);
    if (backoff > m_maxBackoffMs) {
        backoff = m_maxBackoffMs;
    }
    backoff += (int)(GetTickCount() % 50);
    return backoff;
}

unsigned int __stdcall SECSIServer::ReceiveThreadFunc(void* param) {
    SECSIServer* pThis = (SECSIServer*)param;
    char _ts[32];
    
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Receive thread started\n", _ts);
    
    while (pThis->m_receiveThreadRunning && pThis->m_serialModule.IsOpen()) {
        if (pThis->m_protocolState == ProtocolState::IDLE) {
            SECSIMessage msg;
            if (pThis->ReceiveMessage(msg)) {
                get_timestamp(_ts, sizeof(_ts));
                printf("%s [SECS-I] Message received in thread\n", _ts);
                pThis->ProcessReceivedMessage(msg);
                SECSIServer::FreeMessageData(msg);
            }
        }
        Sleep(1);
    }
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Receive thread stopped\n", _ts);
    return 0;
}

unsigned int __stdcall SECSIServer::SendThreadFunc(void* param) {
    SECSIServer* pThis = (SECSIServer*)param;
    char _ts[32];
    
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Send thread started\n", _ts);
    
    int debugCount = 0;
    while (pThis->m_sendThreadRunning && pThis->m_serialModule.IsOpen()) {
        bool isIdle = false;
        EnterCriticalSection(&pThis->m_cs);
        isIdle = (pThis->m_protocolState == ProtocolState::IDLE);
        LeaveCriticalSection(&pThis->m_cs);
        
        bool queueNotEmpty = !pThis->m_sendQueue.Empty();
        
        if (debugCount++ % 1000 == 0) {
            get_timestamp(_ts, sizeof(_ts));
            EnterCriticalSection(&pThis->m_cs);
            int state = (int)pThis->m_protocolState;
            LeaveCriticalSection(&pThis->m_cs);
            printf("%s [SECS-I] Send thread - Running=%d, IsOpen=%d, State=%d, QueueSize=%d\n", 
                   _ts, 
                   pThis->m_sendThreadRunning, 
                   pThis->m_serialModule.IsOpen(), 
                   state,
                   (int)pThis->m_sendQueue.Size());
        }
        
        if (isIdle && queueNotEmpty) {
            SECSIMessage msg;
            if (pThis->m_sendQueue.TryDequeue(msg)) {
                get_timestamp(_ts, sizeof(_ts));
                printf("%s [SECS-I] Send thread - Processing message from queue, Stream=0x%02X, Function=0x%02X\n", 
                       _ts, msg.header.stream, msg.header.function);
                
                int result = pThis->DoSendSECSIMessage(msg);
                if (result <= 0) {
                    get_timestamp(_ts, sizeof(_ts));
                    printf("%s [SECS-I] Send thread - Failed to send message after max retries, discarding\n", _ts);
                    if (pThis->m_onError) {
                        pThis->m_onError((int)SECSIError::PROTOCOL_ERROR, "Failed to send message after max retries");
                    }
                }
                SECSIServer::FreeMessageData(msg);
            }
        }
        Sleep(1);
    }
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Send thread stopped\n", _ts);
    return 0;
}

bool SECSIServer::StartServer(const SerialConfig& config) {
    StopServer();
    
    if (!m_serialModule.Open(config)) {
        return false;
    }
    
    m_sendQueue.Reset();
    
    m_receiveThreadRunning = true;
    m_receiveThread = (HANDLE)_beginthreadex(NULL, 0, ReceiveThreadFunc, this, 0, &m_receiveThreadId);
    
    if (m_receiveThread == NULL) {
        m_serialModule.Close();
        m_receiveThreadRunning = false;
        if (m_onError) m_onError(-100, "Failed to create receive thread");
        return false;
    }
    
    m_sendThreadRunning = true;
    m_sendThread = (HANDLE)_beginthreadex(NULL, 0, SendThreadFunc, this, 0, &m_sendThreadId);
    
    if (m_sendThread == NULL) {
        m_receiveThreadRunning = false;
        WaitForSingleObject(m_receiveThread, 2000);
        CloseHandle(m_receiveThread);
        m_receiveThread = NULL;
        m_serialModule.Close();
        m_sendThreadRunning = false;
        if (m_onError) m_onError(-100, "Failed to create send thread");
        return false;
    }
    
    char _ts[32];
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Server started on %s, Mode=%s\n", _ts, config.portName.c_str(), 
           m_isMaster ? "MASTER" : "SLAVE");
    
    return true;
}

bool SECSIServer::StopServer() {
    m_receiveThreadRunning = false;
    m_sendThreadRunning = false;
    
    if (m_receiveThread != NULL) {
        WaitForSingleObject(m_receiveThread, 2000);
        CloseHandle(m_receiveThread);
        m_receiveThread = NULL;
    }
    
    if (m_sendThread != NULL) {
        WaitForSingleObject(m_sendThread, 2000);
        CloseHandle(m_sendThread);
        m_sendThread = NULL;
    }
    
    m_sendQueue.Stop();
    
    EnterCriticalSection(&m_cs);
    m_isSelected = false;
    m_frameBufferPos = 0;
    m_protocolState = ProtocolState::IDLE;
    LeaveCriticalSection(&m_cs);
    
    bool result = m_serialModule.Close();
    
    char _ts[32];
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Server stopped\n", _ts);
    
    return result;
}

int SECSIServer::SendSECSIMessage(const SECSIMessage& msg) {
    char _ts[32];
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] SendSECSIMessage - Stream=0x%02X, Function=0x%02X\n", 
           _ts, msg.header.stream, msg.header.function);
    
    if (!m_serialModule.IsOpen()) {
        if (m_onError) m_onError((int)SECSIError::NOT_CONNECTED, "Not connected");
        return -1;
    }
    
    SECSIMessage copyMsg;
    copyMsg.header = msg.header;
    copyMsg.dataLength = msg.dataLength;
    
    if (msg.data != nullptr && msg.dataLength > 0) {
        copyMsg.data = new unsigned char[msg.dataLength];
        memcpy(copyMsg.data, msg.data, msg.dataLength);
    } else {
        copyMsg.data = nullptr;
    }
    
    if (!m_sendQueue.Enqueue(copyMsg)) {
        FreeMessageData(copyMsg);
        if (m_onError) m_onError((int)SECSIError::MEMORY_ERROR, "Send queue is full");
        return -1;
    }
    
    return msg.dataLength + 10;
}

int SECSIServer::DoSendSECSIMessage(const SECSIMessage& msg) {
    char _ts[32];
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] DoSendSECSIMessage - Stream=0x%02X, Function=0x%02X\n", 
           _ts, msg.header.stream, msg.header.function);
    
    EnterCriticalSection(&m_cs);
    
    if (!m_serialModule.IsOpen()) {
        if (m_onError) m_onError((int)SECSIError::NOT_CONNECTED, "Not connected");
        LeaveCriticalSection(&m_cs);
        return -1;
    }
    
    if (m_protocolState != ProtocolState::IDLE) {
        if (m_onError) m_onError((int)SECSIError::STATE_ERROR, "Not in IDLE state");
        LeaveCriticalSection(&m_cs);
        return -1;
    }
    
    m_protocolState = ProtocolState::LINE_CONTROL;
    LeaveCriticalSection(&m_cs);
    
    std::vector<SECSIMessage> blocks = SplitMessageIntoBlocks(msg);
    int totalResult = 0;
    
    for (size_t i = 0; i < blocks.size(); i++) {
        int result = SendSingleBlock(blocks[i]);
        if (result <= 0) {
            for (auto& block : blocks) {
                if (block.data != nullptr && block.data != msg.data) {
                    delete[] block.data;
                }
            }
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return -1;
        }
        totalResult += result;
    }
    
    EnterCriticalSection(&m_cs);
    m_protocolState = ProtocolState::IDLE;
    LeaveCriticalSection(&m_cs);
    
    for (auto& block : blocks) {
        if (block.data != nullptr && block.data != msg.data) {
            delete[] block.data;
        }
    }
    
    return totalResult;
}

int SECSIServer::SendSingleBlock(const SECSIMessage& msg) {
    char _ts[32];
    int rtries = 0;
    
    while (rtries < m_maxRetries) {
        // ========== Line Control State ==========
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::LINE_CONTROL;
        LeaveCriticalSection(&m_cs);
        rtries = 0;
        
    LineControlStart:
        get_timestamp(_ts, sizeof(_ts));
        printf("%s [SECS-I] Line Control - rtries=%d, sending ENQ\n", _ts, rtries);
        
        unsigned char enq = SECSI_ENQ;
        if (m_serialModule.SendData(&enq, 1) <= 0) {
            rtries++;
            if (rtries < m_maxRetries) {
                Sleep(CalculateBackoff(rtries));
                goto LineControlStart;
            }
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return -1;
        }
        
        DWORD tStart = GetTickCount();
        
        while (true) {
            DWORD t = GetTickCount() - tStart;
            
            // 检查T2超时
            if (t > (DWORD)m_t2Timeout) {
                get_timestamp(_ts, sizeof(_ts));
                printf("%s [SECS-I] Line Control - T2 timeout\n", _ts);
                rtries++;
                if (rtries >= m_maxRetries) {
                    get_timestamp(_ts, sizeof(_ts));
                    printf("%s [SECS-I] Line Control - Max retries exceeded\n", _ts);
                    EnterCriticalSection(&m_cs);
                    m_protocolState = ProtocolState::IDLE;
                    LeaveCriticalSection(&m_cs);
                    return -1;
                }
                Sleep(CalculateBackoff(rtries));
                goto LineControlStart;
            }
            
            unsigned char ctrl;
            if (m_serialModule.ReadByte(ctrl, 10)) {
                if (ctrl == SECSI_EOT) {
                    // 收到EOT，进入Send状态
                    get_timestamp(_ts, sizeof(_ts));
                    printf("%s [SECS-I] Line Control - Received EOT, entering Send state\n", _ts);
                    break;
                } else if (ctrl == SECSI_ENQ) {
                    // 冲突检测：Slave收到ENQ响应自己的ENQ时
                    if (!m_isMaster) {
                        get_timestamp(_ts, sizeof(_ts));
                        printf("%s [SECS-I] Line Control - Contention detected, sending EOT\n", _ts);
                        unsigned char eot = SECSI_EOT;
                        m_serialModule.SendData(&eot, 1);
                        EnterCriticalSection(&m_cs);
                        m_protocolState = ProtocolState::IDLE;
                        LeaveCriticalSection(&m_cs);
                        return -1;
                    }
                }
            }
        }
        
        // ========== Send State ==========
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::SEND;
        LeaveCriticalSection(&m_cs);
        
        const int maxBufferSize = 512;
        unsigned char* buffer = new unsigned char[maxBufferSize];
        int encodedLength = EncodeSECSIMessage(msg, buffer, maxBufferSize);
        
        if (encodedLength <= 0) {
            delete[] buffer;
            rtries++;
            if (rtries < m_maxRetries) {
                Sleep(CalculateBackoff(rtries));
                goto LineControlStart;
            }
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return -1;
        }
        
        get_timestamp(_ts, sizeof(_ts));
        printf("%s [SECS-I] Send State - Sending data block (length=%d)\n", _ts, encodedLength);
        if (m_serialModule.SendData(buffer, encodedLength) != encodedLength) {
            delete[] buffer;
            rtries++;
            if (rtries < m_maxRetries) {
                Sleep(CalculateBackoff(rtries));
                goto LineControlStart;
            }
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return -1;
        }
        delete[] buffer;
        
        // ========== Send Completion State ==========
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::SEND_COMPLETION;
        LeaveCriticalSection(&m_cs);
        
        tStart = GetTickCount();
        bool ackReceived = false;
        
        while (true) {
            DWORD t = GetTickCount() - tStart;
            
            // 检查T2超时
            if (t > (DWORD)m_t2Timeout) {
                get_timestamp(_ts, sizeof(_ts));
                printf("%s [SECS-I] Send Completion - T2 timeout\n", _ts);
                rtries++;
                if (rtries >= m_maxRetries) {
                    EnterCriticalSection(&m_cs);
                    m_protocolState = ProtocolState::IDLE;
                    LeaveCriticalSection(&m_cs);
                    return -1;
                }
                Sleep(CalculateBackoff(rtries));
                goto LineControlStart;
            }
            
            unsigned char ctrl;
            if (m_serialModule.ReadByte(ctrl, 10)) {
                if (ctrl == SECSI_ACK) {
                    get_timestamp(_ts, sizeof(_ts));
                    printf("%s [SECS-I] Send Completion - Received ACK, Block Sent\n", _ts);
                    ackReceived = true;
                    break;
                } else if (ctrl == SECSI_NAK) {
                    get_timestamp(_ts, sizeof(_ts));
                    printf("%s [SECS-I] Send Completion - Received NAK, retrying\n", _ts);
                    rtries++;
                    if (rtries >= m_maxRetries) {
                        EnterCriticalSection(&m_cs);
                        m_protocolState = ProtocolState::IDLE;
                        LeaveCriticalSection(&m_cs);
                        return -1;
                    }
                    Sleep(CalculateBackoff(rtries));
                    goto LineControlStart;
                }
            }
        }
        
        if (ackReceived) {
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return 10 + msg.dataLength + 2;
        }
    }
    
    EnterCriticalSection(&m_cs);
    m_protocolState = ProtocolState::IDLE;
    LeaveCriticalSection(&m_cs);
    return -1;
}

bool SECSIServer::ReceiveMessage(SECSIMessage& msg) {
    char _ts[32];
    
    // ========== Idle State: 等待ENQ ==========
    unsigned char ctrl;
    while (true) {
        if (m_serialModule.ReadByte(ctrl, 10)) {
            if (ctrl == SECSI_ENQ) {
                get_timestamp(_ts, sizeof(_ts));
                printf("%s [SECS-I] Idle State - Received ENQ\n", _ts);
                break;
            }
        }
        
        if (!m_receiveThreadRunning) {
            return false;
        }
        
        Sleep(1);
    }
    
    // ========== Line Control: 回复EOT ==========
    EnterCriticalSection(&m_cs);
    m_protocolState = ProtocolState::LINE_CONTROL;
    LeaveCriticalSection(&m_cs);
    
    unsigned char eot = SECSI_EOT;
    if (m_serialModule.SendData(&eot, 1) <= 0) {
        get_timestamp(_ts, sizeof(_ts));
        printf("%s [SECS-I] Line Control - Failed to send EOT\n", _ts);
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::IDLE;
        LeaveCriticalSection(&m_cs);
        return false;
    }
    
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Line Control - Sent EOT, entering Receive state\n", _ts);
    
    // ========== Receive State: 接收长度字节和数据 ==========
    EnterCriticalSection(&m_cs);
    m_protocolState = ProtocolState::RECEIVE;
    LeaveCriticalSection(&m_cs);
    
    unsigned char lengthByte = 0;
    DWORD tStart = GetTickCount();
    
    // 接收长度字节
    while (true) {
        DWORD t = GetTickCount() - tStart;
        
        if (t > (DWORD)m_t2Timeout) {
            get_timestamp(_ts, sizeof(_ts));
            printf("%s [SECS-I] Receive State - T2 timeout waiting for length byte\n", _ts);
            SendNAK();
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return false;
        }
        
        if (m_serialModule.ReadByte(lengthByte, 10)) {
            break;
        }
    }
    
    int expectedLength = lengthByte;
    
    // 验证长度字节有效性
    if (expectedLength < 10 || expectedLength > 254) {
        get_timestamp(_ts, sizeof(_ts));
        printf("%s [SECS-I] Receive State - Invalid length byte: %d\n", _ts, expectedLength);
        SendNAK();
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::IDLE;
        LeaveCriticalSection(&m_cs);
        return false;
    }
    
    // 接收N+2个字节（数据+校验和）
    const int maxFrameSize = 512;
    unsigned char frameBuffer[maxFrameSize];
    
    tStart = GetTickCount();
    int bytesRead = 0;
    
    while (bytesRead < expectedLength + 2) {
        DWORD t = GetTickCount() - tStart;
        
        // 检查T1超时（字符间超时）
        if (t > (DWORD)m_t1Timeout) {
            get_timestamp(_ts, sizeof(_ts));
            printf("%s [SECS-I] Receive State - T1 timeout\n", _ts);
            SendNAK();
            EnterCriticalSection(&m_cs);
            m_protocolState = ProtocolState::IDLE;
            LeaveCriticalSection(&m_cs);
            return false;
        }
        
        unsigned char ch;
        if (m_serialModule.ReadByte(ch, 10)) {
            frameBuffer[bytesRead++] = ch;
            tStart = GetTickCount(); // 重置超时计时器
        }
    }
    
    // ========== Receive Completion State ==========
    EnterCriticalSection(&m_cs);
    m_protocolState = ProtocolState::RECEIVE_COMPLETION;
    LeaveCriticalSection(&m_cs);
    
    // 比较校验和
    unsigned short receivedChecksum = (frameBuffer[expectedLength] << 8) | frameBuffer[expectedLength + 1];
    unsigned short calculatedChecksum = CalculateChecksum(frameBuffer, expectedLength);
    
    if (receivedChecksum != calculatedChecksum) {
        get_timestamp(_ts, sizeof(_ts));
        printf("%s [SECS-I] Receive Completion - Checksum mismatch: received=0x%04X, calculated=0x%04X\n", 
               _ts, receivedChecksum, calculatedChecksum);
        SendNAK();
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::IDLE;
        LeaveCriticalSection(&m_cs);
        return false;
    }
    
    // 正确接收后发送ACK
    unsigned char ack = SECSI_ACK;
    if (m_serialModule.SendData(&ack, 1) <= 0) {
        get_timestamp(_ts, sizeof(_ts));
        printf("%s [SECS-I] Receive Completion - Failed to send ACK\n", _ts);
        EnterCriticalSection(&m_cs);
        m_protocolState = ProtocolState::IDLE;
        LeaveCriticalSection(&m_cs);
        return false;
    }
    
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Receive Completion - Sent ACK, Block Received\n", _ts);
    
    EnterCriticalSection(&m_cs);
    m_protocolState = ProtocolState::IDLE;
    LeaveCriticalSection(&m_cs);
    
    return DecodeSECSIMessage(frameBuffer, expectedLength, msg);
}

void SECSIServer::SendNAK() {
    char _ts[32];
    
    unsigned char nak = SECSI_NAK;
    m_serialModule.SendData(&nak, 1);
    
    get_timestamp(_ts, sizeof(_ts));
    printf("%s [SECS-I] Sent NAK\n", _ts);
}

bool SECSIServer::ReceiveSECSIMessage(SECSIMessage& msg, int timeoutMs) {
    if (!m_serialModule.IsOpen()) {
        if (m_onError) m_onError((int)SECSIError::NOT_CONNECTED, "Not connected");
        return false;
    }
    
    int waitTime = 0;
    while (m_protocolState != ProtocolState::IDLE && waitTime < timeoutMs) {
        Sleep(10);
        waitTime += 10;
    }
    
    if (m_protocolState != ProtocolState::IDLE) {
        return false;
    }
    
    DWORD startTime = GetTickCount();
    while (true) {
        if (ReceiveMessage(msg)) {
            return true;
        }
        
        if (timeoutMs > 0 && GetTickCount() - startTime >= (DWORD)timeoutMs) {
            return false;
        }
        
        Sleep(1);
    }
}

std::vector<SECSIMessage> SECSIServer::SplitMessageIntoBlocks(const SECSIMessage& msg) {
    std::vector<SECSIMessage> blocks;
    
    const int maxDataPerBlock = 244;
    
    if (msg.dataLength <= maxDataPerBlock) {
        blocks.push_back(msg);
        return blocks;
    }
    
    int offset = 0;
    unsigned short blockNumber = 1;
    
    while (offset < msg.dataLength) {
        SECSIMessage block;
        block.header = msg.header;
        block.header.blockNumber = blockNumber++;
        block.header.endBit = (offset + maxDataPerBlock >= msg.dataLength);
        
        int blockDataLength = min(maxDataPerBlock, msg.dataLength - offset);
        block.data = new unsigned char[blockDataLength];
        memcpy(block.data, msg.data + offset, blockDataLength);
        block.dataLength = blockDataLength;
        
        blocks.push_back(block);
        offset += blockDataLength;
    }
    
    return blocks;
}

int SECSIServer::EncodeSECSIMessage(const SECSIMessage& msg, unsigned char* buffer, int bufferSize) {
    if (buffer == nullptr || bufferSize < 13) {
        return -1;
    }
    
    int pos = 0;
    int totalDataLength = 10 + msg.dataLength;
    
    if (totalDataLength > 254) {
        return -1;
    }
    
    buffer[pos++] = (unsigned char)totalDataLength;
    
    unsigned short deviceId = msg.header.deviceId & 0x7FFF;
    bool rbitValue = (msg.header.ms == SECSIMode::SLAVE);
    buffer[pos++] = ((rbitValue ? 0x80 : 0x00) | ((deviceId >> 8) & 0x7F));
    buffer[pos++] = deviceId & 0xFF;
    
    buffer[pos++] = (msg.header.wbit ? 0x80 : 0x00) | (msg.header.stream & 0x7F);
    buffer[pos++] = msg.header.function;
    
    unsigned short blockNumber = msg.header.blockNumber & 0x7FFF;
    buffer[pos++] = ((msg.header.endBit ? 0x80 : 0x00) | ((blockNumber >> 8) & 0x7F));
    buffer[pos++] = blockNumber & 0xFF;
    
    buffer[pos++] = (msg.header.systemBytes >> 24) & 0xFF;
    buffer[pos++] = (msg.header.systemBytes >> 16) & 0xFF;
    buffer[pos++] = (msg.header.systemBytes >> 8) & 0xFF;
    buffer[pos++] = msg.header.systemBytes & 0xFF;
    
    if (msg.data != nullptr && msg.dataLength > 0) {
        if (pos + msg.dataLength > bufferSize - 2) {
            return -1;
        }
        memcpy(buffer + pos, msg.data, msg.dataLength);
        pos += msg.dataLength;
    }
    
    unsigned short checksum = CalculateChecksum(buffer + 1, totalDataLength);
    buffer[pos++] = (checksum >> 8) & 0xFF;
    buffer[pos++] = checksum & 0xFF;
    
    return pos;
}

bool SECSIServer::DecodeSECSIMessage(const unsigned char* data, int length, SECSIMessage& msg) {
    if (data == nullptr || length < 10) {
        return false;
    }
    
    int pos = 0;
    
    unsigned short devHigh = data[pos++];
    unsigned short devLow = data[pos++];
    msg.header.rbit = (devHigh & 0x80) != 0;
    msg.header.deviceId = ((devHigh & 0x7F) << 8) | devLow;
    msg.header.ms = msg.header.rbit ? SECSIMode::SLAVE : SECSIMode::MASTER;
    
    unsigned char messageIdByte = data[pos++];
    msg.header.wbit = (messageIdByte & 0x80) != 0;
    msg.header.stream = messageIdByte & 0x7F;
    msg.header.function = data[pos++];
    
    unsigned short blockHigh = data[pos++];
    unsigned short blockLow = data[pos++];
    msg.header.endBit = (blockHigh & 0x80) != 0;
    msg.header.blockNumber = ((blockHigh & 0x7F) << 8) | blockLow;
    
    unsigned char sysByte1 = data[pos++];
    unsigned char sysByte2 = data[pos++];
    unsigned char sysByte3 = data[pos++];
    unsigned char sysByte4 = data[pos++];
    msg.header.systemBytes = (sysByte1 << 24) | (sysByte2 << 16) | (sysByte3 << 8) | sysByte4;
    
    int dataLength = length - 10;
    if (dataLength > 0) {
        msg.data = new unsigned char[dataLength];
        memcpy(msg.data, data + pos, dataLength);
        msg.dataLength = dataLength;
    } else {
        msg.data = nullptr;
        msg.dataLength = 0;
    }
    
    return true;
}

void SECSIServer::ProcessReceivedMessage(const SECSIMessage& msg) {
    if (m_onMessageReceived) {
        m_onMessageReceived(msg);
    }
}

unsigned short SECSIServer::CalculateChecksum(const unsigned char* data, int length) {
    unsigned short checksum = 0;
    for (int i = 0; i < length; i++) {
        checksum += data[i];
    }
    return checksum;
}

unsigned short SECSIServer::CalculateCRC(const unsigned char* data, int length) {
    unsigned short crc = 0xFFFF;
    for (int i = 0; i < length; i++) {
        crc ^= data[i];
        for (int j = 0; j < 8; j++) {
            crc = (crc >> 1) ^ (crc & 1 ? 0xA001 : 0);
        }
    }
    return crc;
}

bool SECSIServer::CheckT4Timeout() const {
    if (m_lastDataReceivedTime == 0) {
        return true;
    }
    return (GetTickCount() - m_lastDataReceivedTime) < (DWORD)m_t4Timeout;
}

void SECSIServer::SetT1Timeout(int timeout) { m_t1Timeout = timeout; }
void SECSIServer::SetT2Timeout(int timeout) { m_t2Timeout = timeout; }
void SECSIServer::SetT3Timeout(int timeout) { m_t3Timeout = timeout; }
void SECSIServer::SetT4Timeout(int timeout) { m_t4Timeout = timeout; }
int SECSIServer::GetT1Timeout() const { return m_t1Timeout; }
int SECSIServer::GetT2Timeout() const { return m_t2Timeout; }
int SECSIServer::GetT3Timeout() const { return m_t3Timeout; }
int SECSIServer::GetT4Timeout() const { return m_t4Timeout; }

void SECSIServer::SetSECSIMessageCallback(OnSECSIMessageReceived callback) {
    m_onMessageReceived = callback;
}

void SECSIServer::SetConnectionCallback(OnSECSIConnectionChanged callback) {
    m_onConnectionChanged = callback;
}

void SECSIServer::SetErrorCallback(OnSECSIError callback) {
    m_onError = callback;
}

bool SECSIServer::IsConnected() const {
    return m_serialModule.IsOpen();
}

void SECSIServer::FreeMessageData(SECSIMessage& msg) {
    if (msg.data != nullptr) {
        delete[] msg.data;
        msg.data = nullptr;
        msg.dataLength = 0;
    }
}