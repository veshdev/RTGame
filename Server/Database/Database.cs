using System;
using Npgsql;

namespace Server;

internal static class Database
{
    private static readonly string _connectionString = "Host=localhost;Port=5432;Database=rtgame;Username=postgres;Password=zxc;Pooling=true";

    public static void Initialize()
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS accounts (
                    id SERIAL PRIMARY KEY,
                    username TEXT NOT NULL UNIQUE,
                    password_hash TEXT NOT NULL,
                    points INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_accounts_username ON accounts (username);", conn);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] Initialize error: {ex.Message}");
        }
    }

    public static (string? passwordHash, int points)? FindAccount(string username)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT password_hash, points FROM accounts WHERE username = @u", conn);
            cmd.Parameters.AddWithValue("u", username);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read())
                return null;

            return (rdr.GetString(0), rdr.GetInt32(1));
        }
        catch (PostgresException ex)
        {
            Logger.Error($"[DB] FindAccount Postgres error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] FindAccount error: {ex.Message}");
            return null;
        }
    }

    public static RegisterResult RegisterAccount(string username, string passwordHash)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();
            using var cmd = new NpgsqlCommand("INSERT INTO accounts (username, password_hash, points) VALUES (@u, @p, 0)", conn, tran);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("p", passwordHash);
            cmd.ExecuteNonQuery();
            tran.Commit();
            return RegisterResult.Success;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return RegisterResult.UsernameTaken;
        }
        catch (PostgresException ex)
        {
            Logger.Error($"[DB] RegisterAccount Postgres error: {ex.Message}");
            return RegisterResult.DatabaseError;
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] RegisterAccount error: {ex.Message}");
            return RegisterResult.DatabaseError;
        }
    }

    public static int GetPoints(string username)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT points FROM accounts WHERE username = @u", conn);
            cmd.Parameters.AddWithValue("u", username);
            var res = cmd.ExecuteScalar();
            return res == null ? 0 : Convert.ToInt32(res);
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] GetPoints error: {ex.Message}");
            return 0;
        }
    }

    public static bool UpdatePoints(string username, int points)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("UPDATE accounts SET points = points + @p WHERE username = @u RETURNING points", conn);
            cmd.Parameters.AddWithValue("p", points);
            cmd.Parameters.AddWithValue("u", username);
            var res = cmd.ExecuteScalar();
            if (res == null)
            {
                Logger.Warn($"[DB] UpdatePoints: user not found: {username}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] UpdatePoints error: {ex.Message}");
            return false;
        }
    }

    private static NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}

internal enum RegisterResult
{
    Success,
    UsernameTaken,
    DatabaseError
}
