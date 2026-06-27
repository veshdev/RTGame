#pragma once

#include "Entities.h"
#include "Protocol.h"
#include <SFML/Network.hpp>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

class UdpClient {
public:
    explicit UdpClient(uint8_t playerId);
    ~UdpClient();

    bool Connect(const std::string& host, uint16_t port = Protocol::UdpPort);
    void Disconnect();

    void SendInput(float moveX, float moveY, float angle, uint8_t hotbarSlot, bool fire, bool pickup);
    std::pair<std::shared_ptr<Snapshot>, std::shared_ptr<Snapshot>> SnapshotPair() const;

private:
    void RecvLoop();

    uint8_t playerId_;
    sf::IpAddress serverAddr_{sf::IpAddress::LocalHost};
    uint16_t serverPort_ = 0;
    sf::UdpSocket socket_;
    std::unique_ptr<std::thread> thread_;
    std::atomic<bool> running_{false};
    uint32_t inputTick_ = 0;

    float lastMoveX_ = 0.f;
    float lastMoveY_ = 0.f;
    float lastAngle_ = 0.f;
    uint8_t lastHotbarSlot_ = 0;
    bool lastFire_ = false;
    bool lastPickup_ = false;
    bool hasLastInput_ = false;
    std::chrono::steady_clock::time_point lastSendTime_{};

    mutable std::mutex snapMutex_;
    std::shared_ptr<Snapshot> latest_;
    std::shared_ptr<Snapshot> previous_;
};
