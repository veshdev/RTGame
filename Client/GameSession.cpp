#include "GameSession.h"
#include "Protocol.h"
#include "AccountStorage.h"
#include "ByteReader.h"
#include <algorithm>
#include <cstring>
#include <span>

bool GameSession::SendAuth(uint8_t msgType, const std::string& host, const std::string& username,
                           const std::string& password) {
    host_ = host;
    username_ = username;
    std::string passToSend = Protocol::IsSha256Hex(password) ? password : Protocol::Sha256Hex(password);
    tcp_.SetEventCallback([this](const TcpMessage& msg) { OnTcpMessage(msg); });
    if (!tcp_.Connect(host_, Protocol::TcpPort)) {
        // clear sensitive data
        std::fill(passToSend.begin(), passToSend.end(), '\0');
        error_ = "Connection failed";
        return false;
    }
    auto packet = Protocol::PackLogin(msgType, username, passToSend);
    bool sent = tcp_.Send(packet[2], packet.data() + 3, packet.size() - 3);
    std::fill(passToSend.begin(), passToSend.end(), '\0');
    return sent;
}

bool GameSession::Login(const std::string& host, const std::string& username, const std::string& password) {
    tcp_.Disconnect();
    return SendAuth(Protocol::TcpMsg::C_LOGIN, host, username, password);
}

bool GameSession::RegisterAccount(const std::string& host, const std::string& username, const std::string& password) {
    tcp_.Disconnect();
    return SendAuth(Protocol::TcpMsg::C_REGISTER, host, username, password);
}

void GameSession::Disconnect() {
    EndGameplaySession();
}

void GameSession::EndGameplaySession(const std::string& message) {
    if (tcp_.IsConnected()) {
        tcp_.Send(Protocol::TcpMsg::C_DISCONNECT);
    }
    tcp_.Disconnect();
    udp_.reset();
    lobby_.reset();
    rooms_.clear();
    hotbar_.clear();
    mapTiles_.clear();
    mapW_ = 0;
    mapH_ = 0;
    mapHash_ = 0;
    mapReady_ = false;
    lobbyReadySent_ = false;
    matchStartSent_ = false;
    playerId_ = 255;
    isHost_ = false;
    loadingProgress_ = 0.f;
    loadingTimer_ = 0.f;
    if (!message.empty())
        error_ = message;
    scenes_.GoTo(Scene::MainMenu);
}

void GameSession::PollNetwork() {
    tcp_.PollEvents();
}

void GameSession::Update(float dt) {
    if (scenes_.Current() == Scene::Loading) {
        loadingTimer_ += dt;
        loadingProgress_ = std::min(1.f, loadingTimer_ / 1.5f);
        if (mapReady_ && loadingProgress_ >= 1.f) {
            scenes_.GoTo(Scene::Playing);
            RequestHotbar();
        }
    }

    if (scenes_.Current() == Scene::Lobby && lobby_) {
        if (!lobbyReadySent_) {
            ToggleReady();
            lobbyReadySent_ = true;
        }
    }
}

std::pair<std::shared_ptr<Snapshot>, std::shared_ptr<Snapshot>> GameSession::Snapshots() const {
    if (!udp_) return {nullptr, nullptr};
    return udp_->SnapshotPair();
}

void GameSession::RequestRoomList() {
    tcp_.Send(Protocol::TcpMsg::C_ROOM_LIST_REQ);
}

void GameSession::CreateRoom(const std::string& name, const std::string& password) {
    std::vector<uint8_t> payload;
    auto n = Protocol::PackString(name, Protocol::RoomNameMax);
    payload.insert(payload.end(), n.begin(), n.end());
    auto p = Protocol::PackString(password, Protocol::RoomPasswordMax);
    payload.insert(payload.end(), p.begin(), p.end());
    tcp_.Send(Protocol::TcpMsg::C_ROOM_CREATE, payload.data(), payload.size());
}

