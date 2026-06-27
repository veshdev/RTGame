using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Server.Network;

public struct PlayerEntryData
{
    public byte PlayerId { get; set; }
    public short Hp { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public byte HotbarSlot { get; set; }
    public bool Alive { get; set; }
    public byte Kills { get; set; }
    public byte CarriedLoot { get; set; }
    public byte ExtractionProgress { get; set; }
}

public struct MonsterEntryData
{
    public byte MonsterId { get; set; }
    public MonsterType Type { get; set; }
    public ItemType WeaponType { get; set; }
    public short Hp { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public byte State { get; set; }
}

public struct ProjectileEntryData
{
    public byte ProjectileId { get; set; }
    public byte OwnerId { get; set; }
    public ItemType WeaponType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public float Speed { get; set; }
}

public struct LootEntryData
{
    public byte LootId { get; set; }
    public ItemType ItemType { get; set; }
    public byte Quantity { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

public class SnapshotData
{
    public uint TickId { get; set; }
    public byte RecipientPlayerId { get; set; }
    public List<PlayerEntryData> Players { get; set; } = new();
    public List<MonsterEntryData> Monsters { get; set; } = new();
    public List<ProjectileEntryData> Projectiles { get; set; } = new();
    public List<LootEntryData> Loot { get; set; } = new();
}

public static class SnapshotHelpers
{
    private const int PlayerEntrySize = 19;
    private const int MonsterEntrySize = 17;
    private const int ProjectileEntrySize = 16;
    private const int LootEntrySize = 12;
    private const int SnapshotHeaderSize = 9;

    private static byte[] PackPlayerEntry(PlayerEntryData player)
    {
        byte[] result = new byte[PlayerEntrySize];
        Span<byte> span = result.AsSpan();
        span[0] = 1;
        span[1] = player.PlayerId;
        BinaryPrimitives.WriteInt16LittleEndian(span[2..4], player.Hp);
        BinaryPrimitives.WriteSingleLittleEndian(span[4..8], player.X);
        BinaryPrimitives.WriteSingleLittleEndian(span[8..12], player.Y);

        float angleNorm = player.Angle % 1.0f;
        if (angleNorm < 0) angleNorm += 1.0f;
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..14], (ushort)(angleNorm * 65535));

        span[14] = player.HotbarSlot;
        span[15] = (byte)(player.Alive ? 1 : 0);
        span[16] = Math.Min(player.Kills, (byte)255);
        span[17] = player.CarriedLoot;
        span[18] = player.ExtractionProgress;
        return result;
    }

    private static byte[] PackMonsterEntry(MonsterEntryData monster)
    {
        byte[] result = new byte[MonsterEntrySize];
        Span<byte> span = result.AsSpan();
        span[0] = 1;
        span[1] = monster.MonsterId;
        span[2] = (byte)monster.Type;
        span[3] = (byte)monster.WeaponType;
        BinaryPrimitives.WriteInt16LittleEndian(span[4..6], monster.Hp);
        BinaryPrimitives.WriteSingleLittleEndian(span[6..10], monster.X);
        BinaryPrimitives.WriteSingleLittleEndian(span[10..14], monster.Y);

        float angleNorm = monster.Angle % 1.0f;
        if (angleNorm < 0) angleNorm += 1.0f;
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..16], (ushort)(angleNorm * 65535));
        span[16] = monster.State;
        return result;
    }

    private static byte[] PackProjectileEntry(ProjectileEntryData projectile)
    {
        byte[] result = new byte[ProjectileEntrySize];
        Span<byte> span = result.AsSpan();
        span[0] = 1;
        span[1] = projectile.ProjectileId;
        span[2] = projectile.OwnerId;
        span[3] = (byte)projectile.WeaponType;
        BinaryPrimitives.WriteSingleLittleEndian(span[4..8], projectile.X);
        BinaryPrimitives.WriteSingleLittleEndian(span[8..12], projectile.Y);

        float angleNorm = projectile.Angle % 1.0f;
        if (angleNorm < 0) angleNorm += 1.0f;
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..14], (ushort)(angleNorm * 65535));
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..16], (ushort)Math.Min(projectile.Speed, 65535.0f));
        return result;
    }

    private static byte[] PackLootEntry(LootEntryData loot)
    {
        byte[] result = new byte[LootEntrySize];
        Span<byte> span = result.AsSpan();
        span[0] = 1;
        span[1] = loot.LootId;
        span[2] = (byte)loot.ItemType;
        span[3] = loot.Quantity;
        BinaryPrimitives.WriteSingleLittleEndian(span[4..8], loot.X);
        BinaryPrimitives.WriteSingleLittleEndian(span[8..12], loot.Y);
        return result;
    }

    public static byte[] BuildSnapshot(
        uint tickId,
        byte recipientPlayerId,
        IReadOnlyList<PlayerEntryData> players,
        IReadOnlyList<MonsterEntryData> monsters,
        IReadOnlyList<ProjectileEntryData> projectiles,
        IReadOnlyList<LootEntryData> loot)
    {
        int numPlayers = Math.Min(players?.Count ?? 0, NetworkConstants.MaxPlayers);
        int numMonsters = Math.Min(monsters?.Count ?? 0, NetworkConstants.MaxMonsters);
        int numProjectiles = Math.Min(projectiles?.Count ?? 0, NetworkConstants.MaxProjectiles);
        int numLoot = Math.Min(loot?.Count ?? 0, NetworkConstants.MaxLoot);

        int totalSize = 1 + SnapshotHeaderSize
            + numPlayers * PlayerEntrySize
            + numMonsters * MonsterEntrySize
            + numProjectiles * ProjectileEntrySize
            + numLoot * LootEntrySize;

        byte[] result = new byte[totalSize];
        Span<byte> span = result.AsSpan();
        span[0] = UdpMsg.S_WORLD_SNAPSHOT;
        BinaryPrimitives.WriteUInt32LittleEndian(span[1..5], tickId);
        span[5] = recipientPlayerId;
        span[6] = (byte)numPlayers;
        span[7] = (byte)numMonsters;
        span[8] = (byte)numProjectiles;
        span[9] = (byte)numLoot;

        int offset = 10;
        for (int i = 0; i < numPlayers; i++)
        {
            byte[] bytes = PackPlayerEntry(players![i]);
            Buffer.BlockCopy(bytes, 0, result, offset, PlayerEntrySize);
            offset += PlayerEntrySize;
        }
        for (int i = 0; i < numMonsters; i++)
        {
            byte[] bytes = PackMonsterEntry(monsters![i]);
            Buffer.BlockCopy(bytes, 0, result, offset, MonsterEntrySize);
            offset += MonsterEntrySize;
        }
        for (int i = 0; i < numProjectiles; i++)
        {
            byte[] bytes = PackProjectileEntry(projectiles![i]);
            Buffer.BlockCopy(bytes, 0, result, offset, ProjectileEntrySize);
            offset += ProjectileEntrySize;
        }
        for (int i = 0; i < numLoot; i++)
        {
            byte[] bytes = PackLootEntry(loot![i]);
            Buffer.BlockCopy(bytes, 0, result, offset, LootEntrySize);
            offset += LootEntrySize;
        }

        return result;
    }
}
