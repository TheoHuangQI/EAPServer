#include "pch.h"
#include "ErrorManager.h"
#include "Secs2Message.h"
#include "HsmsUtils.h"
#include "SecsLite.h"
#include "SecsLogger.h"

ErrorManager::ErrorManager(HsmsServer* pServer, unsigned short localDeviceId, SecsLite* pSecsLite)
	: m_pServer(pServer), m_localDeviceId(localDeviceId), m_pSecsLite(pSecsLite) {
}

HsmsHeader ErrorManager::MakeErrorHeader(uint8_t func, uint32_t sys) const {
	HsmsHeader h{};
	h.deviceId = m_localDeviceId;   // 使用成员变量中的设备 ID
	h.stream = 9;
	h.function = func;
	h.wbit = false;
	h.pType = 0;
	h.sType = HsmsMessageType::DataMessage;
	h.systemBytes = sys;
	return h;
}

std::vector<unsigned char> ErrorManager::EncodeBinaryData(const std::vector<unsigned char>& rawData) {
	if (rawData.size() != 10)
		return {};
	Secs2Item binItem(Secs2DataType::Binary, rawData);
	Secs2Message secsMsg;
	secsMsg.root = binItem;
	std::vector<unsigned char> encoded;
	Secs2Codec::Encode(secsMsg, encoded);
	return encoded;
}

bool ErrorManager::SendError(int func, const std::vector<unsigned char>& oriheader, unsigned int systemByte) {
	if (!m_pServer)
	{
		SecsLogger::Error("SendError S9F%d failed: server is null", func);
		return false;
	}
	if (oriheader.size() != 10)
	{
		SecsLogger::Error("SendError S9F%d failed: original header size=%d", func, (int)oriheader.size());
		return false;
	}

	std::vector<unsigned char> body = EncodeBinaryData(oriheader);
	if (body.empty())
		return false;

	// unsigned int nextSysByte = m_pSecsLite->GetNextMsgId();
	HsmsMessage msg;
	msg.header = MakeErrorHeader(static_cast<uint8_t> (func), systemByte);

	msg.data = new unsigned char[body.size()];
	memcpy_s(msg.data, body.size(), body.data(), body.size());
	msg.dataLength = (int)body.size();

	int sendResult = m_pServer->SendHsmsMessage(msg);
	HsmsServer::FreeMessageData(msg);
	if (sendResult > 0) 
	{
		SecsLogger::Warn("S9F%d sent, MsgId=%u", func, systemByte);
		return true;
	}

	SecsLogger::Error("S9F%d send failed. MsgId=%u sendResult=%d", func, systemByte, sendResult);
	return false;
}

bool ErrorManager::SendError(int func, const HsmsMessage& oriMsg) {
	std::vector<unsigned char> header = HsmsUtils::PackHeader(oriMsg);
	return SendError(func, header, oriMsg.header.systemBytes);
}