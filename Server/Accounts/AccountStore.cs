using System;
using Npgsql;

namespace Server.Accounts;

public sealed class AccountStore
{
    private readonly object _lock = new();

    public AccountStore()
    {
        Database.Initialize();
    }

    public bool TryLogin(string username, string password, out Account? account, out string? error)
    {
        account = null;
        error = null;
        try
        {
            using var conn = Database.CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT password_hash, points FROM accounts WHERE username = @u", conn);
            cmd.Parameters.AddWithValue("u", username);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read())
            {
                error = "invalid_credentials";
                return false;
            }

            string stored = rdr.GetString(0);
            int points = rdr.GetInt32(1);
            if (stored != password)
            {
                error = "invalid_credentials";
                return false;
            }

            account = new Account(username, stored, points);
            return true;
        }
        catch (PostgresException ex)
        {
            Logger.Error($"[DB] Login Postgres error: {ex.Message}");
            error = "db_error";
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] Login error: {ex.Message}");
            error = "db_error";
            return false;
        }
    }

    public bool TryRegister(string username, string password, out Account? account, out string? error)
    {
        account = null;
        error = null;

        if (!IsValidUsername(username))
        {
            error = "bad_username";
            return false;
        }

        if (!IsValidPassword(password))
        {
            error = "bad_password";
            return false;
        }

        try
        {
            using var conn = Database.CreateConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();
            using var cmd = new NpgsqlCommand("INSERT INTO accounts (username, password_hash, points) VALUES (@u, @p, 0)", conn, tran);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("p", password);
            cmd.ExecuteNonQuery();
            tran.Commit();

            account = new Account(username, password, 0);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            error = "username_taken";
            return false;
        }
        catch (PostgresException ex)
        {
            Logger.Error($"[DB] Register Postgres error: {ex.Message}");
            error = "db_error";
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] Register error: {ex.Message}");
            error = "db_error";
            return false;
        }
    }

    public void AddPoints(string username, int points)
    {
        if (points <= 0) return;

        try
        {
            using var conn = Database.CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("UPDATE accounts SET points = points + @p WHERE username = @u RETURNING points", conn);
            cmd.Parameters.AddWithValue("p", points);
            cmd.Parameters.AddWithValue("u", username);
            var res = cmd.ExecuteScalar();
            if (res == null)
            {
                Logger.Warn($"[DB] AddPoints: user not found: {username}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[DB] AddPoints error: {ex.Message}");
        }
    }

    public int GetPoints(string username)
    {
        try
        {
            using var conn = Database.CreateConnection();
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

    private static bool IsValidUsername(string username) =>
        !string.IsNullOrWhiteSpace(username) && username.Length <= 16;

    private static bool IsValidPassword(string password) =>
        !string.IsNullOrEmpty(password) && password.Length <= 64;
}