void GameSession::JoinRoom(uint8_t roomId, const std::string& password) {
    joinRoomId_ = roomId;
    std::vector<uint8_t> payload{roomId};
    auto p = Protocol::PackString(password, Protocol::RoomPasswordMax);
    payload.insert(payload.end(), p.begin(), p.end());
    tcp_.Send(Protocol::TcpMsg::C_ROOM_JOIN, payload.data(), payload.size());
}

void GameSession::LeaveRoom() {
    tcp_.Send(Protocol::TcpMsg::C_ROOM_LEAVE);
    lobby_.reset();
    scenes_.GoTo(Scene::RoomList);
    RequestRoomList();
}

void GameSession::ToggleReady() {
    tcp_.Send(Protocol::TcpMsg::C_READY_TOGGLE);
}

void GameSession::StartMatch() {
    tcp_.Send(Protocol::TcpMsg::C_HOST_START);
}

void GameSession::RequestHotbar() {
    tcp_.Send(Protocol::TcpMsg::C_HOTBAR_REQUEST);
}

void GameSession::OnTcpMessage(const TcpMessage& msg) {
    switch (msg.type) {
    case Protocol::TcpMsg::S_LOGIN_ACK:
        if (msg.payload.size() >= 5) {
            playerId_ = msg.payload[0];
            points_ = static_cast<int>(msg.payload[1]) | (static_cast<int>(msg.payload[2]) << 8) |
                      (static_cast<int>(msg.payload[3]) << 16) | (static_cast<int>(msg.payload[4]) << 24);
            AccountStorage::Instance().Remember(username_);
            udp_ = std::make_unique<UdpClient>(playerId_);
            udp_->Connect(host_, Protocol::UdpPort);
            scenes_.GoTo(Scene::RoomList);
            RequestRoomList();
        }
        break;
    case Protocol::TcpMsg::S_LOGIN_REJECT: {
        auto [reason, _] = Protocol::UnpackString(msg.payload.data(), msg.payload.size(), 0);
        error_ = reason.empty() ? "Login rejected" : reason;
        scenes_.GoTo(Scene::Login);
        break;
    }
    case Protocol::TcpMsg::S_ROOM_LIST:
        ParseRoomList(msg.payload);
        if (scenes_.Current() == Scene::RoomList) {
            selectedRoom_ = std::clamp(selectedRoom_, 0, std::max(0, static_cast<int>(rooms_.size()) - 1));
        }
        break;
    case Protocol::TcpMsg::S_ROOM_JOINED:
        if (msg.payload.size() >= 2) {
            isHost_ = msg.payload[1] != 0;
            lobbyReadySent_ = false;
            matchStartSent_ = false;
            scenes_.GoTo(Scene::Lobby);
        }
        break;
    case Protocol::TcpMsg::S_ROOM_LEFT:
        lobby_.reset();
        scenes_.GoTo(Scene::RoomList);
        RequestRoomList();
        break;
    case Protocol::TcpMsg::S_LOBBY_STATE:
        ParseLobby(msg.payload);
        break;
    case Protocol::TcpMsg::S_MATCH_STARTING:
        loadingProgress_ = 0.f;
        loadingTimer_ = 0.f;
        mapReady_ = false;
        scenes_.GoTo(Scene::Loading);
        break;
    case Protocol::TcpMsg::S_MATCH_STARTED:
        ParseMap(msg.payload);
        break;
    case Protocol::TcpMsg::S_HOTBAR_SNAPSHOT:
        ParseHotbar(msg.payload);
        break;
    case Protocol::TcpMsg::S_EXTRACTED:
        if (msg.payload.size() >= 8) {
            const int earned = static_cast<int>(msg.payload[0]) | (static_cast<int>(msg.payload[1]) << 8) |
                               (static_cast<int>(msg.payload[2]) << 16) | (static_cast<int>(msg.payload[3]) << 24);
            points_ = static_cast<int>(msg.payload[4]) | (static_cast<int>(msg.payload[5]) << 8) |
                      (static_cast<int>(msg.payload[6]) << 16) | (static_cast<int>(msg.payload[7]) << 24);
            error_ = "Extracted +" + std::to_string(earned) + " points";
            EndGameplaySession();
        }
        break;
    case Protocol::TcpMsg::S_ERROR: {
        auto [reason, _] = Protocol::UnpackString(msg.payload.data(), msg.payload.size(), 0);
        error_ = reason;
        if (scenes_.Current() == Scene::Loading) {
            mapReady_ = false;
            loadingProgress_ = 0.f;
            loadingTimer_ = 0.f;
            scenes_.GoTo(Scene::Lobby);
        }
        break;
    }
    default:
        break;
    }
}

