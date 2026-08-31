#pragma once
#include <vector>
#include <string>

/**
 * @brief SECS-II 数据类型枚举（遵循SEMI E5标准）
 * 
 * Format byte 结构：
 * - Bit 8-7: 长度字节数 (01=1字节, 10=2字节, 11=3字节)
 * - Bit 6-1: 数据类型码
 * 
 * 标准数据类型格式码：
 */
enum class Secs2DataType {
    List = 0x00,        ///< 00000000 - LIST (length in elements)
    Binary = 0x08,      ///< 00010000 - Binary
    Boolean = 0x09,     ///< 00010001 - Boolean
    ASCII = 0x10,       ///< 01000000 - ASCII string
    JIS8 = 0x11,        ///< 01000001 - JIS-8 string
    I8 = 0x30,          ///< 01100000 - 8-byte integer (signed)
    I1 = 0x19,          ///< 01100001 - 1-byte integer (signed)
    I2 = 0x1A,          ///< 01100010 - 2-byte integer (signed)
    I4 = 0x1C,          ///< 01100100 - 4-byte integer (signed)
    F8 = 0x20,          ///< 10000000 - 8-byte floating point
    F4 = 0x24,          ///< 10000100 - 4-byte floating point
    U8 = 0x28,          ///< 10100000 - 8-byte integer (unsigned)
    U1 = 0x29,          ///< 10100001 - 1-byte integer (unsigned)
    U2 = 0x2A,          ///< 10100010 - 2-byte integer (unsigned)
    U4 = 0x2C,          ///< 10100100 - 4-byte integer (unsigned)
};

/**
 * @brief SECS-II 消息项
 */
struct Secs2Item {
    Secs2DataType type;
    std::vector<unsigned char> data;
    std::vector<Secs2Item> children;  
    
    Secs2Item() : type(Secs2DataType::Binary) {}
    
    Secs2Item(Secs2DataType t, const std::vector<unsigned char>& d) 
        : type(t), data(d) {}
};

/**
 * @brief SECS-II 消息结构（仅包含数据部分，不含stream/function/w-bit）
 */
struct Secs2Message {
    Secs2Item root;     
    
    Secs2Message() {}
};

/**
 * @brief SECS-II 编解码器
 */
class Secs2Codec {
public:
    static int Encode(const Secs2Message& msg, std::vector<unsigned char>& buffer);
    static bool Decode(const unsigned char* data, int length, Secs2Message& msg);
    static int EncodeItem(const Secs2Item& item, std::vector<unsigned char>& buffer);
    static bool DecodeItem(const unsigned char*& data, int& length, Secs2Item& item);
};