#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>

typedef UINT_PTR SOCKET;
#define INVALID_SOCKET (SOCKET)(~0)
#define SOCKET_ERROR (-1)
#define AF_INET 2
#define SOCK_STREAM 1
#define IPPROTO_TCP 6

struct sockaddr_in {
    short sin_family;
    unsigned short sin_port;
    union {
        unsigned char s_b4[4];
        unsigned long s_addr;
    } sin_addr;
    char sin_zero[8];
};

typedef struct WSAData {
    WORD wVersion;
    WORD wHighVersion;
    char szDescription[257];
    char szSystemStatus[129];
    unsigned short iMaxSockets;
    unsigned short iMaxUdpDg;
    char* lpVendorInfo;
} WSADATA;

extern "C" {
    int __stdcall WSAStartup(WORD, WSADATA*);
    int __stdcall WSACleanup();
    SOCKET __stdcall socket(int, int, int);
    int __stdcall closesocket(SOCKET);
    int __stdcall bind(SOCKET, const struct sockaddr*, int);
    int __stdcall listen(SOCKET, int);
    SOCKET __stdcall accept(SOCKET, struct sockaddr*, int*);
    int __stdcall send(SOCKET, const char*, int, int);
    int __stdcall recv(SOCKET, char*, int, int);
    unsigned short __stdcall htons(unsigned short);
    int __stdcall inet_pton(int, const char*, void*);
}

#pragma comment(lib, "ws2_32.lib")

#include <iostream>
#include <vector>
#include <string>
#include <queue>
#include <mutex>
#include <condition_variable>
#include <thread>
#include <atomic>
#include <iomanip>
#include <sstream>
#include <fstream>

using namespace std;

string GetFullTime() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[100];
    sprintf_s(buf, "[%04d-%02d-%02d %02d:%02d:%02d,%03d]",
        st.wYear, st.wMonth, st.wDay,
        st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    return buf;
}

string GetDateStr() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[20];
    sprintf_s(buf, "%04d-%02d-%02d", st.wYear, st.wMonth, st.wDay);
    return buf;
}

string BytesToHex(const unsigned char* d, int l) {
    stringstream ss;
    for (int i = 0; i < l; i++)
        ss << uppercase << hex << setw(2) << setfill('0') << (int)d[i] << " ";
    string s = ss.str();
    if (!s.empty()) s.pop_back();
    return s;
}

bool CreateDir(const string& path) {
    bool ok = CreateDirectoryA(path.c_str(), NULL) || (GetLastError() == ERROR_ALREADY_EXISTS);
    cout << "[DIR] " << path << (ok ? " OK" : " FAILED") << endl;
    return ok;
}

void CreateLogDirs() {
    CreateDir("SECS_LOG");
    CreateDir("SECS_LOG\\SECS_I");
    CreateDir("SECS_LOG\\SECS_II");
}

void WriteSecs1Log(const string& log) {
    static mutex mtx;
    lock_guard<mutex> lk(mtx);
    string file = "SECS_LOG\\SECS_I\\" + GetDateStr() + ".txt";
    ofstream f(file, ios::app);
    if (f) {
        f << log << endl;
    }
    else {
        cout << "[ERROR] 无法写入: " << file << endl;
    }
}

void WriteSecs2Log(const string& log) {
    static mutex mtx;
    lock_guard<mutex> lk(mtx);
    string file = "SECS_LOG\\SECS_II\\" + GetDateStr() + ".txt";
    ofstream f(file, ios::app);
    if (f) {
        f << log << endl;
    }
    else {
        cout << "[ERROR] 无法写入: " << file << endl;
    }
}

struct SecsNode {
    string type;
    int len = 0;
    string val;
    vector<SecsNode> ch;
};

SecsNode ParseSecs(const vector<unsigned char>& d, int& p) {
    SecsNode n;
    if (p >= d.size()) return n;
    unsigned char head = d[p];
    int fmt = (head >> 2) & 0x3F;
    int nLen = head & 3;
    int len = 0;
    if (p + 1 + nLen > d.size()) { p = d.size(); return n; }
    for (int i = 0; i < nLen; i++) len = (len << 8) | d[p + 1 + i];
    p += 1 + nLen;
    n.len = len;

    if (fmt == 0) {
        n.type = "L";
        for (int i = 0; i < len && p < d.size(); i++)
            n.ch.push_back(ParseSecs(d, p));
    }
    else {
        if (p + len > d.size()) len = d.size() - p;
        if (fmt == 8) { n.type = "B"; n.val = BytesToHex(d.data() + p, len); }
        else if (fmt == 16) { n.type = "A"; n.val = string((char*)d.data() + p, len); }
        else if (fmt == 25) { n.type = "I1"; n.val = to_string((char)d[p]); }
        else if (fmt == 26 && len >= 2) { n.type = "I2"; n.val = to_string((short)(d[p] << 8 | d[p + 1])); }
        else if (fmt == 28 && len >= 4) { n.type = "I4"; n.val = to_string((int)(d[p] << 24 | d[p + 1] << 16 | d[p + 2] << 8 | d[p + 3])); }
        else if (fmt == 41) { n.type = "U1"; n.val = to_string(d[p]); }
        else if (fmt == 42 && len >= 2) { n.type = "U2"; n.val = to_string((unsigned short)(d[p] << 8 | d[p + 1])); }
        else if (fmt == 44 && len >= 4) { n.type = "U4"; n.val = to_string((unsigned int)(d[p] << 24 | d[p + 1] << 16 | d[p + 2] << 8 | d[p + 3])); }
        else { n.type = "F"; }
        p += len;
    }
    return n;
}

