#pragma once

#include "Entities.h"
#include <algorithm>
#include <cmath>
#include <optional>

namespace Interpolation {

inline float Lerp(float a, float b, float t) {
    return a + (b - a) * t;
}

inline float LerpAngle(float a, float b, float t) {
    float diff = b - a;
    if (diff > 0.5f) diff -= 1.f;
    if (diff < -0.5f) diff += 1.f;
    float result = a + diff * t;
    if (result < 0.f) result += 1.f;
    if (result >= 1.f) result -= 1.f;
    return result;
}

inline Snapshot Blend(const Snapshot& from, const Snapshot& to, float alpha, uint8_t localPid) {
    Snapshot out = to;
    out.tickId = to.tickId;

    for (auto& player : out.players) {
        if (player.id == localPid) continue;
        const PlayerEntry* prev = nullptr;
        for (const auto& p : from.players) {
            if (p.id == player.id) {
                prev = &p;
                break;
            }
        }
        if (!prev) continue;
        player.x = Lerp(prev->x, player.x, alpha);
        player.y = Lerp(prev->y, player.y, alpha);
        player.angle = LerpAngle(prev->angle, player.angle, alpha);
    }

    for (auto& monster : out.monsters) {
        const MonsterEntry* prev = nullptr;
        for (const auto& m : from.monsters) {
            if (m.id == monster.id) {
                prev = &m;
                break;
            }
        }
        if (!prev) continue;
        monster.x = Lerp(prev->x, monster.x, alpha);
        monster.y = Lerp(prev->y, monster.y, alpha);
        monster.angle = LerpAngle(prev->angle, monster.angle, alpha);
    }

    return out;
}

inline std::optional<PlayerEntry> FindLocalPlayer(const Snapshot& snap, uint8_t pid) {
    for (const auto& p : snap.players) {
        if (p.id == pid) return p;
    }
    return std::nullopt;
}

} // namespace Interpolation
