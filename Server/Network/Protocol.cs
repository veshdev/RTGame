using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Server.Network;

public static class NetworkConstants
{
    public const int TcpPort = 9000;
    public const int UdpPort = 9001;
    public const int ServerTickRate = 100;
    public const float TickDt = 1.0f / ServerTickRate;

    public const int MaxPlayers = 4;
    public const int MaxRooms = 16;
    public const int MaxMonsters = 32;
    public const int MaxProjectiles = 64;
    public const int MaxLoot = 32;

    public const int ProtocolVersion = 2;
    public const float ExtractionDuration = 10.0f;
    public const int PointsPerLoot = 10;
}

public static class TcpMsg
{
    public const byte C_LOGIN = 0x01;
    public const byte C_REGISTER = 0x09;
    public const byte C_ROOM_LIST_REQ = 0x02;
    public const byte C_ROOM_CREATE = 0x03;
    public const byte C_ROOM_JOIN = 0x04;
    public const byte C_ROOM_LEAVE = 0x05;
    public const byte C_READY_TOGGLE = 0x06;
    public const byte C_HOST_START = 0x07;
    public const byte C_HOTBAR_REQUEST = 0x08;
    public const byte C_DISCONNECT = 0x0F;

    public const byte S_LOGIN_ACK = 0x80;
    public const byte S_LOGIN_REJECT = 0x81;
    public const byte S_ROOM_LIST = 0x82;
    public const byte S_ROOM_JOINED = 0x83;
    public const byte S_ROOM_LEFT = 0x84;
    public const byte S_LOBBY_STATE = 0x85;
    public const byte S_MATCH_STARTING = 0x86;
    public const byte S_MATCH_STARTED = 0x87;
    public const byte S_HOTBAR_SNAPSHOT = 0x88;
    public const byte S_EXTRACTED = 0x89;
    public const byte S_ERROR = 0x8F;
}

public static class UdpMsg
{
    public const byte C_INPUT = 0x01;
    public const byte S_WORLD_SNAPSHOT = 0x81;
}

public static class DataSizes
{
    public const int UsernameMax = 16;
    public const int PasswordMax = 32;
    public const int RoomNameMax = 24;
    public const int RoomPasswordMax = 16;
    public const int ReasonMax = 64;

    public const int HotbarSlots = 9;
    public const int WeaponSlots = 4;

    public const int TileSize = 32;
    public const int MaxMapW = 128;
    public const int MaxMapH = 128;
    public const int MapW = 64;
    public const int MapH = 64;

    public const int TcpHeaderSize = 3;
    public const int SlotStructSize = 2;
}

public enum HotbarSlot : byte
{
    Axe = 0,
    Pistol = 1,
    Rifle = 2,
    Shotgun = 3,
    PistolMag = 4,
    RifleMag = 5,
    ShotgunAmmo = 6,
    Medkit = 7,
    Loot = 8,
}

public enum ItemType : byte
{
    Empty = 0,
    Axe = 1,
    Pistol = 2,
    Rifle = 3,
    Shotgun = 4,
    Medkit = 5,
    AmmoPistol = 6,
    AmmoRifle = 7,
    AmmoShotgun = 8,
    Loot = 9,
}

public static class HotbarLayout
{
    public static readonly ItemType[] SlotItems =
    {
        ItemType.Axe,
        ItemType.Pistol,
        ItemType.Rifle,
        ItemType.Shotgun,
        ItemType.AmmoPistol,
        ItemType.AmmoRifle,
        ItemType.AmmoShotgun,
        ItemType.Medkit,
        ItemType.Loot,
    };

    public static int AmmoSlotForWeapon(ItemType weapon) => weapon switch
    {
        ItemType.Pistol => (int)HotbarSlot.PistolMag,
        ItemType.Rifle => (int)HotbarSlot.RifleMag,
        ItemType.Shotgun => (int)HotbarSlot.ShotgunAmmo,
        _ => -1,
    };

    public static int HotbarSlotForPickup(ItemType item) => item switch
    {
        ItemType.Loot => (int)HotbarSlot.Loot,
        ItemType.AmmoPistol => (int)HotbarSlot.PistolMag,
        ItemType.AmmoRifle => (int)HotbarSlot.RifleMag,
        ItemType.AmmoShotgun => (int)HotbarSlot.ShotgunAmmo,
        ItemType.Medkit => (int)HotbarSlot.Medkit,
        _ => -1,
    };
}

public static class ItemProperties
{
    public static readonly Dictionary<ItemType, bool> IsWeapon = new()
    {
        { ItemType.Axe, true },
        { ItemType.Pistol, true },
        { ItemType.Rifle, true },
        { ItemType.Shotgun, true },
    };

    public static readonly Dictionary<ItemType, int> WeaponDamage = new()
    {
        { ItemType.Axe, 35 },
        { ItemType.Pistol, 25 },
        { ItemType.Rifle, 35 },
        { ItemType.Shotgun, 60 },
    };

    public static readonly Dictionary<ItemType, float> WeaponCooldown = new()
    {
        { ItemType.Axe, 0.55f },
        { ItemType.Pistol, 0.40f },
        { ItemType.Rifle, 0.18f },
        { ItemType.Shotgun, 0.80f },
    };

