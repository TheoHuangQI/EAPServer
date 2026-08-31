#pragma once
#include "HsmsServer.h"
#include <vector>
#include <mutex>

class SecsLite;
class ErrorManager {
public:
	ErrorManager(HsmsServer* pServer, unsigned short localDeviceId, SecsLite* pSecsLite);
	~ErrorManager() = default;

	bool SendError(int func, const std::vector<unsigned char>& oriheader, unsigned int systemByte);
	bool SendError(int func, const HsmsMessage& oriMsg);

private:
	std::vector<unsigned char> EncodeBinaryData(const std::vector<unsigned char>& rawData);
	HsmsHeader MakeErrorHeader(uint8_t func, uint32_t sys) const;

	HsmsServer* m_pServer;
	SecsLite* m_pSecsLite;
	unsigned short m_localDeviceId;
	std::mutex m_mutex;
};