void GameSession::ParseRoomList(const std::vector<uint8_t>& payload) {
    rooms_.clear();
    if (payload.empty()) return;
    ByteReader reader{std::span<const uint8_t>(payload)};
    uint8_t count = reader.u8();
    for (uint8_t i = 0; i < count; ++i) {
        if (!reader.remaining(4)) break;
        RoomListEntry entry;
        entry.roomId = reader.u8();
        entry.playerCount = reader.u8();
        entry.hasPassword = reader.u8() != 0;
        entry.name = reader.stringPrefixed();
        rooms_.push_back(std::move(entry));
    }
}

void GameSession::ParseLobby(const std::vector<uint8_t>& payload) {
    if (payload.size() < 2) return;
    LobbyState lobby;
    lobby.hostPlayerId = payload[1];
    size_t offset = 2;
    const uint8_t count = payload[0];
    for (uint8_t i = 0; i < count; ++i) {
        if (offset + 3 > payload.size()) break;
        LobbySlot slot;
        slot.playerId = payload[offset++];
        slot.ready = payload[offset++] != 0;
        const uint8_t len = payload[offset++];
        if (offset + len > payload.size()) break;
        slot.username.assign(reinterpret_cast<const char*>(payload.data() + offset), len);
        offset += len;
        lobby.slots.push_back(std::move(slot));
    }
    lobby_ = std::move(lobby);
}

void GameSession::ParseHotbar(const std::vector<uint8_t>& payload) {
    hotbar_.clear();
    if (payload.size() < 1) return;
    for (size_t i = 1; i < payload.size() && hotbar_.size() < Protocol::HotbarSlots; ++i) {
        HotbarSlot slot;
        slot.quantity = payload[i];
        hotbar_.push_back(slot);
    }
    while (hotbar_.size() < Protocol::HotbarSlots) {
        hotbar_.push_back({});
    }
}

void GameSession::ParseMap(const std::vector<uint8_t>& payload) {
    if (payload.size() < 8) return;
    mapHash_ = static_cast<uint32_t>(payload[0]) | (static_cast<uint32_t>(payload[1]) << 8) |
               (static_cast<uint32_t>(payload[2]) << 16) | (static_cast<uint32_t>(payload[3]) << 24);
    mapW_ = static_cast<uint16_t>(payload[4]) | (static_cast<uint16_t>(payload[5]) << 8);
    mapH_ = static_cast<uint16_t>(payload[6]) | (static_cast<uint16_t>(payload[7]) << 8);
    mapTiles_.assign(payload.begin() + 8, payload.end());
    if (mapTiles_.size() < static_cast<size_t>(mapW_) * mapH_) {
        mapTiles_.resize(static_cast<size_t>(mapW_) * mapH_, Protocol::TileType::Floor);
    }
    mapReady_ = true;
}

bool GameSession::AllLobbyReady(const LobbyState& lobby) const {
    if (lobby.slots.empty())
        return false;
    for (const auto& slot : lobby.slots) {
        if (slot.playerId == lobby.hostPlayerId)
            continue;
        if (!slot.ready)
            return false;
    }
    return true;
}
