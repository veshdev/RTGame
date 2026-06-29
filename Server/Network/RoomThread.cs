using System;
using System.Diagnostics;
using System.Threading;
using Server.World;

namespace Server.Network;

internal class RoomThread : IDisposable
{
    private readonly Room _room;
    private Thread? _thread;
    private volatile bool _running;
    private readonly object _lock = new();

    public RoomThread(Room room)
    {
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _running = false;
        _thread = null;
    }

    public bool Start()
    {
        lock (_lock)
        {
            if (_running || _thread != null)
                return false;

            _running = true;
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = $"Room-{_room.RoomId}"
            };
            _thread.Start();
            return true;
        }
    }

    public void Stop(TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(2);

        lock (_lock)
        {
            _running = false;
        }

        if (_thread != null && _thread.IsAlive)
        {
            _thread.Join(timeout);
        }
    }

    private void Run()
    {
        if (_room.World is not GameWorld world)
        {
            _running = false;
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        double tickInterval = 1.0 / NetworkConstants.ServerTickRate;
        double nextTick = stopwatch.Elapsed.TotalSeconds;

        while (_running && world.Running)
        {
            double now = stopwatch.Elapsed.TotalSeconds;
            if (now >= nextTick)
            {
                try
                {
                    world.Tick();
                    nextTick += tickInterval;

                    if (stopwatch.Elapsed.TotalSeconds > nextTick + tickInterval * 10)
                        nextTick = stopwatch.Elapsed.TotalSeconds + tickInterval;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Room] Tick error room {_room.RoomId}: {ex.Message}");
                    _running = false;
                }
            }
            else
            {
                double sleepTime = nextTick - now;
                if (sleepTime > 0.0005)
                    Thread.Sleep((int)(sleepTime * 800));
            }
        }

        _running = false;
    }

    public void Dispose()
    {
        Stop();
    }
}
