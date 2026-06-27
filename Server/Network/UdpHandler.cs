using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Server.World;

namespace Server.Network;

// =============================================================================
// Killfeed City - UDP Handler
// UDP handler: receive client input packets, send world snapshots.
// Runs in a dedicated thread. Maps UDP (ip, port) to PlayerId.
// C# port from server/net/udp_handler.py
// =============================================================================

internal class UdpHandler : IDisposable
{
    private const int UdpBufSize = 2048;

    private readonly ServerState _serverState;
    private readonly UdpClient _udpClient;
    private Thread _listenerThread;
    private volatile bool _running;

    // Map IPEndPoint -> playerId (thread-safe)
    private readonly Dictionary<IPEndPoint, int> _addrToPid;
    private readonly object _addrLock = new();

    public UdpHandler(ServerState serverState, string host = "0.0.0.0")
    {
        _serverState = serverState ?? throw new ArgumentNullException(nameof(serverState));
        _addrToPid = new Dictionary<IPEndPoint, int>();

        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(host), NetworkConstants.UdpPort));
        _udpClient.Client.ReceiveTimeout = 1000;  // 1 second timeout so thread can exit cleanly

        _running = true;
        _listenerThread = new Thread(Run) { IsBackground = true, Name = "UDP-Listener" };
        _listenerThread.Start();

        Console.WriteLine($"[UDP] Listening on {host}:{NetworkConstants.UdpPort}");
    }

    private void Run()
    {
        while (_running)
        {
            try
            {
                IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udpClient.Receive(ref remoteEp);
                HandleDatagram(data, remoteEp);
            }
            catch (SocketException)
            {
                if (!_running)
                    break;
                // Timeout, continue loop
                continue;
            }
            catch (ObjectDisposedException)
            {
                // Socket was closed
                break;
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _udpClient?.Close();
    }

    // ------------------------------------------------------------------
    // Receive (private)
    // ------------------------------------------------------------------
    private void HandleDatagram(byte[] data, IPEndPoint addr)
    {
        if (data == null || data.Length == 0)
            return;

        byte msgType = data[0];

        if (msgType == UdpMsg.C_INPUT)
        {
            // Layout: [type=0x01][1B pid][input_struct...]
            // The pid prefix lets the server map addr->pid before the
            // first packet is associated with a known player.
            if (data.Length < 2)
                return;

            int pid = data[1];

            // Reconstruct the input packet for unpacking: [type][payload]
            byte[] inputPayload = new byte[data.Length - 1];
            inputPayload[0] = UdpMsg.C_INPUT;
            if (data.Length > 2)
            {
                Buffer.BlockCopy(data, 2, inputPayload, 1, data.Length - 2);
            }

            UdpInputData? inp = UdpPacketHelpers.UnpackUdpInput(inputPayload);
            if (inp == null)
                return;

            lock (_addrLock)
            {
                if (!_addrToPid.ContainsKey(addr))
                {
                    _addrToPid[addr] = pid;
                    RegisterPlayerAddr(pid, addr);
                }
            }

            // Forward input to the appropriate room's world
            Room? room = GetRoomOfPlayer(pid);
            if (room?.World is GameWorld world)
            {
                world.ApplyInput(pid, inp.Value);
            }
        }
    }

    private void RegisterPlayerAddr(int pid, IPEndPoint addr)
    {
        // Store the UDP addr on the Player so snapshots can be sent back.
        Room? room = GetRoomOfPlayer(pid);
        if (room != null)
        {
            // Try to get player from room.World (GameWorld)
            if (room.World is GameWorld world)
            {
                if (world.Players.TryGetValue(pid, out Player? player) && player != null)
                {
                    player.UdpAddr = (addr.Address.ToString(), addr.Port);
                }
            }

            if (room.Players.TryGetValue(pid, out Player? roomPlayer) && roomPlayer != null)
            {
                roomPlayer.UdpAddr = (addr.Address.ToString(), addr.Port);
            }
        }
    }

    private Room? GetRoomOfPlayer(int pid)
    {
        return _serverState.RoomManager.FindRoomOfPlayer(pid);
    }

    // ------------------------------------------------------------------
    // Send snapshots to all players in all active worlds
    // ------------------------------------------------------------------
    public void BroadcastSnapshots()
    {
        // Called from the game loop thread each tick.
        try
        {
            foreach (var room in _serverState.RoomManager.Rooms.Values)
            {
                if (room.World is not GameWorld world)
                    continue;

                try
                {
                    foreach (var kvp in world.Players)
                    {
                        int pid = kvp.Key;
                        Player player = kvp.Value;

                        if (!player.UdpAddr.HasValue)
                            continue;

                        try
                        {
                            var (ip, port) = player.UdpAddr.Value;
                            byte[] snapshot = world.BuildSnapshotFor(pid);
                            _udpClient.Send(snapshot, snapshot.Length, ip, port);
                        }
                        catch (SocketException)
                        {
                            // Ignore send errors
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Room was disposed or players collection modified, skip
                }
            }
        }
        catch
        {
            // Ignore errors during broadcast
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        if (_listenerThread != null && _listenerThread.IsAlive)
        {
            _listenerThread.Join(new TimeSpan(0, 0, 2));  // 2 second timeout
        }
    }
}
