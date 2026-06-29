using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.World;

namespace Server.Network;

public class LobbySlot
{
    public int PlayerId { get; set; }
    public string Username { get; set; }
    public bool Ready { get; set; }
    internal TcpClientHandler? TcpConn { get; set; }

    internal LobbySlot(int playerId, string username, TcpClientHandler? tcpConn)
    {
        PlayerId = playerId;
        Username = username ?? string.Empty;
        Ready = false;
        TcpConn = tcpConn;
    }
}

public class Room : IDisposable
{
    public int RoomId { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }
    public RoomState State { get; set; }
    public uint Seed { get; set; }
    public int HostPlayerId { get; set; }

    private readonly List<LobbySlot> _slots;
    private readonly Dictionary<int, Player> _players;
    private RoomThread? _thread;

    internal GameWorld? World { get; set; }

    internal Room(int roomId, string name, string password,
                int hostPlayerId, string hostUsername, TcpClientHandler? hostTcp)
    {
        RoomId = roomId;
        Name = name.Length > DataSizes.RoomNameMax ? 
               name.Substring(0, DataSizes.RoomNameMax) : name;
        Password = password.Length > DataSizes.RoomPasswordMax ?
                   password.Substring(0, DataSizes.RoomPasswordMax) : password;
        State = RoomState.Lobby;
        Seed = 0;

        _slots = new List<LobbySlot>
        {
            new LobbySlot(hostPlayerId, hostUsername, hostTcp)
        };
        HostPlayerId = hostPlayerId;

        _players = new Dictionary<int, Player>();
    }

    public int PlayerCount => _slots.Count;

    public bool IsFull => _slots.Count >= NetworkConstants.MaxPlayers;

    public bool HasPassword() => Password.Length > 0;

    public IReadOnlyList<LobbySlot> Slots => _slots.AsReadOnly();

    public IReadOnlyDictionary<int, Player> Players => _players;

    internal string? Join(int playerId, string username, string password, TcpClientHandler? tcpConn)
    {
        if (State != RoomState.Lobby)
            return "match_already_started";

        if (IsFull)
            return "room_full";

        if (HasPassword() && password != Password)
            return "wrong_password";

        if (_slots.Any(s => s.PlayerId == playerId))
            return "already_in_room";

        _slots.Add(new LobbySlot(playerId, username, tcpConn));
        return null;
    }

    public void Leave(int playerId)
    {
        _slots.RemoveAll(s => s.PlayerId == playerId);

        if (_slots.Count > 0 && HostPlayerId == playerId)
        {
            HostPlayerId = _slots[0].PlayerId;
        }
    }

    public bool ToggleReady(int playerId)
    {
        foreach (var slot in _slots)
        {
            if (slot.PlayerId == playerId)
            {
                slot.Ready = !slot.Ready;
                return slot.Ready;
            }
        }
        return false;
    }

    public string? CanStart(int requesterPid)
    {
        if (requesterPid != HostPlayerId)
            return "not_host";

        if (State != RoomState.Lobby)
            return "not_in_lobby";

        if (_slots.Count < 1)
            return "no_players";

        if (_slots.Count > 1)
        {
            bool allNonHostReady = _slots
                .Where(s => s.PlayerId != HostPlayerId)
                .All(s => s.Ready);

            if (!allNonHostReady)
                return "players_not_ready";
        }

        return null;
    }

    public void StartMatch(uint seed)
    {
        Seed = seed;
        State = RoomState.Starting;

        _players.Clear();
        foreach (var slot in _slots)
        {
            var player = new Player(slot.PlayerId, slot.Username);
            player.TcpConn = slot.TcpConn;
                _players[slot.PlayerId] = player;
                }
            }

            internal void SetWorld(GameWorld? world)
            {
                World = world;
                State = RoomState.InGame;

                // Start the room's game thread
                if (world != null)
                {
                    _thread = new RoomThread(this);
                    _thread.Start();
                }
            }

    public void RemoveMatchPlayer(int playerId)
    {
        _players.Remove(playerId);
        Leave(playerId);
    }

    public void CancelMatchStart()
    {
        State = RoomState.Lobby;
        Seed = 0;
        _players.Clear();
        World = null;
        _thread?.Stop();
        _thread = null;
    }

