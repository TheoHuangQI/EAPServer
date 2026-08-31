#pragma once
#include <string>
#include <cstdarg>

enum class SecsLogLevel {
    Debug = 0,
    Info,
    Warn,
    Error
};

class SecsLogger {
public:
    static void SetEnable(bool enabled);
    static void SetMinLevel(SecsLogLevel level);
    static void SetLogDirectory(const std::string& dir);

    static void Debug(const char* fmt, ...);
    static void Info(const char* fmt, ...);
    static void Warn(const char* fmt, ...);
    static void Error(const char* fmt, ...);

    static void RawSend(const char* channel, const unsigned char* data, int lenth);
    static void RawRecv(const char* channel, const unsigned char* data, int lenth);

private:
    static void Write(SecsLogLevel level, const char* fmt, va_list args);
    static void WriteRaw(const char* direction, const char* channel, const unsigned char* data, int length);
};