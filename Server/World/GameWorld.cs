using System;
using System.Collections.Generic;
using System.Linq;
using Server.Network;

namespace Server.World;

internal class GameWorld
{
    private const int EnemyCountBase = 10;
    private const double MarauderRatio = 0.35;
    private const int SHOTGUN_PELLETS = 5;
    private const double SHOTGUN_SPREAD = 0.06;
    private const float MarauderShootRange = 260.0f;

    public int RoomId { get; }
    public uint MapHash { get; }
    public int MapWidth { get; }
    public int MapHeight { get; }
    public uint TickId { get; private set; }
    public bool Running { get; private set; }

    public byte[] Tiles { get; }
    public List<ExtractionZone> ExtractionZones { get; } = new();

    public Dictionary<int, Player> Players { get; } = new();
    public Dictionary<int, Monster> Monsters { get; } = new();
    public Dictionary<int, Projectile> Projectiles { get; } = new();
    public Dictionary<int, LootItem> LootItems { get; } = new();

    public event Action<Player, int>? PlayerExtracted;
    public event Action<Player>? HotbarChanged;
    public event Action? AllPlayersGone;

    private readonly Random _rng;
    private int _nextMonsterId;
    private int _nextProjId;
    private int _nextLootId;

    public GameWorld(int roomId, LoadedMap map, uint matchSeed, List<Player> players)
    {
        RoomId = roomId;
        MapHash = map.MapHash;
        MapWidth = map.Width;
        MapHeight = map.Height;
        Tiles = map.Tiles;
        ExtractionZones.AddRange(map.ExtractionZones);
        _rng = new Random((int)matchSeed);

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var (sx, sy) = map.PlayerSpawns[i % map.PlayerSpawns.Count];
            player.Respawn(sx, sy);
            Players[player.PlayerId] = player;
        }

        var enemySpawns = MapLoader.PickMonsterSpawns(
            Tiles, MapWidth, MapHeight, map.PlayerSpawns, EnemyCountBase, matchSeed);
        foreach (var (mx, my) in enemySpawns)
        {
            if (_rng.NextDouble() < MarauderRatio)
                SpawnMonster(MonsterType.Marauder, mx, my, PickMarauderWeapon());
            else
                SpawnMonster(MonsterType.Zombie, mx, my);
        }

