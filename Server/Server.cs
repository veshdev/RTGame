using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.Accounts;
using Server.Network;
using Server.World;

namespace Server;

public class ServerState : IGameServer
{
    private readonly Dictionary<int, string> _players = new();
    private int _nextPid = 1;
    private readonly object _playersLock = new();
    public string MapPath { get; }

    public RoomManager RoomManager { get; } = new();
    public AccountStore Accounts { get; } = new();

    public ServerState(string mapPath)
    {
        MapPath = mapPath;
    }

    public int RegisterPlayer(object tcpHandler, string username)
    {
        lock (_playersLock)
        {
            if (_players.Count >= NetworkConstants.MaxPlayers * 8)
                return -1;

            int pid = _nextPid++;
            _players[pid] = username;
            return pid;
        }
    }

    public void UnregisterPlayer(int playerId)
    {
        lock (_playersLock)
        {
            _players.Remove(playerId);
        }
    }

    public string? StartWorld(Room room, uint matchSeed)
    {
        try
        {
            List<Player> playerList = room.Players.Values.ToList();
            LoadedMap map = MapLoader.Load(MapPath);
            var world = new GameWorld(room.RoomId, map, matchSeed, playerList);
            world.PlayerExtracted += (player, points) => OnPlayerExtracted(room, player, points);
            world.HotbarChanged += player => OnHotbarChanged(player);
            world.AllPlayersGone += () => OnAllPlayersGone(room);
            room.SetWorld(world);
            Logger.Info($"[Server] World created for room {room.RoomId}, {playerList.Count} players, map={MapPath}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Server] Map load failed for room {room.RoomId}: {ex.Message}");
            room.CancelMatchStart();
            return ex.Message;
        }
    }

    private void OnHotbarChanged(Player player)
    {
        player.TcpConn?.SendHotbarSnapshot((byte)player.PlayerId, player.Hotbar.ToBytes());
    }

    private void OnPlayerExtracted(Room room, Player player, int points)
    {
        Accounts.AddPoints(player.Username, points);
        int total = Accounts.GetPoints(player.Username);

        player.TcpConn?.SendExtracted(points, total);

        room.World?.Players.Remove(player.PlayerId);

        room.RemoveMatchPlayer(player.PlayerId);
        ServerLogger.LogPlayerExtraction(room.RoomId, player.PlayerId, player.Username, points);
        if (room.PlayerCount == 0)
            RoomManager.RemoveRoom(room.RoomId);
    }

    private void OnAllPlayersGone(Room room)
    {
        ServerLogger.LogRoomDestroy(room.RoomId);
        RoomManager.RemoveRoom(room.RoomId);
    }
}

public class Server
{
    private static void TcpListen(ServerState serverState, string host, int port)
    {
        try
        {
            TcpListener srvSock = new(IPAddress.Parse(host), port);
            srvSock.Start(16);
            Logger.Info($"[TCP] Listening on {host}:{port}");

            while (true)
            {
                try
                {
                    TcpClient conn = srvSock.AcceptTcpClient();
                    conn.NoDelay = true;
                    string addr = conn.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    var handler = new TcpClientHandler(conn, addr, serverState);
                    new Thread(handler.Run) { IsBackground = true, Name = $"TCP-Handler-{addr}" }.Start();
                }
                catch (Exception ex)
                {
                    Logger.Error($"[TCP] Accept error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[TCP] Listener error: {ex.Message}");
        }
    }

    private static void GameLoop(ServerState serverState, UdpHandler udpHandler)
    {
        Logger.Info($"[Server] Game loop starting (UDP broadcast) at {NetworkConstants.ServerTickRate} Hz");
        Stopwatch stopwatch = Stopwatch.StartNew();
        double tickInterval = NetworkConstants.TickDt;
        double nextTick = stopwatch.Elapsed.TotalSeconds;

        while (true)
        {
            double now = stopwatch.Elapsed.TotalSeconds;
            if (now >= nextTick)
            {
                // Broadcast snapshots from all active room threads
                udpHandler.BroadcastSnapshots();
                nextTick += tickInterval;

                if (stopwatch.Elapsed.TotalSeconds > nextTick + tickInterval * 10)
                    nextTick = stopwatch.Elapsed.TotalSeconds + tickInterval;
            }
            else
            {
                double sleepTime = nextTick - stopwatch.Elapsed.TotalSeconds;
                if (sleepTime > 0.0005)
                    Thread.Sleep((int)(sleepTime * 800));
            }
        }
    }

    public static void Main(string[]? args)
    {
        string host = args != null && args.Length > 0 ? args[0] : "0.0.0.0";
        string mapPath = args != null && args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "Maps", "default.map");

        Logger.Info("============================================================");
        Logger.Info("Game Server");
        Logger.Info($"TCP:{NetworkConstants.TcpPort}  UDP:{NetworkConstants.UdpPort}  Tick:{NetworkConstants.ServerTickRate}Hz");
        Logger.Info($"Map: {mapPath}");
        Logger.Info("============================================================");

        ServerState serverState = new(mapPath);
        MapValidationResult mapCheck = MapLoader.Validate(mapPath);
        if (!mapCheck.Ok)
        {
            Logger.Warn($"[Server] WARNING: Map validation failed: {mapCheck.Error}");
        }
        else
        {
            Logger.Info($"[Server] Map validated: {mapPath}");
        }

        UdpHandler udpHandler = new(serverState, host);

        new Thread(() => TcpListen(serverState, host, NetworkConstants.TcpPort))
        {
            IsBackground = true,
            Name = "TCP-Listener"
        }.Start();

        try
        {
            GameLoop(serverState, udpHandler);
        }
        catch (OperationCanceledException)
        {
            Logger.Info("\n[Server] Shutting down.");
            udpHandler.Dispose();
        }
    }
}
