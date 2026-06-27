#include "TcpClient.h"
#include "Protocol.h"
#include <SFML/Network.hpp>
#include <atomic>
#include <iostream>
#include <thread>

class TcpClient::Impl {
public:
    sf::TcpSocket socket;
    std::unique_ptr<std::thread> thread;
    std::atomic<bool> running{false};
    std::mutex sendMutex;
    std::mutex queueMutex;
    std::deque<TcpMessage> queue;
    EventCallback callback;
};

TcpClient::TcpClient() : impl_(new Impl()) {}

TcpClient::~TcpClient() {
    Disconnect();
    delete impl_;
}

bool TcpClient::Connect(const std::string& host, uint16_t port) {
    auto addr = sf::IpAddress::resolve(host);
    if (!addr) {
        std::cerr << "TcpClient: invalid host " << host << '\n';
        return false;
    }
    if (impl_->socket.connect(*addr, port, sf::seconds(5.f)) != sf::Socket::Status::Done) {
        std::cerr << "TcpClient: connect failed\n";
        return false;
    }
    impl_->socket.setBlocking(true);
    impl_->running = true;
    impl_->thread = std::make_unique<std::thread>(&TcpClient::RecvLoop, this);
    return true;
}

void TcpClient::Disconnect() {
    if (!impl_->running) return;
    impl_->running = false;
    impl_->socket.disconnect();
    if (impl_->thread && impl_->thread->joinable()) {
        impl_->thread->join();
    }
    impl_->thread.reset();
}

bool TcpClient::IsConnected() const {
    return impl_->running;
}

bool TcpClient::Send(uint8_t msgType, const uint8_t* payload, size_t payloadLen) {
    if (!impl_->running) return false;
    auto packet = Protocol::PackTcp(msgType, payload, payloadLen);
    std::lock_guard lock(impl_->sendMutex);
    std::size_t sent = 0;
    while (sent < packet.size()) {
        std::size_t chunk = 0;
        if (impl_->socket.send(packet.data() + sent, packet.size() - sent, chunk) != sf::Socket::Status::Done) {
            return false;
        }
        sent += chunk;
    }
    return true;
}

void TcpClient::SetEventCallback(EventCallback cb) {
    impl_->callback = std::move(cb);
}

void TcpClient::PollEvents() {
    std::deque<TcpMessage> local;
    {
        std::lock_guard lock(impl_->queueMutex);
        local.swap(impl_->queue);
    }
    if (!impl_->callback) return;
    for (const auto& msg : local) {
        impl_->callback(msg);
    }
}

void TcpClient::RecvLoop() {
    while (impl_->running) {
        uint8_t header[3]{};
        std::size_t got = 0;
        if (impl_->socket.receive(header, 3, got) != sf::Socket::Status::Done || got != 3) {
            break;
        }
        const uint16_t len = static_cast<uint16_t>(header[0]) | (static_cast<uint16_t>(header[1]) << 8);
        const uint8_t type = header[2];
        const int payloadLen = static_cast<int>(len) - 1;
        TcpMessage msg;
        msg.type = type;
        if (payloadLen > 0) {
            msg.payload.resize(static_cast<size_t>(payloadLen));
            int received = 0;
            while (received < payloadLen) {
                std::size_t chunk = 0;
                if (impl_->socket.receive(msg.payload.data() + received,
                                          static_cast<std::size_t>(payloadLen - received),
                                          chunk) != sf::Socket::Status::Done) {
                    impl_->running = false;
                    return;
                }
                received += static_cast<int>(chunk);
            }
        }
        {
            std::lock_guard lock(impl_->queueMutex);
            impl_->queue.push_back(std::move(msg));
        }
    }
    impl_->running = false;
}