        Running = true;
    }

    public void Tick()
    {
        float dt = NetworkConstants.TickDt;
        TickId += 1;

        TickPlayers(dt);
        TickProjectiles(dt);
        TickMonsters(dt);
        TickExtraction(dt);
        CleanupDead();

        // Check if all players are gone (dead or disconnected)
        if (Players.Count > 0 && Players.Values.All(p => !p.Alive || !p.IsConnected))
        {
            Running = false;
            AllPlayersGone?.Invoke();
        }
    }

    public void ApplyInput(int playerId, UdpInputData input)
    {
        if (!Players.TryGetValue(playerId, out var player) || !player.Alive)
            return;

        player.MoveX = input.MoveX;
        player.MoveY = input.MoveY;
        player.Angle = input.Angle;
        player.HotbarSlot = Math.Min(input.HotbarSlot, (byte)(DataSizes.HotbarSlots - 1));
        player.Fire = input.Fire;
        player.Pickup = input.Pickup;
        player.LastInputTick = input.TickId;
    }

    public byte[] BuildSnapshotFor(int recipientPid)
    {
        var players = Players.Values.Select(p => new PlayerEntryData
        {
            PlayerId = (byte)p.PlayerId,
            Hp = (short)p.Hp,
            X = p.X,
            Y = p.Y,
            Angle = p.Angle,
            HotbarSlot = (byte)p.HotbarSlot,
            Alive = p.Alive,
            Kills = (byte)Math.Min(p.Kills, 255),
            CarriedLoot = (byte)Math.Min(p.CarriedLoot, 255),
            ExtractionProgress = ToExtractionProgress(p.ExtractionTimer),
        }).ToList();

        var monsters = Monsters.Values.Where(m => m.Alive).Select(m => new MonsterEntryData
        {
            MonsterId = (byte)m.MonsterId,
            Type = m.MonsterType,
            WeaponType = m.WeaponType,
            Hp = (short)m.Hp,
            X = m.X,
            Y = m.Y,
            Angle = m.Angle,
            State = (byte)m.State,
        }).ToList();

        var projectiles = Projectiles.Values.Where(p => p.Alive).Select(pr => new ProjectileEntryData
        {
            ProjectileId = (byte)pr.ProjId,
            OwnerId = (byte)pr.OwnerId,
            WeaponType = pr.WeaponType,
            X = pr.X,
            Y = pr.Y,
            Angle = pr.Angle,
            Speed = pr.Speed,
        }).ToList();

        var loot = LootItems.Values.Where(l => l.Alive).Select(l => new LootEntryData
        {
            LootId = (byte)l.LootId,
            ItemType = l.ItemType,
            Quantity = (byte)Math.Min(l.Quantity, 255),
            X = l.X,
            Y = l.Y,
        }).ToList();

        return SnapshotHelpers.BuildSnapshot(TickId, (byte)recipientPid, players, monsters, projectiles, loot);
    }

    private static byte ToExtractionProgress(float timer)
    {
        if (timer <= 0)
            return 0;
        return (byte)Math.Clamp((int)(timer / NetworkConstants.ExtractionDuration * 100), 1, 100);
    }

    private void TickPlayers(float dt)
    {
        foreach (var player in Players.Values)
        {
            if (!player.Alive)
                continue;

            MoveEntity(player, player.MoveX, player.MoveY, dt);

            if (player.ShootCooldown > 0)
                player.ShootCooldown -= dt;

            if (player.Fire && player.ShootCooldown <= 0)
                UseHotbarSlot(player);

            player.Fire = false;

            if (player.Pickup)
                TryPickup(player);
        }
    }

    private void MoveEntity(Player player, float mx, float my, float dt)
    {
        double length = Math.Sqrt(mx * mx + my * my);
        if (length <= 0.01)
            return;

        float nx = (float)(mx / length);
        float ny = (float)(my / length);
        float newX = player.X + nx * EntityConstants.PlayerSpeed * dt;
        float newY = player.Y + ny * EntityConstants.PlayerSpeed * dt;
        if (CanMove(newX, player.Y, EntityConstants.PlayerRadius))
            player.X = newX;
        if (CanMove(player.X, newY, EntityConstants.PlayerRadius))
            player.Y = newY;
    }

    private void UseHotbarSlot(Player player)
    {
        int slot = player.HotbarSlot;
        ItemType item = HotbarLayout.SlotItems[slot];

        if (slot == (int)HotbarSlot.Medkit)
        {
            if (player.Hp >= EntityConstants.PlayerMaxHp)
                return;
            if (player.Hotbar.GetQuantity(slot) == 0)
                return;
            if (!player.Hotbar.TryConsume(slot))
                return;
            player.Hp = Math.Min(EntityConstants.PlayerMaxHp, player.Hp + EntityConstants.MedkitHeal);
            player.ShootCooldown = 0.5f;
            NotifyHotbarChanged(player);
            return;
        }

        ItemType weapon = player.Hotbar.GetWeapon(slot);
        if (weapon == ItemType.Empty)
            return;

        if (weapon == ItemType.Axe)
        {
            player.ShootCooldown = ItemProperties.WeaponCooldown[ItemType.Axe];
            MeleeAttack(player, ItemProperties.WeaponDamage[ItemType.Axe], ItemProperties.WeaponRange[ItemType.Axe]);
            return;
        }

        int ammoSlot = HotbarLayout.AmmoSlotForWeapon(weapon);
        if (ammoSlot < 0 || !player.Hotbar.TryConsume(ammoSlot))
            return;

        player.ShootCooldown = ItemProperties.WeaponCooldown[weapon];
        if (weapon == ItemType.Shotgun)
        {
            double baseAngle = player.Angle;
            for (int i = 0; i < SHOTGUN_PELLETS; i++)
            {
                double spread = (i - SHOTGUN_PELLETS / 2.0) * SHOTGUN_SPREAD / SHOTGUN_PELLETS;
                SpawnProjectile(player.PlayerId, weapon, player.X, player.Y, (float)((baseAngle + spread) % 1.0));
            }
        }
        else
        {
            SpawnProjectile(player.PlayerId, weapon, player.X, player.Y, player.Angle);
        }

        NotifyHotbarChanged(player);
    }

    private void MeleeAttack(Player player, int damage, int range)
    {
        double rad = player.Angle * 2.0 * Math.PI;
        float hx = player.X + (float)(Math.Sin(rad) * range);
        float hy = player.Y - (float)(Math.Cos(rad) * range);

        foreach (var monster in Monsters.Values)
        {
            if (!monster.Alive) continue;
            if (CirclePoint(hx, hy, EntityConstants.PlayerRadius, monster.X, monster.Y))
                ApplyDamageMonster(monster, damage, player.PlayerId);
        }
    }

    private void TryPickup(Player player)
    {
        foreach (var loot in LootItems.Values.ToList())
        {
            if (!loot.Alive)
                continue;

            float dx = loot.X - player.X;
            float dy = loot.Y - player.Y;
            if (dx * dx + dy * dy >= EntityConstants.PickupRadius * EntityConstants.PickupRadius)
                continue;

            int slot = HotbarLayout.HotbarSlotForPickup(loot.ItemType);
            if (slot < 0)
                continue;

            if (player.Hotbar.AddQuantity(slot, loot.Quantity))
            {
                loot.Alive = false;
                NotifyHotbarChanged(player);
            }
        }
    }

    private void TickProjectiles(float dt)
    {
        foreach (var proj in Projectiles.Values.ToList())
        {
            if (!proj.Alive)
                continue;

            double radAngle = proj.Angle * 2.0 * Math.PI;
            float dx = (float)(Math.Sin(radAngle) * proj.Speed * dt);
            float dy = (float)(-Math.Cos(radAngle) * proj.Speed * dt);

            float newX = proj.X + dx;
            float newY = proj.Y + dy;
            proj.Traveled += (float)Math.Sqrt(dx * dx + dy * dy);

            var (tx, ty) = MapData.WorldToTile(newX, newY);
            if (MapData.BlocksMovement(Tiles, MapWidth, MapHeight, tx, ty))
            {
                proj.Alive = false;
                continue;
            }

            if (proj.Traveled >= proj.MaxRange)
            {
                proj.Alive = false;
                continue;
            }

            proj.X = newX;
            proj.Y = newY;

            if (proj.FromMonster)
            {
                foreach (var player in Players.Values)
                {
                    if (!player.Alive) continue;
                    if (CirclePoint(player.X, player.Y, EntityConstants.PlayerRadius, proj.X, proj.Y))
                    {
                        ApplyDamagePlayer(player, proj.Damage, proj.OwnerId);
                        proj.Alive = false;
                        break;
                    }
                }
                continue;
            }

            foreach (var kv in Players)
            {
                if (kv.Key == proj.OwnerId || !kv.Value.Alive)
                    continue;
                if (CirclePoint(kv.Value.X, kv.Value.Y, EntityConstants.PlayerRadius, proj.X, proj.Y))
                {
                    ApplyDamagePlayer(kv.Value, proj.Damage, proj.OwnerId);
                    proj.Alive = false;
                    goto nextProj;
                }
            }

            foreach (var monster in Monsters.Values)
            {
                if (!monster.Alive) continue;
                if (CirclePoint(monster.X, monster.Y, EntityConstants.PlayerRadius, proj.X, proj.Y))
                {
                    ApplyDamageMonster(monster, proj.Damage, proj.OwnerId);
                    proj.Alive = false;
                    break;
                }
            }

            nextProj: ;
        }
    }

    private void TickMonsters(float dt)
    {
        foreach (var monster in Monsters.Values)
        {
            if (!monster.Alive) continue;

            var (nearestPid, nearestDist) = FindNearestPlayer(monster);
            float speed = MonsterProperties.Speed[monster.MonsterType];
            float atkRange = MonsterProperties.AttackRange[monster.MonsterType];
            float detectRange = MonsterProperties.DetectRange[monster.MonsterType];

            if (nearestPid == -1)
            {
                MonsterWander(monster, dt, speed);
                continue;
            }

            if (nearestDist < detectRange)
            {
                monster.State = MonsterState.Chase;
                monster.TargetPlayerId = nearestPid;
            }
            else if (monster.State == MonsterState.Chase && nearestDist > detectRange * 1.5)
            {
                monster.State = MonsterState.Wander;
                monster.TargetPlayerId = -1;
            }

            if (monster.State == MonsterState.Wander)
            {
                MonsterWander(monster, dt, speed);
            }
            else if (monster.State == MonsterState.Chase && Players.TryGetValue(nearestPid, out var target) && target.Alive)
            {
                double dx = target.X - monster.X;
                double dy = target.Y - monster.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                UpdateFacing(monster, dx, dy);

                if (monster.MonsterType == MonsterType.Marauder && dist <= MarauderShootRange && dist > atkRange)
                    TryMarauderShoot(monster, target);

                if (dist < atkRange)
                {
                    if (monster.AttackCooldown <= 0)
                    {
                        int damage = MonsterProperties.MeleeDamage[monster.MonsterType];
                        ApplyDamagePlayer(target, damage, -1);
                        monster.AttackCooldown = MonsterProperties.AttackCooldown[monster.MonsterType];
                    }
                }
                else
                {
                    float nx = (float)(dx / dist);
                    float ny = (float)(dy / dist);
                    float newX = monster.X + nx * speed * dt;
                    float newY = monster.Y + ny * speed * dt;
                    if (CanMove(newX, monster.Y, EntityConstants.PlayerRadius)) monster.X = newX;
                    if (CanMove(monster.X, newY, EntityConstants.PlayerRadius)) monster.Y = newY;
                }
            }

            if (monster.AttackCooldown > 0)
                monster.AttackCooldown -= dt;
            if (monster.ShootCooldown > 0)
                monster.ShootCooldown -= dt;
        }
    }

    private void TryMarauderShoot(Monster monster, Player target)
    {
        if (monster.WeaponType == ItemType.Empty || monster.ShootCooldown > 0)
            return;

        monster.ShootCooldown = ItemProperties.WeaponCooldown[monster.WeaponType];
        double dx = target.X - monster.X;
        double dy = target.Y - monster.Y;
        double ang = Math.Atan2(dx, -dy) / (2.0 * Math.PI);
        float angle = (float)(ang - Math.Floor(ang));
        SpawnProjectile(monster.MonsterId, monster.WeaponType, monster.X, monster.Y, angle, fromMonster: true);
    }

    private (int pid, double dist) FindNearestPlayer(Monster monster)
    {
        int nearestPid = -1;
        double nearestDist = double.PositiveInfinity;
        foreach (var kv in Players)
        {
            if (!kv.Value.Alive) continue;
            double dx = kv.Value.X - monster.X;
            double dy = kv.Value.Y - monster.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearestPid = kv.Key;
            }
        }
        return (nearestPid, nearestDist);
    }

    private void MonsterWander(Monster monster, float dt, float speed)
    {
        monster.WanderTimer -= dt;
        if (monster.WanderTimer <= 0)
        {
            double angle = _rng.NextDouble() * 2.0 * Math.PI;
            float wanderSpeed = speed * 0.35f;
            monster.WanderDx = (float)(Math.Cos(angle) * wanderSpeed);
            monster.WanderDy = (float)(Math.Sin(angle) * wanderSpeed);
            monster.WanderTimer = (float)(_rng.NextDouble() * 2.0 + 1.0);
        }

        float newX = monster.X + monster.WanderDx * dt;
        float newY = monster.Y + monster.WanderDy * dt;
        if (!CanMove(newX, newY, EntityConstants.PlayerRadius))
            monster.WanderTimer = 0;
        else
        {
            monster.X = newX;
            monster.Y = newY;
        }
    }

    private static void UpdateFacing(Monster monster, double dx, double dy)
    {
        double ang = Math.Atan2(dx, -dy) / (2.0 * Math.PI);
        monster.Angle = (float)(ang - Math.Floor(ang));
    }

    private void TickExtraction(float dt)
    {
        foreach (var player in Players.Values.ToList())
        {
            if (!player.Alive)
                continue;

            if (!IsInExtractionZone(player.X, player.Y))
            {
                player.ExtractionTimer = 0;
                continue;
            }

            player.ExtractionTimer += dt;
            if (player.ExtractionTimer < NetworkConstants.ExtractionDuration)
                continue;

            int loot = player.CarriedLoot;
            int points = loot * NetworkConstants.PointsPerLoot;
            player.Hotbar.SetQuantity((int)HotbarSlot.Loot, 0);
            player.ExtractionTimer = 0;
            player.Alive = false;
            NotifyHotbarChanged(player);
            PlayerExtracted?.Invoke(player, points);
        }
    }

    private bool IsInExtractionZone(float x, float y)
    {
        foreach (var zone in ExtractionZones)
        {
            if (zone.Contains(x, y))
                return true;
        }
        return false;
    }

    private void ApplyDamagePlayer(Player player, int damage, int attackerId)
    {
        if (!player.Alive) return;
        player.Hp -= damage;
        if (player.Hp > 0) return;

        player.Hp = 0;
        player.Alive = false;

        if (attackerId >= 0 && Players.ContainsKey(attackerId))
        {
            Player attacker = Players[attackerId];
            attacker.Kills += 1;
            ServerLogger.LogPlayerKill(RoomId, attacker.PlayerId, attacker.Username, player.PlayerId, player.Username, "player");
        }
        else
        {
            ServerLogger.LogPlayerDeath(RoomId, player.PlayerId, player.Username, "environment");
        }
    }

    private void ApplyDamageMonster(Monster monster, int damage, int killerPid)
    {
        if (!monster.Alive) return;
        monster.Hp -= damage;
        if (monster.Hp > 0) return;

        monster.Hp = 0;
        monster.Alive = false;
        if (killerPid >= 0 && Players.ContainsKey(killerPid))
        {
            Players[killerPid].Kills += 1;
            string monsterType = monster.MonsterType == MonsterType.Zombie ? "zombie" : "marauder";
            ServerLogger.LogPlayerKill(RoomId, killerPid, Players[killerPid].Username, monster.MonsterId, $"monster_{monster.MonsterId}", monsterType);
        }

        DropEnemyLoot(monster);
    }

    private void DropEnemyLoot(Monster monster)
    {
        if (monster.MonsterType == MonsterType.Zombie)
        {
            SpawnLoot(ItemType.Loot, _rng.Next(1, 4), monster.X, monster.Y);
            return;
        }

        SpawnLoot(ItemType.Loot, _rng.Next(1, 3), monster.X, monster.Y);
        ItemType ammo = monster.WeaponType switch
        {
            ItemType.Pistol => ItemType.AmmoPistol,
            ItemType.Rifle => ItemType.AmmoRifle,
            ItemType.Shotgun => ItemType.AmmoShotgun,
            _ => ItemType.AmmoPistol,
        };
        SpawnLoot(ammo, _rng.Next(4, 12), monster.X + 8, monster.Y);
    }

    private ItemType PickMarauderWeapon()
    {
        ItemType[] weapons = { ItemType.Pistol, ItemType.Rifle, ItemType.Shotgun };
        return weapons[_rng.Next(weapons.Length)];
    }

    private void SpawnMonster(MonsterType type, float x, float y, ItemType weapon = ItemType.Empty)
    {
        int mid = _nextMonsterId++;
        Monsters[mid] = new Monster(mid, type, x, y, weapon);
    }

    private void SpawnProjectile(int ownerId, ItemType wtype, float x, float y, float angle, bool fromMonster = false)
    {
        int pid = _nextProjId++;
        Projectiles[pid] = new Projectile(pid, ownerId, wtype, x, y, angle, fromMonster);
    }

    private void SpawnLoot(ItemType itemType, int quantity, float x, float y)
    {
        int lid = _nextLootId++;
        LootItems[lid] = new LootItem(lid, itemType, quantity, x, y);
    }

    private bool CanMove(float wx, float wy, int radius)
    {
        foreach (var (ox, oy) in new (int, int)[] { (radius, 0), (-radius, 0), (0, radius), (0, -radius) })
        {
            var (tx, ty) = MapData.WorldToTile(wx + ox, wy + oy);
            if (MapData.BlocksMovement(Tiles, MapWidth, MapHeight, tx, ty))
                return false;
        }
        return true;
    }

    private void CleanupDead()
    {
        foreach (var k in Projectiles.Where(kv => !kv.Value.Alive).Select(kv => kv.Key).ToList())
            Projectiles.Remove(k);
        foreach (var k in LootItems.Where(kv => !kv.Value.Alive).Select(kv => kv.Key).ToList())
            LootItems.Remove(k);
        foreach (var k in Monsters.Where(kv => !kv.Value.Alive).Select(kv => kv.Key).ToList())
            Monsters.Remove(k);
    }

    private void NotifyHotbarChanged(Player player) => HotbarChanged?.Invoke(player);

    private static bool CirclePoint(float cx, float cy, int radius, float px, float py)
    {
        float dx = px - cx;
        float dy = py - cy;
        return dx * dx + dy * dy < radius * radius;
    }
}