    public static readonly Dictionary<ItemType, int> WeaponRange = new()
    {
        { ItemType.Axe, 48 },
        { ItemType.Pistol, 400 },
        { ItemType.Rifle, 600 },
        { ItemType.Shotgun, 200 },
    };

    public static readonly Dictionary<ItemType, int> WeaponSpeed = new()
    {
        { ItemType.Pistol, 480 },
        { ItemType.Rifle, 700 },
        { ItemType.Shotgun, 350 },
    };
}

public enum EntityType : byte
{
    Player = 0,
    Monster = 1,
    Projectile = 2,
    Loot = 3,
}

public enum MonsterType : byte
{
    Zombie = 0,
    Marauder = 1,
}

public static class MonsterProperties
{
    public static readonly Dictionary<MonsterType, int> Hp = new()
    {
        { MonsterType.Zombie, 60 },
        { MonsterType.Marauder, 90 },
    };

    public static readonly Dictionary<MonsterType, float> Speed = new()
    {
        { MonsterType.Zombie, 75.0f },
        { MonsterType.Marauder, 95.0f },
    };

    public static readonly Dictionary<MonsterType, int> MeleeDamage = new()
    {
        { MonsterType.Zombie, 18 },
        { MonsterType.Marauder, 12 },
    };

    public static readonly Dictionary<MonsterType, float> AttackRange = new()
    {
        { MonsterType.Zombie, 30.0f },
        { MonsterType.Marauder, 28.0f },
    };

    public static readonly Dictionary<MonsterType, float> AttackCooldown = new()
    {
        { MonsterType.Zombie, 0.8f },
        { MonsterType.Marauder, 1.0f },
    };

    public static readonly Dictionary<MonsterType, float> DetectRange = new()
    {
        { MonsterType.Zombie, 160.0f },
        { MonsterType.Marauder, 220.0f },
    };
}

public enum TileType : byte
{
    Road = 0,
    Wall = 1,
    Floor = 2,
    Spawn = 3,
    Extraction = 4,
    Door = 5,
}

public enum RoomState : byte
{
    Lobby = 0,
    Starting = 1,
    InGame = 2,
}

public static class PacketHelpers
{
    public static byte[] PackTcp(byte msgType, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        ushort total = (ushort)(1 + payload.Length);
        byte[] result = new byte[DataSizes.TcpHeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0, 2), total);
        result[2] = msgType;
        if (payload.Length > 0)
            Buffer.BlockCopy(payload, 0, result, 3, payload.Length);
        return result;
    }

    public static (ushort length, byte msgType) UnpackTcpHeader(byte[] data)
    {
        if (data.Length < DataSizes.TcpHeaderSize)
            throw new ArgumentException($"Data too short for TCP header (need {DataSizes.TcpHeaderSize} bytes)");
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        return (length, data[2]);
    }

    public static byte[] PackString(string? s, int maxLen)
    {
        if (string.IsNullOrEmpty(s))
            return new byte[] { 0 };

        byte[] encoded = Encoding.UTF8.GetBytes(s);
        if (encoded.Length > maxLen)
            Array.Resize(ref encoded, maxLen);

        byte[] result = new byte[1 + encoded.Length];
        result[0] = (byte)encoded.Length;
        Buffer.BlockCopy(encoded, 0, result, 1, encoded.Length);
        return result;
    }

    public static (string value, int newOffset) UnpackString(byte[] data, int offset)
    {
        if (offset >= data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        byte length = data[offset];
        if (offset + 1 + length > data.Length)
            throw new ArgumentException("Data too short for string");
        return (Encoding.UTF8.GetString(data, offset + 1, length), offset + 1 + length);
    }

    public static byte[] PackHotbarSlot(byte quantity) => new[] { quantity };

    public static (byte quantity, int newOffset) UnpackHotbarSlot(byte[] data, int offset)
    {
        if (offset >= data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return (data[offset], offset + DataSizes.SlotStructSize);
    }
}

public struct UdpInputData
{
    public uint TickId { get; set; }
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public float Angle { get; set; }
    public byte HotbarSlot { get; set; }
    public bool Fire { get; set; }
    public bool Pickup { get; set; }
}

public static class UdpPacketHelpers
{
    private const int InputPayloadSize = 12;

    public static UdpInputData? UnpackUdpInput(byte[] data)
    {
        if (data == null || data.Length < 1 + InputPayloadSize || data[0] != UdpMsg.C_INPUT)
            return null;

        try
        {
            ReadOnlySpan<byte> payload = data.AsSpan(1, InputPayloadSize);
            byte flags = payload[11];
            return new UdpInputData
            {
                TickId = BinaryPrimitives.ReadUInt32LittleEndian(payload[0..4]),
                MoveX = BinaryPrimitives.ReadInt16LittleEndian(payload[4..6]) / 32767.0f,
                MoveY = BinaryPrimitives.ReadInt16LittleEndian(payload[6..8]) / 32767.0f,
                Angle = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..10]) / 65535.0f,
                HotbarSlot = payload[10],
                Fire = (flags & 1) != 0,
                Pickup = (flags & 2) != 0,
            };
        }
        catch
        {
            return null;
        }
    }
}
