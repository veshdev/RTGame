#include "SnapshotParser.h"

#include "Protocol.h"

#include <cstring>

namespace SnapshotParser {

namespace {

constexpr int PlayerEntrySize = 19;
constexpr int MonsterEntrySize = 17;
constexpr int ProjectileEntrySize = 16;
constexpr int LootEntrySize = 12;
constexpr int SnapshotHeaderSize = 9;
constexpr float kAngleNormalization = 65535.f;

inline uint16_t ReadUInt16LE(const uint8_t* data) {
    return static_cast<uint16_t>(data[0]) | (static_cast<uint16_t>(data[1]) << 8);
}

inline int16_t ReadInt16LE(const uint8_t* data) {
    return static_cast<int16_t>(ReadUInt16LE(data));
}

} // namespace

std::shared_ptr<Snapshot> Parse(const uint8_t* data, size_t len) {
    if (!data || len < 1 + SnapshotHeaderSize) return nullptr;
    if (data[0] != Protocol::UdpMsg::S_WORLD_SNAPSHOT) return nullptr;

    auto snap = std::make_shared<Snapshot>();

    snap->tickId = static_cast<uint32_t>(data[1]) | (static_cast<uint32_t>(data[2]) << 8) |
                   (static_cast<uint32_t>(data[3]) << 16) | (static_cast<uint32_t>(data[4]) << 24);
    snap->recipientPid = data[5];

    const uint8_t numPlayers = data[6];
    const uint8_t numMonsters = data[7];
    const uint8_t numProjectiles = data[8];
    const uint8_t numLoot = data[9];

    size_t offset = 10;

    for (uint8_t i = 0; i < numPlayers; ++i) {
        if (offset + PlayerEntrySize > len) break;
        if (data[offset]) {
            PlayerEntry e;
            e.id = data[offset + 1];
            e.hp = ReadInt16LE(data + offset + 2);
            std::memcpy(&e.x, data + offset + 4, 4);
            std::memcpy(&e.y, data + offset + 8, 4);
            e.angle = static_cast<float>(ReadUInt16LE(data + offset + 12)) / kAngleNormalization;
            e.hotbarSlot = data[offset + 14];
            e.alive = data[offset + 15] != 0;
            e.kills = data[offset + 16];
            e.carriedLoot = data[offset + 17];
            e.extractionProgress = data[offset + 18];
            snap->players.push_back(e);
        }
        offset += PlayerEntrySize;
    }

    for (uint8_t i = 0; i < numMonsters; ++i) {
        if (offset + MonsterEntrySize > len) break;
        if (data[offset]) {
            MonsterEntry e;
            e.id = data[offset + 1];
            e.type = data[offset + 2];
            e.weapon = data[offset + 3];
            e.hp = ReadInt16LE(data + offset + 4);
            std::memcpy(&e.x, data + offset + 6, 4);
            std::memcpy(&e.y, data + offset + 10, 4);
            e.angle = static_cast<float>(ReadUInt16LE(data + offset + 14)) / kAngleNormalization;
            e.state = data[offset + 16];
            snap->monsters.push_back(e);
        }
        offset += MonsterEntrySize;
    }

    for (uint8_t i = 0; i < numProjectiles; ++i) {
        if (offset + ProjectileEntrySize > len) break;
        if (data[offset]) {
            ProjectileEntry e;
            e.id = data[offset + 1];
            e.owner = data[offset + 2];
            e.weapon = data[offset + 3];
            std::memcpy(&e.x, data + offset + 4, 4);
            std::memcpy(&e.y, data + offset + 8, 4);
            e.angle = static_cast<float>(ReadUInt16LE(data + offset + 12)) / kAngleNormalization;
            e.speed = static_cast<float>(ReadUInt16LE(data + offset + 14));
            snap->projectiles.push_back(e);
        }
        offset += ProjectileEntrySize;
    }

    for (uint8_t i = 0; i < numLoot; ++i) {
        if (offset + LootEntrySize > len) break;
        if (data[offset]) {
            LootEntry e;
            e.id = data[offset + 1];
            e.item = data[offset + 2];
            e.quantity = data[offset + 3];
            std::memcpy(&e.x, data + offset + 4, 4);
            std::memcpy(&e.y, data + offset + 8, 4);
            snap->loot.push_back(e);
        }
        offset += LootEntrySize;
    }

    return snap;
}

} // namespace SnapshotParser

