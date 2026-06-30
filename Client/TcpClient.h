#pragma once

#include <cstdint>
#include <deque>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

struct TcpMessage {
    uint8_t type = 0;
    std::vector<uint8_t> payload;
};

class TcpClient {
public:
    using EventCallback = std::function<void(const TcpMessage&)>;

    TcpClient();
    ~TcpClient();

    bool Connect(const std::string& host, uint16_t port);
    void Disconnect();
    bool IsConnected() const;

    bool Send(uint8_t msgType, const uint8_t* payload = nullptr, size_t payloadLen = 0);
    void SetEventCallback(EventCallback cb);

    void PollEvents();

private:
    void RecvLoop();

    class Impl;
    std::unique_ptr<Impl> impl_;
};
