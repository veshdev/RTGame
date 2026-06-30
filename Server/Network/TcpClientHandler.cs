using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Server.Accounts;
using Server.World;

namespace Server.Network;

public interface IGameServer
{
    RoomManager RoomManager { get; }
    AccountStore Accounts { get; }
    int RegisterPlayer(object tcpHandler, string username);
    void UnregisterPlayer(int playerId);
    string? StartWorld(Room room, uint matchSeed);
}

internal class TcpClientHandler
{
    private readonly TcpClient _client;
    private readonly string _address;
    private readonly IGameServer _server;
    private readonly object _sendLock = new();

    public int PlayerId { get; set; } = -1;
    public string Username { get; set; } = "";
    public bool Running { get; set; } = true;

    public TcpClientHandler(TcpClient client, string address, IGameServer server)
    {
        _client = client;
        _address = address;
        _server = server;
    }

    public void Run()
    {
        Logger.Info($"[TCP] Client connected: {_address}");
        try
        {
            while (Running)
            {
                var (msgType, payload) = RecvMessage();
                if (msgType == null)
                    break;
                Dispatch(msgType.Value, payload ?? Array.Empty<byte>());
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Logger.Error($"[TCP] Handler error: {ex.Message}");
        }
        finally
        {
            OnDisconnect();
        }
    }

    public void Send(byte msgType, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        byte[] data = PacketHelpers.PackTcp(msgType, payload);
        lock (_sendLock)
        {
            try
            {
                _client.GetStream().Write(data, 0, data.Length);
                _client.GetStream().Flush();
            }
            catch (ObjectDisposedException) { Running = false; }
            catch (IOException) { Running = false; }
        }
    }

    public void SendError(string reason)
    {
        byte[] reasonBytes = Encoding.UTF8.GetBytes(reason);
        if (reasonBytes.Length > 63)
            Array.Resize(ref reasonBytes, 63);
        byte[] payload = new byte[1 + reasonBytes.Length];
        payload[0] = (byte)reasonBytes.Length;
        Buffer.BlockCopy(reasonBytes, 0, payload, 1, reasonBytes.Length);
        Send(TcpMsg.S_ERROR, payload);
    }

    public void SendExtracted(int pointsEarned, int totalPoints)
    {
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), pointsEarned);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), totalPoints);
        Send(TcpMsg.S_EXTRACTED, payload);
    }

    public void SendHotbarSnapshot(byte playerId, byte[] quantities)
    {
        byte[] payload = new byte[1 + quantities.Length];
        payload[0] = playerId;
        Buffer.BlockCopy(quantities, 0, payload, 1, quantities.Length);
        Send(TcpMsg.S_HOTBAR_SNAPSHOT, payload);
    }

    private (byte? msgType, byte[]? payload) RecvMessage()
    {
        byte[]? header = RecvExactly(DataSizes.TcpHeaderSize);
        if (header == null)
            return (null, null);

        var (length, msgType) = PacketHelpers.UnpackTcpHeader(header);
        int payloadLen = length - 1;
        if (payloadLen < 0)
            return (null, null);

        byte[]? payload = payloadLen > 0 ? RecvExactly(payloadLen) : Array.Empty<byte>();
        return payload == null ? (null, null) : (msgType, payload);
    }

    private byte[]? RecvExactly(int n)
    {
        if (n <= 0)
            return Array.Empty<byte>();

        byte[] buf = new byte[n];
        int bytesRead = 0;
        while (bytesRead < n)
        {
            try
            {
                int chunk = _client.GetStream().Read(buf, bytesRead, n - bytesRead);
                if (chunk == 0)
                    return null;
                bytesRead += chunk;
            }
            catch (IOException)
            {
                return null;
            }
        }
        return buf;
    }

    private void Dispatch(byte msgType, byte[] payload)
    {
        switch (msgType)
        {
            case TcpMsg.C_LOGIN: HandleLogin(payload, register: false); break;
            case TcpMsg.C_REGISTER: HandleLogin(payload, register: true); break;
            case TcpMsg.C_ROOM_LIST_REQ: HandleRoomList(); break;
            case TcpMsg.C_ROOM_CREATE: HandleRoomCreate(payload); break;
            case TcpMsg.C_ROOM_JOIN: HandleRoomJoin(payload); break;
            case TcpMsg.C_ROOM_LEAVE: HandleRoomLeave(); break;
            case TcpMsg.C_READY_TOGGLE: HandleReadyToggle(); break;
            case TcpMsg.C_HOST_START: HandleHostStart(); break;
            case TcpMsg.C_HOTBAR_REQUEST: HandleHotbarRequest(); break;
            case TcpMsg.C_DISCONNECT: Running = false; break;
        }
    }

    private void HandleLogin(byte[] payload, bool register)
    {
        if (payload.Length < 2)
        {
            SendError("bad_login");
            return;
        }

        byte version = payload[0];
        if (version != NetworkConstants.ProtocolVersion)
        {
            Send(TcpMsg.S_LOGIN_REJECT, PacketHelpers.PackString("version_mismatch", DataSizes.ReasonMax));
            Running = false;
            return;
        }

        int offset = 1;
        var (username, next) = PacketHelpers.UnpackString(payload, offset);
        offset = next;
        string password = "";
        if (offset < payload.Length)
        {
            var (pwd, pwdOffset) = PacketHelpers.UnpackString(payload, offset);
            password = pwd;
            offset = pwdOffset;
        }

        username = username.Substring(0, Math.Min(username.Length, DataSizes.UsernameMax)).Trim();
        password = password.Substring(0, Math.Min(password.Length, DataSizes.PasswordMax));

        bool ok;
        string? error;
        Account? account;
        if (register)
            ok = _server.Accounts.TryRegister(username, password, out account, out error);
        else
            ok = _server.Accounts.TryLogin(username, password, out account, out error);

        if (!ok || account == null)
        {
            Send(TcpMsg.S_LOGIN_REJECT, PacketHelpers.PackString(error ?? "login_failed", DataSizes.ReasonMax));
            Running = false;
            return;
        }

        int playerId = _server.RegisterPlayer(this, username);
        if (playerId < 0)
        {
            Send(TcpMsg.S_LOGIN_REJECT, PacketHelpers.PackString("server_full", DataSizes.ReasonMax));
            Running = false;
            return;
        }

        PlayerId = playerId;
        Username = username;
        Logger.Event($"LOGIN: player_id={playerId} username={username} address={_address}");

        byte[] ack = new byte[5];
        ack[0] = (byte)playerId;
        BinaryPrimitives.WriteInt32LittleEndian(ack.AsSpan(1, 4), account.Points);
        Send(TcpMsg.S_LOGIN_ACK, ack);
    }

    private void HandleRoomList()
    {
        Send(TcpMsg.S_ROOM_LIST, _server.RoomManager.SerializeRoomList());
    }

    private void HandleRoomCreate(byte[] payload)
    {
        if (PlayerId < 0) { SendError("not_logged_in"); return; }

        int offset = 0;
        var (roomName, newOffset) = PacketHelpers.UnpackString(payload, offset);
        offset = newOffset;
        string password = "";
        if (offset < payload.Length)
        {
            var (pwd, pwdOffset) = PacketHelpers.UnpackString(payload, offset);
            password = pwd;
        }

        Room? room = _server.RoomManager.CreateRoom(roomName, password, PlayerId, Username, this);
        if (room == null) 
        { 
            SendError("server_full"); 
            return; 
        }

        Logger.Event($"ROOM_CREATE: room_id={room.RoomId} name={roomName} host_id={PlayerId} host={Username}");

        Send(TcpMsg.S_ROOM_JOINED, new byte[] { (byte)room.RoomId, 1 });
        BroadcastLobbyState(room);
    }

    private void HandleRoomJoin(byte[] payload)
    {
        if (PlayerId < 0) { SendError("not_logged_in"); return; }
        if (payload.Length < 1) { SendError("bad_join"); return; }

        int roomId = payload[0];
        int offset = 1;
        string password = "";
        if (offset < payload.Length)
        {
            var (pwd, _) = PacketHelpers.UnpackString(payload, offset);
            password = pwd;
        }

        Room? room = _server.RoomManager.GetRoom(roomId);
        if (room == null) { SendError("room_not_found"); return; }

        string? err = room.Join(PlayerId, Username, password, this);
        if (err != null) { SendError(err); return; }

        byte isHost = (byte)(PlayerId == room.HostPlayerId ? 1 : 0);
        Send(TcpMsg.S_ROOM_JOINED, new byte[] { (byte)room.RoomId, isHost });
        BroadcastLobbyState(room);
    }

    private void HandleRoomLeave()
    {
        Room? room = _server.RoomManager.FindRoomOfPlayer(PlayerId);
        if (room == null)
            return;

        room.Leave(PlayerId);
        Send(TcpMsg.S_ROOM_LEFT, Array.Empty<byte>());

        if (room.Slots.Count == 0)
            _server.RoomManager.RemoveRoomDelayed(room.RoomId, 1000);
        else
            BroadcastLobbyState(room);
    }

    private void HandleReadyToggle()
    {
        Room? room = _server.RoomManager.FindRoomOfPlayer(PlayerId);
        if (room == null) { SendError("not_in_room"); return; }
        room.ToggleReady(PlayerId);
        BroadcastLobbyState(room);
    }

    private void HandleHostStart()
    {
        Room? room = _server.RoomManager.FindRoomOfPlayer(PlayerId);
        if (room == null) { SendError("not_in_room"); return; }

        string? err = room.CanStart(PlayerId);
        if (err != null) { SendError(err); return; }

        uint matchSeed = (uint)new Random().Next(1, int.MaxValue);
        room.StartMatch(matchSeed);

        byte[] seedBytes = BitConverter.GetBytes(matchSeed);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(seedBytes);

        BroadcastToRoom(room, TcpMsg.S_MATCH_STARTING, seedBytes);

        string? mapError = _server.StartWorld(room, matchSeed);
        if (mapError != null)
        {
            BroadcastToRoom(room, TcpMsg.S_ERROR, BuildErrorPayload($"map_error: {mapError}"));
            BroadcastLobbyState(room);
            return;
        }

        if (room.World is not GameWorld gameWorld)
        {
            room.CancelMatchStart();
            SendError("world_not_created");
            BroadcastLobbyState(room);
            return;
        }

        byte[] mapData = MapData.SerializeMap(gameWorld.Tiles, gameWorld.MapWidth, gameWorld.MapHeight, gameWorld.MapHash);
        BroadcastToRoom(room, TcpMsg.S_MATCH_STARTED, mapData);
        Logger.Event($"MATCH_START: room_id={room.RoomId} players={room.PlayerCount}");
    }

    private static byte[] BuildErrorPayload(string reason)
    {
        byte[] reasonBytes = Encoding.UTF8.GetBytes(reason);
        if (reasonBytes.Length > 63)
            Array.Resize(ref reasonBytes, 63);
        byte[] payload = new byte[1 + reasonBytes.Length];
        payload[0] = (byte)reasonBytes.Length;
        Buffer.BlockCopy(reasonBytes, 0, payload, 1, reasonBytes.Length);
        return payload;
    }

    private void HandleHotbarRequest()
    {
        Room? room = _server.RoomManager.FindRoomOfPlayer(PlayerId);
        if (room?.World is not GameWorld gameWorld) { SendError("not_in_game"); return; }
        if (!gameWorld.Players.TryGetValue(PlayerId, out Player? player) || player == null)
        {
            SendError("player_not_found");
            return;
        }

        SendHotbarSnapshot((byte)PlayerId, player.Hotbar.ToBytes());
    }

    private void BroadcastLobbyState(Room room)
    {
        BroadcastToRoom(room, TcpMsg.S_LOBBY_STATE, room.SerializeLobbyState());
    }

    private void BroadcastToRoom(Room room, byte msgType, byte[] payload)
    {
        foreach (LobbySlot slot in room.Slots)
        {
            if (slot.TcpConn is TcpClientHandler handler)
                handler.Send(msgType, payload);
        }

        if (room.World is GameWorld gameWorld)
        {
            foreach (Player player in gameWorld.Players.Values)
            {
                if (player.TcpConn is TcpClientHandler playerHandler)
                    playerHandler.Send(msgType, payload);
            }
        }
    }

    private void OnDisconnect()
    {
        Logger.Event($"DISCONNECT: player_id={PlayerId} username={Username}");
        Running = false;
        try { _client.Close(); } catch (ObjectDisposedException) { }

        if (PlayerId >= 0)
        {
            _server.Accounts.Logout(Username);
        }

        if (PlayerId < 0)
            return;

        Room? room = _server.RoomManager.FindRoomOfPlayer(PlayerId);
        if (room != null)
        {
            room.Leave(PlayerId);
            if (room.Slots.Count == 0 && room.Players.Count == 0)
                _server.RoomManager.RemoveRoomDelayed(room.RoomId, 1000);
            else if (room.Slots.Count > 0)
                BroadcastLobbyState(room);
        }

        _server.UnregisterPlayer(PlayerId);
    }
}
