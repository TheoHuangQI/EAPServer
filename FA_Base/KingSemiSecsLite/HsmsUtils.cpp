#include "pch.h"
#include "HsmsUtils.h"

std::vector<unsigned char> HsmsUtils::PackHeader(const HsmsHeader& h) {
    std::vector<unsigned char> raw(10);
    raw[0] = (h.deviceId >> 8) & 0xFF;
    raw[1] = h.deviceId & 0xFF;
    if (h.sType == HsmsMessageType::DataMessage) {
        raw[2] = (h.wbit ? 0x80 : 0x00) | (h.stream & 0x7F);
        raw[3] = h.function;
    }
    else {
        raw[2] = 0x00;
        raw[3] = 0x00;
    }
    raw[4] = h.pType;
    raw[5] = static_cast<unsigned char>(h.sType);
    raw[6] = (h.systemBytes >> 24) & 0xFF;
    raw[7] = (h.systemBytes >> 16) & 0xFF;
    raw[8] = (h.systemBytes >> 8) & 0xFF;
    raw[9] = h.systemBytes & 0xFF;
    return raw;
}

std::vector<unsigned char> HsmsUtils::PackHeader(const HsmsMessage& msg) {
    return PackHeader(msg.header);
}