void PrintSecs(const SecsNode& n, int ind, stringstream& ss) {
    string pad(ind * 4, ' ');
    if (n.type == "L") {
        ss << pad << "<L[" << n.len << "]\n";
        for (const auto& c : n.ch) PrintSecs(c, ind + 1, ss);
        ss << pad << ">\n";
    }
    else if (n.type == "A") {
        ss << pad << "<" << n.type << "[" << n.len << "] \"" << n.val << "\">\n";
    }
    else {
        ss << pad << "<" << n.type << "[" << n.len << "] " << n.val << ">\n";
    }
}

queue<string> g_sendQ;
mutex g_qMtx;
condition_variable g_qCv;
atomic<bool> g_connected = false;
SOCKET g_sock = INVALID_SOCKET;

string g_ip = "127.0.0.1";
int g_port = 5000;

void SendThread() {
    while (g_connected) {
        unique_lock<mutex> l(g_qMtx);
        if (g_sendQ.empty()) {
            g_qCv.wait_for(l, chrono::milliseconds(100));
            continue;
        }
        string hex = g_sendQ.front();
        g_sendQ.pop();
        l.unlock();

        vector<unsigned char> buf;
        stringstream ss(hex);
        string b;
        while (ss >> b) buf.push_back((unsigned char)stoi(b, nullptr, 16));

        if (!buf.empty() && g_sock != INVALID_SOCKET) {
            send(g_sock, (char*)buf.data(), (int)buf.size(), 0);
            string sendLog = GetFullTime() + " [SEND] " + hex;
            cout << sendLog << endl;
            WriteSecs1Log(sendLog);
        }
    }
}

void RecvThread() {
    vector<unsigned char> buf(4096);
    while (g_connected && g_sock != INVALID_SOCKET) {
        int r = recv(g_sock, (char*)buf.data(), (int)buf.size(), 0);
        if (r <= 0) break;

        string hex = BytesToHex(buf.data(), r);
        string t = GetFullTime();
        string secs1 = t + " [RECV] " + hex;
        cout << secs1 << endl;
        WriteSecs1Log(secs1);
        if (r == 14 && buf[8] == 0 && buf[9] != 0) {
            unsigned char s1 = buf[4], s2 = buf[5], st = buf[9];
            unsigned char y1 = buf[10], y2 = buf[11], y3 = buf[12], y4 = buf[13];
            char ses[10], sys[30];
            sprintf_s(ses, "%02X %02X", s1, s2);
            sprintf_s(sys, "%02X %02X %02X %02X", y1, y2, y3, y4);
            string rsp;
            if (st == 0x01) rsp = "00 00 00 0A " + string(ses) + " 00 00 00 02 " + string(sys);
            if (st == 0x03) rsp = "00 00 00 0A " + string(ses) + " 00 00 00 04 " + string(sys);
            if (st == 0x05) rsp = "00 00 00 0A " + string(ses) + " 00 00 00 06 " + string(sys);
            if (st == 0x09) rsp = "00 00 00 0A " + string(ses) + " 00 00 00 0A " + string(sys);

            if (!rsp.empty()) {
                lock_guard<mutex> l(g_qMtx);
                g_sendQ.push(rsp);
                g_qCv.notify_one();
            }
            continue;
        }
        if (r >= 14 && buf[8] == 0 && buf[9] == 0) {
            int device = (buf[4] << 8) | buf[5];

            // 4字节完整SystemBytes（不报错、不崩溃）
            uint32_t systemByte = ((uint32_t)buf[10] << 24) | ((uint32_t)buf[11] << 16) | ((uint32_t)buf[12] << 8) | (uint32_t)buf[13];

            int stream = buf[6] & 0x7F;
            int func = buf[7];
            bool wbit = (buf[6] & 0x80) != 0;

            stringstream ss;
            ss << GetFullTime() << " [RECV][SystemByte=" << systemByte << ", DeviceId=" << device << ", WaitBit=" << wbit << "]" << endl;
            ss << "'S" << stream << "F" << func << "_MSG': 'S" << stream << "F" << func << "' W=" << wbit << endl;
            if (r > 14) {
                vector<unsigned char> d(buf.begin() + 14, buf.end());
                int p = 0;
                SecsNode root = ParseSecs(d, p);
                PrintSecs(root, 1, ss);
            }

            string secs2Log = ss.str();
            cout << secs2Log;
            WriteSecs2Log(secs2Log);
        }
    }
    g_connected = false;
}void Start() {
    WSADATA w;
    WSAStartup(MAKEWORD(2, 2), &w);
    CreateLogDirs();
    cout << "=== HSMS/SECS C++ 服务端 ===" << endl;

    while (true) {
        SOCKET ls = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        sockaddr_in addr{ AF_INET, htons((unsigned short)g_port) };
        inet_pton(AF_INET, g_ip.c_str(), &addr.sin_addr);

        if (bind(ls, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
            closesocket(ls);
            Sleep(3000);
            continue;
        }

        listen(ls, 1);
        cout << "[" << GetFullTime() << "] 监听: " << g_ip << ":" << g_port << endl;

        g_sock = accept(ls, NULL, NULL);
        if (g_sock == INVALID_SOCKET) { closesocket(ls); continue; }

        cout << "[" << GetFullTime() << "] 客户端已连接" << endl;
        g_connected = true;
        thread(SendThread).detach();
        thread(RecvThread).detach();

        while (g_connected) Sleep(500);
        closesocket(g_sock);
        closesocket(ls);
        Sleep(1000);
    }
}

int main() {
    try { Start(); }
    catch (...) { cout << "异常" << endl; }
    WSACleanup();
    return 0;
}