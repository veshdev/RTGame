#pragma once



#include <cstdint>

#include <string>

#include <vector>



namespace Protocol {



constexpr uint16_t TcpPort = 9000;

constexpr uint16_t UdpPort = 9001;

constexpr const char* DefaultServerHost = "127.0.0.1";

constexpr uint32_t ServerTickRate = 100;

constexpr float TickDt = 1.0f / static_cast<float>(ServerTickRate);

constexpr float ExtractionDuration = 10.0f;

constexpr int PointsPerLoot = 10;



constexpr uint32_t MaxPlayers = 4;

constexpr uint32_t MaxMonsters = 32;

constexpr uint32_t MaxProjectiles = 64;

constexpr uint32_t MaxLoot = 32;

constexpr uint8_t ProtocolVersion = 2;



constexpr uint32_t UsernameMax = 16;

constexpr uint32_t PasswordMax = 64;

constexpr uint32_t RoomNameMax = 24;

constexpr uint32_t RoomPasswordMax = 16;

constexpr uint32_t ReasonMax = 64;



constexpr uint32_t HotbarSlots = 9;

constexpr uint32_t WeaponSlots = 4;

constexpr uint32_t SlotStructSize = 1;



constexpr uint32_t TileSize = 32;

constexpr uint32_t MaxMapW = 128;

constexpr uint32_t MaxMapH = 128;

constexpr uint32_t MapW = 64;

constexpr uint32_t MapH = 64;

constexpr uint32_t TcpHeaderSize = 3;

constexpr uint32_t UdpInputPayloadSize = 12; // tickId(4) + moveX(2) + moveY(2) + angle(2) + hotbar(1) + flags(1)



namespace TcpMsg {

constexpr uint8_t C_LOGIN = 0x01;

constexpr uint8_t C_REGISTER = 0x09;

constexpr uint8_t C_ROOM_LIST_REQ = 0x02;

constexpr uint8_t C_ROOM_CREATE = 0x03;

constexpr uint8_t C_ROOM_JOIN = 0x04;

constexpr uint8_t C_ROOM_LEAVE = 0x05;

constexpr uint8_t C_READY_TOGGLE = 0x06;

constexpr uint8_t C_HOST_START = 0x07;

constexpr uint8_t C_HOTBAR_REQUEST = 0x08;

constexpr uint8_t C_DISCONNECT = 0x0F;



constexpr uint8_t S_LOGIN_ACK = 0x80;

constexpr uint8_t S_LOGIN_REJECT = 0x81;

constexpr uint8_t S_ROOM_LIST = 0x82;

constexpr uint8_t S_ROOM_JOINED = 0x83;

constexpr uint8_t S_ROOM_LEFT = 0x84;

constexpr uint8_t S_LOBBY_STATE = 0x85;

constexpr uint8_t S_MATCH_STARTING = 0x86;

constexpr uint8_t S_MATCH_STARTED = 0x87;

constexpr uint8_t S_HOTBAR_SNAPSHOT = 0x88;

constexpr uint8_t S_EXTRACTED = 0x89;

constexpr uint8_t S_ERROR = 0x8F;

}



namespace UdpMsg {

constexpr uint8_t C_INPUT = 0x01;

constexpr uint8_t S_WORLD_SNAPSHOT = 0x81;

}



namespace HotbarSlot {

constexpr uint8_t Axe = 0;

constexpr uint8_t Pistol = 1;

constexpr uint8_t Rifle = 2;

constexpr uint8_t Shotgun = 3;

constexpr uint8_t PistolMag = 4;

constexpr uint8_t RifleMag = 5;

constexpr uint8_t ShotgunAmmo = 6;

constexpr uint8_t Medkit = 7;

constexpr uint8_t Loot = 8;

}



namespace ItemType {

constexpr uint8_t Empty = 0;

constexpr uint8_t Axe = 1;

constexpr uint8_t Pistol = 2;

constexpr uint8_t Rifle = 3;

constexpr uint8_t Shotgun = 4;

constexpr uint8_t Medkit = 5;

constexpr uint8_t AmmoPistol = 6;

constexpr uint8_t AmmoRifle = 7;

constexpr uint8_t AmmoShotgun = 8;

constexpr uint8_t Loot = 9;

}



namespace MonsterType {

constexpr uint8_t Zombie = 0;

constexpr uint8_t Marauder = 1;

}



namespace TileType {

constexpr uint8_t Road = 0;

constexpr uint8_t Wall = 1;

constexpr uint8_t Floor = 2;

constexpr uint8_t Spawn = 3;

constexpr uint8_t Extraction = 4;
constexpr uint8_t Door = 5;

}



namespace Entity {

constexpr float PlayerSpeed = 120.0f;

constexpr int PlayerRadius = 12;

constexpr int PlayerMaxHp = 100;

constexpr int PickupRadius = 28;

}



struct UdpInput {

    uint32_t tickId = 0;

    float moveX = 0.f;

    float moveY = 0.f;

    float angle = 0.f;

    uint8_t hotbarSlot = 0;

    bool fire = false;

    bool pickup = false;

};



const char* HotbarSlotName(uint8_t slot);

const char* ItemName(uint8_t type);

bool IsWeaponSlot(uint8_t slot);



std::vector<uint8_t> PackTcp(uint8_t msgType, const uint8_t* payload, size_t payloadLen);

std::vector<uint8_t> PackString(const std::string& s, size_t maxLen);

std::pair<std::string, size_t> UnpackString(const uint8_t* data, size_t dataLen, size_t offset);



std::vector<uint8_t> PackLogin(uint8_t msgType, const std::string& username, const std::string& password);

// Hash helpers
std::string Sha256Hex(const std::string& input);
bool IsSha256Hex(const std::string& s);

std::vector<uint8_t> PackUdpInput(uint32_t tickId, float moveX, float moveY, float angle,

                                  uint8_t hotbarSlot, bool fire, bool pickup);

bool UnpackUdpInput(const uint8_t* data, size_t dataLen, UdpInput& out);



} // namespace Protocol

