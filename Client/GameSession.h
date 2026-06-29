#pragma once



#include "Entities.h"

#include "Protocol.h"

#include "TcpClient.h"

#include "UdpClient.h"

#include "SceneManager.h"

#include <cstdint>

#include <memory>

#include <optional>

#include <string>

#include <vector>



class GameSession {

public:

    SceneManager& Scenes() { return scenes_; }

    const SceneManager& Scenes() const { return scenes_; }



    bool Login(const std::string& host, const std::string& username, const std::string& password);

    bool RegisterAccount(const std::string& host, const std::string& username, const std::string& password);

    void Disconnect();
    void EndGameplaySession(const std::string& message = {});

    void Update(float dt);

    void PollNetwork();



    TcpClient& Tcp() { return tcp_; }

    UdpClient* Udp() { return udp_.get(); }



    uint8_t PlayerId() const { return playerId_; }

    bool IsHost() const { return isHost_; }

    int Points() const { return points_; }

    const std::string& Host() const { return host_; }

    const std::string& Username() const { return username_; }

    const std::string& Error() const { return error_; }

    void ClearError() { error_.clear(); }



    const std::vector<RoomListEntry>& Rooms() const { return rooms_; }

    int SelectedRoom() const { return selectedRoom_; }

    void SelectRoom(int idx) { selectedRoom_ = idx; }



    const std::optional<LobbyState>& Lobby() const { return lobby_; }

    const std::vector<HotbarSlot>& Hotbar() const { return hotbar_; }



    const std::vector<uint8_t>& MapTiles() const { return mapTiles_; }
    uint32_t MapHash() const { return mapHash_; }
    unsigned MapWidth() const { return mapW_; }
    unsigned MapHeight() const { return mapH_; }
    bool MapReady() const { return mapReady_; }



    std::pair<std::shared_ptr<Snapshot>, std::shared_ptr<Snapshot>> Snapshots() const;



    void RequestRoomList();

    void CreateRoom(const std::string& name, const std::string& password);

    void JoinRoom(uint8_t roomId, const std::string& password);

    void LeaveRoom();

    void ToggleReady();

    void StartMatch();

    void RequestHotbar();

    bool AllLobbyReady(const LobbyState& lobby) const;

    float LoadingProgress() const { return loadingProgress_; }



private:

    void OnTcpMessage(const TcpMessage& msg);

    void ParseRoomList(const std::vector<uint8_t>& payload);

    void ParseLobby(const std::vector<uint8_t>& payload);

    void ParseHotbar(const std::vector<uint8_t>& payload);

    void ParseMap(const std::vector<uint8_t>& payload);

    bool SendAuth(uint8_t msgType, const std::string& host, const std::string& username,
                  const std::string& password);



    SceneManager scenes_;

    TcpClient tcp_;

    std::unique_ptr<UdpClient> udp_;

    std::string host_;

    std::string username_;

    std::string error_;

    uint8_t playerId_ = 255;

    bool isHost_ = false;

    int points_ = 0;

    int selectedRoom_ = 0;

    uint8_t joinRoomId_ = 0;



    std::vector<RoomListEntry> rooms_;

    std::optional<LobbyState> lobby_;

    std::vector<HotbarSlot> hotbar_;



    std::vector<uint8_t> mapTiles_;

    uint32_t mapHash_ = 0;
    unsigned mapW_ = 0;
    unsigned mapH_ = 0;

    bool mapReady_ = false;
    bool lobbyReadySent_ = false;
    bool matchStartSent_ = false;

    float loadingProgress_ = 0.f;

    float loadingTimer_ = 0.f;

};

