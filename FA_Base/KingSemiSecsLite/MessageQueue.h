#pragma once
#include <queue>
#include <mutex>
#include <condition_variable>

template<typename T>
class MessageQueue {
public:
    MessageQueue(size_t maxSize = 1000) : m_maxSize(maxSize), m_stopped(false) {}
    
    ~MessageQueue() {
        Stop();
    }
    
    bool Enqueue(const T& msg) {
        std::unique_lock<std::mutex> lock(m_mutex);
        
        if (m_stopped) return false;
        if (m_queue.size() >= m_maxSize) return false;
        
        m_queue.push(msg);
        m_condition.notify_one();
        return true;
    }
    
    bool Dequeue(T& msg) {
        std::unique_lock<std::mutex> lock(m_mutex);
        
        while (m_queue.empty() && !m_stopped) {
            m_condition.wait(lock);
        }
        
        if (m_stopped && m_queue.empty()) return false;
        
        msg = m_queue.front();
        m_queue.pop();
        return true;
    }
    
    bool TryDequeue(T& msg) {
        std::lock_guard<std::mutex> lock(m_mutex);
        
        if (m_queue.empty() || m_stopped) return false;
        
        msg = m_queue.front();
        m_queue.pop();
        return true;
    }
    
    size_t Size() const {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_queue.size();
    }
    
    bool Empty() const {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_queue.empty();
    }
    
    void Stop() {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_stopped = true;
        m_condition.notify_all();
    }
    
    void Reset() {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_stopped = false;
        while (!m_queue.empty()) {
            m_queue.pop();
        }
    }
    
    void Clear() {
        std::lock_guard<std::mutex> lock(m_mutex);
        while (!m_queue.empty()) {
            m_queue.pop();
        }
    }

private:
    std::queue<T> m_queue;
    mutable std::mutex m_mutex;
    std::condition_variable m_condition;
    size_t m_maxSize;
    bool m_stopped;
};