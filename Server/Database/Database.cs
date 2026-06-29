using System;
using Npgsql;

namespace Server
{
    internal static class Database
    {
        private static readonly string _connectionString = "Host=localhost;Port=5432;Database=rtgame;Username=postgres;Password=zxc;Pooling=true";

        public static NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

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
    }
}
