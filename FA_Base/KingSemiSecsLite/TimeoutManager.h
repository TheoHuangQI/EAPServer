#pragma once
#include <map>
#include <functional>
#include <mutex>
#include <thread>
#include <chrono>

typedef std::function<void(unsigned int msgId)> TimeoutCallback;

class TimeoutManager {
public:
    TimeoutManager();
    ~TimeoutManager();
    
    void SetTimeout(int t3, int t5, int t6, int t7, int t8);
    void StartTimer(unsigned int msgId, int timeoutType, TimeoutCallback callback);
    void StopTimer(unsigned int msgId);
    bool HasTimer(unsigned int msgId) const;
    unsigned int GetTimeoutMs(int timeoutType) const;

private:
    struct TimerInfo {
        unsigned int msgId;
        int timeoutType;
        unsigned int timeoutMs;
        TimeoutCallback callback;
        std::chrono::steady_clock::time_point startTime;
    };
    
    std::map<unsigned int, TimerInfo> m_timers;
    mutable std::mutex m_mutex;
    std::thread m_checkThread;
    bool m_running;
    
    int m_t3, m_t5, m_t6, m_t7, m_t8;
    
    void CheckLoop();
};