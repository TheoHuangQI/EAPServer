#include "pch.h"
#include "TimeoutManager.h"

TimeoutManager::TimeoutManager() 
    : m_running(true),
      m_t3(10000), m_t5(5000), m_t6(5000), m_t7(10000), m_t8(10000) {
    m_checkThread = std::thread(&TimeoutManager::CheckLoop, this);
}

TimeoutManager::~TimeoutManager() {
    m_running = false;
    if (m_checkThread.joinable()) {
        m_checkThread.join();
    }
}

void TimeoutManager::SetTimeout(int t3, int t5, int t6, int t7, int t8) {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_t3 = t3;
    m_t5 = t5;
    m_t6 = t6;
    m_t7 = t7;
    m_t8 = t8;
}

void TimeoutManager::StartTimer(unsigned int msgId, int timeoutType, TimeoutCallback callback) {
    std::lock_guard<std::mutex> lock(m_mutex);
    
    TimerInfo info;
    info.msgId = msgId;
    info.timeoutType = timeoutType;
    info.timeoutMs = GetTimeoutMs(timeoutType);
    info.callback = callback;
    info.startTime = std::chrono::steady_clock::now();
    
    m_timers[msgId] = info;
}

void TimeoutManager::StopTimer(unsigned int msgId) {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_timers.erase(msgId);
}

bool TimeoutManager::HasTimer(unsigned int msgId) const {
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_timers.find(msgId) != m_timers.end();
}

void TimeoutManager::CheckLoop() {
    while (m_running) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        
        std::vector<unsigned int> expiredIds;
        
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            auto now = std::chrono::steady_clock::now();
            
            for (const auto& pair : m_timers) {
                const TimerInfo& info = pair.second;
                auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - info.startTime);
                
                if (elapsed.count() >= info.timeoutMs) {
                    expiredIds.push_back(pair.first);
                }
            }
        }
        
        for (unsigned int msgId : expiredIds) {
            TimeoutCallback callback;
            
            {
                std::lock_guard<std::mutex> lock(m_mutex);
                auto it = m_timers.find(msgId);
                if (it != m_timers.end()) {
                    callback = it->second.callback;
                    m_timers.erase(it);
                }
            }
            
            if (callback) {
                callback(msgId);
            }
        }
    }
}

unsigned int TimeoutManager::GetTimeoutMs(int timeoutType) const {
    switch (timeoutType) {
        case 3: return m_t3;
        case 5: return m_t5;
        case 6: return m_t6;
        case 7: return m_t7;
        case 8: return m_t8;
        default: return m_t3;
    }
}