#include "Protocol.h"

#include <algorithm>

#include <cmath>

#include <cstring>

#include <hash-library/sha256.h>



namespace Protocol {



const char* HotbarSlotName(uint8_t slot) {

    switch (slot) {

    case HotbarSlot::Axe: return "Axe";

    case HotbarSlot::Pistol: return "Pistol";

    case HotbarSlot::Rifle: return "Rifle";

    case HotbarSlot::Shotgun: return "Shotgun";

    case HotbarSlot::PistolMag: return "Pistol Mag";

    case HotbarSlot::RifleMag: return "Rifle Mag";

    case HotbarSlot::ShotgunAmmo: return "Shotgun Ammo";

    case HotbarSlot::Medkit: return "Medkit";

    case HotbarSlot::Loot: return "Loot";

    default: return "Empty";

    }

}



const char* ItemName(uint8_t type) {

    switch (type) {

    case ItemType::Axe: return "Axe";

    case ItemType::Pistol: return "Pistol";

    case ItemType::Rifle: return "Rifle";

    case ItemType::Shotgun: return "Shotgun";

    case ItemType::Medkit: return "Med-Kit";

    case ItemType::AmmoPistol: return "Pistol Ammo";

    case ItemType::AmmoRifle: return "Rifle Ammo";

    case ItemType::AmmoShotgun: return "Shotgun Ammo";

    case ItemType::Loot: return "Loot";

    default: return "Empty";

    }

}



bool IsWeaponSlot(uint8_t slot) {

    return slot <= HotbarSlot::Shotgun;

}



std::vector<uint8_t> PackTcp(uint8_t msgType, const uint8_t* payload, size_t payloadLen) {

    const uint16_t total = static_cast<uint16_t>(1 + payloadLen);

    std::vector<uint8_t> out(TcpHeaderSize + payloadLen);

    out[0] = static_cast<uint8_t>(total & 0xFF);

    out[1] = static_cast<uint8_t>((total >> 8) & 0xFF);

    out[2] = msgType;

    if (payload && payloadLen > 0) {

        std::memcpy(out.data() + 3, payload, payloadLen);

    }

    return out;

}



std::vector<uint8_t> PackString(const std::string& s, size_t maxLen) {

    const size_t len = std::min(s.size(), maxLen);

    std::vector<uint8_t> out(1 + len);

    out[0] = static_cast<uint8_t>(len);

    if (len > 0) {

        std::memcpy(out.data() + 1, s.data(), len);

    }

    return out;

}



std::pair<std::string, size_t> UnpackString(const uint8_t* data, size_t dataLen, size_t offset) {

    if (!data || offset >= dataLen) return {"", dataLen};

    const uint8_t len = data[offset];

    if (offset + 1 + len > dataLen) return {"", dataLen};

    return {std::string(reinterpret_cast<const char*>(data + offset + 1), len), offset + 1 + len};

}



std::vector<uint8_t> PackLogin(uint8_t msgType, const std::string& username, const std::string& password) {

    std::vector<uint8_t> payload;

    payload.push_back(ProtocolVersion);

    auto user = PackString(username, UsernameMax);

    payload.insert(payload.end(), user.begin(), user.end());

    auto pass = PackString(password, PasswordMax);

    payload.insert(payload.end(), pass.begin(), pass.end());

    return PackTcp(msgType, payload.data(), payload.size());

}



std::vector<uint8_t> PackUdpInput(uint32_t tickId, float moveX, float moveY, float angle,

                                  uint8_t hotbarSlot, bool fire, bool pickup) {

    std::vector<uint8_t> out(1 + 1 + 12);

    out[0] = UdpMsg::C_INPUT;

    out[1] = 0;



    uint8_t* p = out.data() + 2;

    p[0] = static_cast<uint8_t>(tickId & 0xFF);

    p[1] = static_cast<uint8_t>((tickId >> 8) & 0xFF);

    p[2] = static_cast<uint8_t>((tickId >> 16) & 0xFF);

    p[3] = static_cast<uint8_t>((tickId >> 24) & 0xFF);



    const auto clamp = [](float v) { return std::clamp(v, -1.f, 1.f); };

    const int16_t mx = static_cast<int16_t>(clamp(moveX) * 32767.f);

    const int16_t my = static_cast<int16_t>(clamp(moveY) * 32767.f);

    p[4] = static_cast<uint8_t>(mx & 0xFF);

    p[5] = static_cast<uint8_t>((mx >> 8) & 0xFF);

    p[6] = static_cast<uint8_t>(my & 0xFF);

    p[7] = static_cast<uint8_t>((my >> 8) & 0xFF);



    float ang = angle - std::floor(angle);

    if (ang < 0.f) ang += 1.f;

    const uint16_t ai = static_cast<uint16_t>(ang * 65535.f);

    p[8] = static_cast<uint8_t>(ai & 0xFF);

    p[9] = static_cast<uint8_t>((ai >> 8) & 0xFF);

    p[10] = hotbarSlot;

    p[11] = static_cast<uint8_t>((fire ? 1 : 0) | (pickup ? 2 : 0));

    return out;

}



bool UnpackUdpInput(const uint8_t* data, size_t dataLen, UdpInput& out) {

    if (!data || dataLen < 1 + 12 || data[0] != UdpMsg::C_INPUT) return false;

    const uint8_t* p = data + 1;

    out.tickId = static_cast<uint32_t>(p[0]) | (static_cast<uint32_t>(p[1]) << 8) |

                 (static_cast<uint32_t>(p[2]) << 16) | (static_cast<uint32_t>(p[3]) << 24);

    const int16_t mx = static_cast<int16_t>(static_cast<uint16_t>(p[4]) | (static_cast<uint16_t>(p[5]) << 8));

    const int16_t my = static_cast<int16_t>(static_cast<uint16_t>(p[6]) | (static_cast<uint16_t>(p[7]) << 8));

    const uint16_t ai = static_cast<uint16_t>(static_cast<uint16_t>(p[8]) | (static_cast<uint16_t>(p[9]) << 8));

    out.moveX = static_cast<float>(mx) / 32767.f;

    out.moveY = static_cast<float>(my) / 32767.f;

    out.angle = static_cast<float>(ai) / 65535.f;

    out.hotbarSlot = p[10];

    out.fire = (p[11] & 1) != 0;

    out.pickup = (p[11] & 2) != 0;

    return true;

}



// SHA-256 helpers
std::string Sha256Hex(const std::string& input) {
    SHA256 sha256;
    return sha256(input);
}

bool IsSha256Hex(const std::string& s) {
    if (s.size() != 64) return false;
    for (unsigned char c : s) {
        bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        if (!ok) return false;
    }
    return true;
}

} // namespace Protocol

