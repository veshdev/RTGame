#pragma once



#include <cstdint>

#include <string>

#include <vector>



struct PlayerEntry {

    uint8_t id = 0;

    int16_t hp = 0;

    float x = 0.f;

    float y = 0.f;

    float angle = 0.f;

    uint8_t hotbarSlot = 0;

    bool alive = false;

    uint8_t kills = 0;

    uint8_t carriedLoot = 0;

    uint8_t extractionProgress = 0;

};



struct MonsterEntry {

    uint8_t id = 0;

    uint8_t type = 0;

    uint8_t weapon = 0;

    int16_t hp = 0;

    float x = 0.f;

    float y = 0.f;

    float angle = 0.f;

    uint8_t state = 0;

};



struct ProjectileEntry {

    uint8_t id = 0;

    uint8_t owner = 0;

    uint8_t weapon = 0;

    float x = 0.f;

    float y = 0.f;

    float angle = 0.f;

    float speed = 0.f;

};



struct LootEntry {

    uint8_t id = 0;

    uint8_t item = 0;

    uint8_t quantity = 0;

    float x = 0.f;

    float y = 0.f;

};



struct Snapshot {

    uint32_t tickId = 0;

    uint8_t recipientPid = 255;

    std::vector<PlayerEntry> players;

    std::vector<MonsterEntry> monsters;

    std::vector<ProjectileEntry> projectiles;

    std::vector<LootEntry> loot;

};



struct RoomListEntry {

    uint8_t roomId = 0;

    uint8_t playerCount = 0;

    bool hasPassword = false;

    std::string name;

};



struct LobbySlot {

    uint8_t playerId = 0;

    bool ready = false;

    std::string username;

};



struct LobbyState {

    uint8_t hostPlayerId = 0;

    std::vector<LobbySlot> slots;

};



struct HotbarSlot {

    uint8_t quantity = 0;

};

