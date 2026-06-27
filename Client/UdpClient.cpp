#include "UdpClient.h"
#include "SnapshotParser.h"
#include "Protocol.h"
#include <chrono>
#include <cstring>
#include <thread>

UdpClient::UdpClient(uint8_t playerId) : playerId_(playerId) {}

UdpClient::~UdpClient() {
    Disconnect();
}

bool UdpClient::Connect(const std::string& host, uint16_t port) {
    auto addr = sf::IpAddress::resolve(host);
    if (!addr)
        return false;
    serverAddr_ = *addr;
    serverPort_ = port;
    socket_.setBlocking(false);
    if (socket_.bind(sf::Socket::AnyPort) != sf::Socket::Status::Done)
        return false;
    running_ = true;
    hasLastInput_ = false;
    thread_ = std::make_unique<std::thread>(&UdpClient::RecvLoop, this);
    return true;
}

void UdpClient::Disconnect() {
    running_ = false;
    if (thread_ && thread_->joinable())
        thread_->join();
    thread_.reset();
    socket_.unbind();
    hasLastInput_ = false;
}

void UdpClient::SendInput(float moveX, float moveY, float angle, uint8_t hotbarSlot, bool fire,
                          bool pickup) {
    if (!running_)
        return;

    const auto now = std::chrono::steady_clock::now();
    const bool unchanged = hasLastInput_ && lastMoveX_ == moveX && lastMoveY_ == moveY &&
                           lastAngle_ == angle && lastHotbarSlot_ == hotbarSlot && lastFire_ == fire &&
                           lastPickup_ == pickup;
    const auto elapsed =
        std::chrono::duration<float>(now - lastSendTime_).count();
    if (unchanged && elapsed < Protocol::TickDt)
        return;

    ++inputTick_;
    auto packet = Protocol::PackUdpInput(inputTick_, moveX, moveY, angle, hotbarSlot, fire, pickup);
    packet[1] = playerId_;
    (void)socket_.send(packet.data(), packet.size(), serverAddr_, serverPort_);

    lastMoveX_ = moveX;
    lastMoveY_ = moveY;
    lastAngle_ = angle;
    lastHotbarSlot_ = hotbarSlot;
    lastFire_ = fire;
    lastPickup_ = pickup;
    hasLastInput_ = true;
    lastSendTime_ = now;
}

std::pair<std::shared_ptr<Snapshot>, std::shared_ptr<Snapshot>> UdpClient::SnapshotPair() const {
    std::lock_guard lock(snapMutex_);
    return {previous_, latest_};
}

void UdpClient::RecvLoop() {
    uint8_t buffer[4096];
    while (running_) {
        std::size_t received = 0;
        std::optional<sf::IpAddress> sender;
        unsigned short senderPort = 0;
        const auto status = socket_.receive(buffer, sizeof(buffer), received, sender, senderPort);
        if (status == sf::Socket::Status::Done && received > 0) {
            auto snap = SnapshotParser::Parse(buffer, received);
            if (snap && snap->recipientPid == playerId_) {
                std::lock_guard lock(snapMutex_);
                if (!latest_ || snap->tickId >= latest_->tickId) {
                    previous_ = latest_;
                    latest_ = std::move(snap);
                }
            }
        } else {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }
}
