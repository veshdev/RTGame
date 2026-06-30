using System;
using System.Collections.Generic;
using Server.Network;

namespace Server.World;

public static class EntityConstants
{
    public const float PlayerSpeed = 120.0f;
    public const int PlayerRadius = 12;
    public const int PlayerMaxHp = 100;
    public const int PickupRadius = 28;
    public const int MedkitHeal = 40;
}

public sealed class Hotbar
{
    private readonly byte[] _quantities = new byte[DataSizes.HotbarSlots];

    public byte GetQuantity(int slot)
    {
        if (slot < 0 || slot >= _quantities.Length)
            return 0;
        return _quantities[slot];
    }

    public void SetQuantity(int slot, byte quantity)
    {
        if (slot < 0 || slot >= _quantities.Length)
            return;
        _quantities[slot] = quantity;
    }

    public bool AddQuantity(int slot, int amount)
    {
        if (slot < 0 || slot >= _quantities.Length || amount <= 0)
            return false;
        _quantities[slot] = (byte)Math.Min(_quantities[slot] + amount, 255);
        return true;
    }

    public bool TryConsume(int slot, int amount = 1)
    {
        if (slot < 0 || slot >= _quantities.Length || _quantities[slot] < amount)
            return false;
        _quantities[slot] = (byte)(_quantities[slot] - amount);
        return true;
    }

    public ItemType GetWeapon(int hotbarSlot)
    {
        if (hotbarSlot < 0 || hotbarSlot >= DataSizes.WeaponSlots)
            return ItemType.Empty;
        if (_quantities[hotbarSlot] <= 0)
            return ItemType.Empty;
        return HotbarLayout.SlotItems[hotbarSlot];
    }

    public void ResetDefaultLoadout()
    {
        Array.Clear(_quantities, 0, _quantities.Length);
        _quantities[(int)HotbarSlot.Axe] = 1;
        _quantities[(int)HotbarSlot.Pistol] = 1;
        _quantities[(int)HotbarSlot.Rifle] = 1;
        _quantities[(int)HotbarSlot.Shotgun] = 1;
        _quantities[(int)HotbarSlot.PistolMag] = 30;
        _quantities[(int)HotbarSlot.RifleMag] = 30;
        _quantities[(int)HotbarSlot.ShotgunAmmo] = 12;
        _quantities[(int)HotbarSlot.Medkit] = 1;
    }

    public byte[] ToBytes()
    {
        byte[] copy = new byte[_quantities.Length];
        Buffer.BlockCopy(_quantities, 0, copy, 0, _quantities.Length);
        return copy;
    }
}

public class Player
{
    public int PlayerId { get; set; }
    public string Username { get; set; }
    internal TcpClientHandler? TcpConn { get; set; }
    public (string ip, int port)? UdpAddr { get; set; }


    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public int Hp { get; set; }
    public bool Alive { get; set; }
    public int Kills { get; set; }

    public int HotbarSlot { get; set; }
    public Hotbar Hotbar { get; set; }

    public float ShootCooldown { get; set; }
    public uint LastInputTick { get; set; }
    public float ExtractionTimer { get; set; }

    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public bool Fire { get; set; }
    public bool Pickup { get; set; }
    public bool IsConnected { get; set; }

    public Player(int playerId, string username)
    {
        PlayerId = playerId;
        Username = username ?? string.Empty;
        Hp = EntityConstants.PlayerMaxHp;
        Alive = true;
        HotbarSlot = (int)Network.HotbarSlot.Pistol;
        Hotbar = new Hotbar();
        Hotbar.ResetDefaultLoadout();
        IsConnected = true;
    }

    public void Respawn(float x, float y)
    {
        X = x;
        Y = y;
        Hp = EntityConstants.PlayerMaxHp;
        Alive = true;
        ShootCooldown = 0.0f;
        ExtractionTimer = 0.0f;
        Hotbar.ResetDefaultLoadout();
    }

    public int CarriedLoot => Hotbar.GetQuantity((int)Network.HotbarSlot.Loot);
}

public static class MonsterState
{
    public const int Wander = 0;
    public const int Chase = 1;
    public const int Attack = 2;
    public const int Dead = 3;
}

public class Monster
{
    public int MonsterId { get; set; }
    public MonsterType MonsterType { get; set; }
    public ItemType WeaponType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public int Hp { get; set; }
    public bool Alive { get; set; }
    public int State { get; set; }
    public int TargetPlayerId { get; set; }
    public float AttackCooldown { get; set; }
    public float ShootCooldown { get; set; }
    public float WanderTimer { get; set; }
    public float WanderDx { get; set; }
    public float WanderDy { get; set; }

    public Monster(int monsterId, MonsterType monsterType, float x, float y, ItemType weaponType = ItemType.Empty)
    {
        MonsterId = monsterId;
        MonsterType = monsterType;
        WeaponType = weaponType;
        X = x;
        Y = y;
        Hp = MonsterProperties.Hp[monsterType];
        Alive = true;
        State = MonsterState.Wander;
        TargetPlayerId = -1;
    }
}

public class Projectile
{
    public int ProjId { get; set; }
    public int OwnerId { get; set; }
    public bool FromMonster { get; set; }
    public ItemType WeaponType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public float Speed { get; set; }
    public int Damage { get; set; }
    public float MaxRange { get; set; }
    public float Traveled { get; set; }
    public bool Alive { get; set; }

    public Projectile(int projId, int ownerId, ItemType weaponType, float x, float y, float angle, bool fromMonster = false)
    {
        ProjId = projId;
        OwnerId = ownerId;
        FromMonster = fromMonster;
        WeaponType = weaponType;
        X = x;
        Y = y;
        Angle = angle;
        Speed = ItemProperties.WeaponSpeed.GetValueOrDefault(weaponType, 400);
        Damage = ItemProperties.WeaponDamage.GetValueOrDefault(weaponType, 20);
        MaxRange = ItemProperties.WeaponRange.GetValueOrDefault(weaponType, 400);
        Alive = true;
    }
}

public class LootItem
{
    public int LootId { get; set; }
    public ItemType ItemType { get; set; }
    public int Quantity { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool Alive { get; set; }

    public LootItem(int lootId, ItemType itemType, int quantity, float x, float y)
    {
        LootId = lootId;
        ItemType = itemType;
        Quantity = quantity;
        X = x;
        Y = y;
        Alive = true;
    }
}

public readonly struct ExtractionZone
{
    public ExtractionZone(float x, float y, float radius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public float X { get; }
    public float Y { get; }
    public float Radius { get; }

    public bool Contains(float wx, float wy)
    {
        float dx = wx - X;
        float dy = wy - Y;
        return dx * dx + dy * dy <= Radius * Radius;
    }
}
