#pragma once
#include "HsmsServer.h"  // For HsmsHeader and HsmsMessage definitions
#include <vector>

class HsmsUtils {
public:
    // Encode HsmsHeader into 10-byte raw data
    static std::vector<unsigned char> PackHeader(const HsmsHeader& h);

    // Overload: extract header from HsmsMessage and call PackHeader(msg.header)
    static std::vector<unsigned char> PackHeader(const HsmsMessage& msg);
};