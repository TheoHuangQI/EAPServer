#include "pch.h"
#include "Secs2Message.h"

int Secs2Codec::Encode(const Secs2Message& msg, std::vector<unsigned char>& buffer) {
    buffer.clear();
    if (!msg.root.data.empty() || !msg.root.children.empty()) {
        EncodeItem(msg.root, buffer);
    }
    return buffer.size();
}

bool Secs2Codec::Decode(const unsigned char* data, int length, Secs2Message& msg) {
    if (data == nullptr || length < 1) // 至少需要一个字节的Format Byte
        return false;
    bool ok = DecodeItem(data, length, msg.root); 
    return ok && length == 0;
}

int Secs2Codec::EncodeItem(const Secs2Item& item, std::vector<unsigned char>& buffer) {
    int startPos = buffer.size();
    int dataSize = 0;
    if (item.type == Secs2DataType::List) {
        dataSize = (int)item.children.size();   // List 的长度是子项个数
    }
    else {
        dataSize = (int)item.data.size();
    }
    unsigned char typeCode = static_cast<unsigned char>(item.type); // 格式码

    // 确定长度字节数编码（低2位）和长度字段的字节数
    unsigned char lenCode = 0;
    if (dataSize <= 0xFF) {
        lenCode = 0x01;   // 1字节长度
    }
    else if (dataSize <= 0xFFFF) {
        lenCode = 0x02;   // 2字节长度
    }
    else if (dataSize <= 0xFFFFFF) {
        lenCode = 0x03;   // 3字节长度
    }
    else {
        return -1; // 不支持更大长度
    }

    // 构造第一个字节：高6位 = 格式码，低2位 = 长度字节数编码
    buffer.push_back((typeCode << 2) | lenCode);

    // 写入长度字段（大端序）
    if (lenCode == 0x01) {
        buffer.push_back(static_cast<unsigned char>(dataSize));
    }
    else if (lenCode == 0x02) {
        buffer.push_back(static_cast<unsigned char>((dataSize >> 8) & 0xFF));
        buffer.push_back(static_cast<unsigned char>(dataSize & 0xFF));
    }
    else if (lenCode == 0x03) {
        buffer.push_back(static_cast<unsigned char>((dataSize >> 16) & 0xFF));
        buffer.push_back(static_cast<unsigned char>((dataSize >> 8) & 0xFF));
        buffer.push_back(static_cast<unsigned char>(dataSize & 0xFF));
    }

    // 写入数据体
    if (item.type == Secs2DataType::List) {
        for (const auto& child : item.children) {
            EncodeItem(child, buffer);
        }
    }
    else {
        buffer.insert(buffer.end(), item.data.begin(), item.data.end());
    }

    return buffer.size() - startPos;
}

bool Secs2Codec::DecodeItem(const unsigned char*& data, int& length, Secs2Item& item) {
    item.data.clear();
    item.children.clear();
    
    if (length < 1) return false;

    // 读Format Byte & length code
    unsigned char firstByte = *data;
    unsigned char typeCode = (firstByte >> 2) & 0x3F;   
    item.type = static_cast<Secs2DataType>(typeCode);
    unsigned char lenCode = firstByte & 0x03;           
    if (lenCode == 0) 
        return false; 
    data++; length--;

    // 读长度字段
    int dataLength = 0;
    if (lenCode == 1) {
        if (length < 1)
            return false;
        dataLength = data[0];
        data++;
        length--;
    }
    else if (lenCode == 2) {
        if (length < 2) 
            return false;
        dataLength = (data[0] << 8) | data[1];
        data += 2; length -= 2;
    }
    else if (lenCode == 3) {
        if (length < 3) 
            return false;
        dataLength = (data[0] << 16) | (data[1] << 8) | data[2];
        data += 3; length -= 3;
    }

    // 读取 data body
    if (item.type == Secs2DataType::List) {
        for (int i = 0; i < dataLength; i++) {
            Secs2Item child;
            if (!DecodeItem(data, length, child)) {
                return false;
            }
            item.children.push_back(std::move(child));
        }
    }
    else {
        if (length < dataLength) {
            return false;
        }
        item.data.assign(data, data + dataLength);
        data += dataLength;
        length -= dataLength;
    }

    return true;
}