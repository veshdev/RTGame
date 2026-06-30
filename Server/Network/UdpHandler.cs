using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Server.World;

namespace Server.Network;

internal class UdpHandler : IDisposable
{
    private const int UdpBufSize = 2048;

    private readonly ServerState _serverState;
    private readonly UdpClient _udpClient;
    private Thread _listenerThread;
    private volatile bool _running;

    private readonly Dictionary<IPEndPoint, int> _addrToPid;
    private readonly object _addrLock = new();

    public UdpHandler(ServerState serverState, string host = "0.0.0.0")
    {
        _serverState = serverState ?? throw new ArgumentNullException(nameof(serverState));
        _addrToPid = new Dictionary<IPEndPoint, int>();

        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(host), NetworkConstants.UdpPort));
        _udpClient.Client.ReceiveTimeout = 1000;

        _running = true;
        _listenerThread = new Thread(Run) { IsBackground = true, Name = "UDP-Listener" };
        _listenerThread.Start();

        Logger.Info($"[UDP] Listening on {host}:{NetworkConstants.UdpPort}");
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
            catch (SocketException) when (_running)
            {
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _udpClient?.Close();
    }

    private void HandleDatagram(byte[] data, IPEndPoint addr)
    {
        if (data == null || data.Length == 0)
            return;

        byte msgType = data[0];

        if (msgType == UdpMsg.C_INPUT)
        {
            if (data.Length < 2)
                return;

            int pid = data[1];
            byte[] inputPayload = new byte[data.Length - 1];
            inputPayload[0] = UdpMsg.C_INPUT;
            Buffer.BlockCopy(data, 2, inputPayload, 1, Math.Max(0, data.Length - 2));

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

            Room? room = GetRoomOfPlayer(pid);
            if (room?.World is GameWorld world)
            {
                world.ApplyInput(pid, inp.Value);
            }
        }
    }

    private void RegisterPlayerAddr(int pid, IPEndPoint addr)
    {
        Room? room = GetRoomOfPlayer(pid);
        if (room == null)
            return;

        var udpAddr = (addr.Address.ToString(), addr.Port);

        if (room.World is GameWorld world && world.Players.TryGetValue(pid, out Player? worldPlayer) && worldPlayer != null)
        {
            worldPlayer.UdpAddr = udpAddr;
        }

        if (room.Players.TryGetValue(pid, out Player? roomPlayer) && roomPlayer != null)
        {
            roomPlayer.UdpAddr = udpAddr;
        }
    }

    private Room? GetRoomOfPlayer(int pid)
    {
        return _serverState.RoomManager.FindRoomOfPlayer(pid);
    }

    public void BroadcastSnapshots()
    {
        foreach (var room in _serverState.RoomManager.Rooms.Values)
        {
            if (room.World is not GameWorld world)
                continue;

            foreach (var (pid, player) in world.Players)
            {
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
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        if (_listenerThread != null && _listenerThread.IsAlive)
        {
            _listenerThread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
