#include "pch.h"
#include "SecsLogger.h"
#include <Windows.h>
#include <mutex>
#include <fstream>
#include <sstream>
#include <iomanip>
#include <cstdarg>

static std::mutex g_logMutex;
static bool g_logEnabled = true;
static SecsLogLevel g_minLevel = SecsLogLevel::Debug;
static std::string g_logDir = "E:\\kingsemi_log\\fa\\HSMS_Log";

static const char* LevelName(SecsLogLevel level) {
    switch (level) {
        case SecsLogLevel::Debug: return "DEBUG";
        case SecsLogLevel::Info: return "INFO";
        case SecsLogLevel::Warn: return "WARN";
        case SecsLogLevel::Error: return "ERROR";
        default: return "INFO";
    }
}

static std::string DateString() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[16];
    sprintf_s(buf, "%04d-%02d-%02d",st.wYear, st.wMonth, st.wDay);
    return buf;
}

static std::string TimeString() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[32];
    sprintf_s(buf, "%02d:%02d:%02d:%03d", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    return buf;
}

static void EnsureDirectory(const std::string& dir) {
    std::string path = dir;
    for (size_t i = 0; i < path.length(); ++i) {
        if (path[i] == '/' || path[i] == '\\') {
            std::string subPath = path.substr(0, i);
            if (!subPath.empty()) {
                CreateDirectoryA(subPath.c_str(), NULL);
            }
        }
    }
    CreateDirectoryA(dir.c_str(), NULL);
}

static std::string HexDump(const unsigned char* data, int length) {
    std::ostringstream ss;
    ss << std::hex << std::setfill('0');
    for (int i = 0; i < length; ++i) {
        ss << std::setw(2) << static_cast<int>(data[i]) << ' ';
        if ((i + 1) % 16 == 0)
            ss << "\n";
    }
    return ss.str();
}

void SecsLogger::SetEnable(bool enabled) {
    std::lock_guard<std::mutex> lock(g_logMutex);
    g_logEnabled = enabled;
}

void SecsLogger::SetMinLevel(SecsLogLevel level) {
    std::lock_guard<std::mutex> lock(g_logMutex);
    g_minLevel = level;
}

void SecsLogger::SetLogDirectory(const std::string& dir) {
    std::lock_guard<std::mutex> lock(g_logMutex);
    if (!dir.empty())
        g_logDir = dir;
}

void SecsLogger::Debug(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    Write(SecsLogLevel::Debug, fmt, args);
    va_end(args);
}

void SecsLogger::Info(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    Write(SecsLogLevel::Info, fmt, args);
    va_end(args);
}

void SecsLogger::Warn(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    Write(SecsLogLevel::Warn, fmt, args);
    va_end(args);
}

void SecsLogger::Error(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    Write(SecsLogLevel::Error, fmt, args);
    va_end(args);
}

void SecsLogger::Write(SecsLogLevel level, const char* fmt, va_list args) {
    std::lock_guard<std::mutex> lock(g_logMutex);
    if (!g_logEnabled || level < g_minLevel)
        return;
    if (fmt == nullptr)
        return;

    char message[2048];
    _vsnprintf_s(message, sizeof(message), _TRUNCATE, fmt, args);

    EnsureDirectory(g_logDir);
    std::ofstream file(g_logDir + "\\" + DateString() + ".log", std::ios::app);
    if (!file.is_open())
        return;
    file << TimeString()
         << " [" << LevelName(level) << "]"
         //<< " [T" << GetCurrentThreadId() << "] "
         << message << "\n";
}

void SecsLogger::RawSend(const char* channel, const unsigned char* data, int length) {
    if (channel == nullptr) channel = "UNKNOWN";
    WriteRaw("SEND", channel, data, length);
}

void SecsLogger::RawRecv(const char* channel, const unsigned char* data, int length) {
    if (channel == nullptr) channel = "UNKNOWN";
    WriteRaw("RECV", channel, data, length);
}

void SecsLogger::WriteRaw(const char* direction, const char* channel, const unsigned char* data, int length) {
    std::lock_guard<std::mutex> lock(g_logMutex);
    if (!g_logEnabled)
        return;
    if (data == nullptr || length <= 0)
        return;
    
    EnsureDirectory(g_logDir);
    std::ofstream file(g_logDir + "\\" + DateString() + "_SecsI.log", std::ios::app);
    if (!file.is_open())
        return;
    
    file << TimeString()
         << " [RAW-" << direction << "]"
         //<< " [T" << GetCurrentThreadId() << "] "
         << channel << " Length=" << length << "\n"
         << HexDump(data, length) << "\n";
}