    // Serialization formats for TCP messages
    // S_LOBBY_STATE: [slot_count(1)][host_id(1)][slot0...]
    // slot: [player_id(1)][ready(1)][username_len(1)][username...]
    public byte[] SerializeLobbyState()
    {
        var parts = new List<byte[]>();

        // Header: slot count and host id
        parts.Add(new byte[] { (byte)_slots.Count, (byte)HostPlayerId });

        // Each slot
        foreach (var slot in _slots)
        {
            byte[] nameEnc = Encoding.UTF8.GetBytes(slot.Username);
            if (nameEnc.Length > DataSizes.UsernameMax)
                Array.Resize(ref nameEnc, DataSizes.UsernameMax);

            var slotData = new List<byte>();
            slotData.Add((byte)slot.PlayerId);
            slotData.Add(slot.Ready ? (byte)1 : (byte)0);
            slotData.Add((byte)nameEnc.Length);
            slotData.AddRange(nameEnc);

            parts.Add(slotData.ToArray());
        }

        // Concatenate all parts
        int totalLen = parts.Sum(p => p.Length);
        byte[] result = new byte[totalLen];
        int offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    // Compact entry for room list: [id(1)][count(1)][has_pw(1)][name_len(1)][name...]
    public byte[] ToListEntry()
    {
        byte[] nameEnc = Encoding.UTF8.GetBytes(Name);
        if (nameEnc.Length > DataSizes.RoomNameMax)
            Array.Resize(ref nameEnc, DataSizes.RoomNameMax);

        var result = new List<byte>();
        result.Add((byte)RoomId);
        result.Add((byte)PlayerCount);
        result.Add(HasPassword() ? (byte)1 : (byte)0);
        result.Add((byte)nameEnc.Length);
        result.AddRange(nameEnc);

        return result.ToArray();
    }

    public void Dispose()
    {
        _thread?.Dispose();
        _thread = null;
        World = null;
    }
}


public class RoomManager
{
    private readonly Dictionary<int, Room> _rooms;
    private int _nextRoomId;
    private readonly object _lock = new();

    public RoomManager()
    {
        _rooms = new Dictionary<int, Room>();
        _nextRoomId = 1;
    }

    public IReadOnlyDictionary<int, Room> Rooms
    {
        get
        {
            lock (_lock)
            {
                return _rooms.AsReadOnly();
            }
        }
    }

    internal Room? CreateRoom(string name, string password,
                           int hostPid, string hostUsername, TcpClientHandler? hostTcp)
    {
        lock (_lock)
        {
            if (_rooms.Count >= NetworkConstants.MaxRooms)
                return null;

            int roomId = _nextRoomId++;
            var room = new Room(roomId, name, password, hostPid, hostUsername, hostTcp);
            _rooms[roomId] = room;
            return room;
        }
    }

    public Room? GetRoom(int roomId)
    {
        lock (_lock)
        {
            _rooms.TryGetValue(roomId, out var room);
            return room;
        }
    }

    public void RemoveRoom(int roomId)
    {
        lock (_lock)
        {
            if (_rooms.TryGetValue(roomId, out var room))
            {
                room.Dispose();
                _rooms.Remove(roomId);
            }
        }
    }

    public Room? FindRoomOfPlayer(int playerId)
    {
        lock (_lock)
        {
            foreach (var room in _rooms.Values)
            {
                // Check lobby slots
                if (room.Slots.Any(s => s.PlayerId == playerId))
                    return room;

                // Check in-game players
                if (room.Players.ContainsKey(playerId))
                    return room;
            }

            return null;
        }
    }

    // Serialize open lobby rooms for C_ROOM_LIST_REQ: [room_count(1)][room0...]
    public byte[] SerializeRoomList()
    {
        lock (_lock)
        {
            var lobbies = _rooms.Values
                .Where(r => r.State == RoomState.Lobby)
                .ToList();

            var parts = new List<byte[]>();

            // Header: room count
            parts.Add(new byte[] { (byte)lobbies.Count });

            // Each room entry
            foreach (var room in lobbies)
            {
                parts.Add(room.ToListEntry());
            }

            // Concatenate all parts
            int totalLen = parts.Sum(p => p.Length);
            byte[] result = new byte[totalLen];
            int offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }
    }
}
