using System;

namespace Server;

internal static class ServerLogger
{
    public static void LogPlayerLogin(int playerId, string username, string address)
    {
        Logger.Event($"LOGIN: player_id={playerId} username={username} address={address}");
    }

    public static void LogPlayerDisconnect(int playerId, string username)
    {
        Logger.Event($"DISCONNECT: player_id={playerId} username={username}");
    }

    public static void LogRoomCreate(int roomId, string roomName, int hostPid, string hostUsername)
    {
        Logger.Event($"ROOM_CREATE: room_id={roomId} name={roomName} host_id={hostPid} host={hostUsername}");
    }

    public static void LogRoomDestroy(int roomId)
    {
        Logger.Event($"ROOM_DESTROY: room_id={roomId}");
    }

    public static void LogMatchStart(int roomId, int playerCount)
    {
        Logger.Event($"MATCH_START: room_id={roomId} players={playerCount}");
    }

    public static void LogPlayerDeath(int roomId, int playerId, string playerName, string cause)
    {
        Logger.Event($"PLAYER_DEATH: room_id={roomId} player_id={playerId} player={playerName} cause={cause}");
    }

    public static void LogPlayerKill(int roomId, int killerId, string killerName, int targetId, string targetName, string targetType)
    {
        Logger.Event($"PLAYER_KILL: room_id={roomId} killer_id={killerId} killer={killerName} target_id={targetId} target={targetName} target_type={targetType}");
    }

    public static void LogPlayerExtraction(int roomId, int playerId, string playerName, int points)
    {
        Logger.Event($"EXTRACTION: room_id={roomId} player_id={playerId} player={playerName} points={points}");
    }
}
