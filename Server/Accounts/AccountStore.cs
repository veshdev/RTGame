using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Server.Accounts;

public sealed class AccountStore
{
    private readonly string _path;
    private readonly Dictionary<string, Account> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public AccountStore(string path = "accounts.txt")
    {
        _path = path;
        Load();
    }

    public bool TryLogin(string username, string password, out Account? account, out string? error)
    {
        lock (_lock)
        {
            if (!_accounts.TryGetValue(username, out Account? found))
            {
                account = null;
                error = "invalid_credentials";
                return false;
            }

            if (found.Password != password)
            {
                account = null;
                error = "invalid_credentials";
                return false;
            }

            account = found;
            error = null;
            return true;
        }
    }

    public bool TryRegister(string username, string password, out Account? account, out string? error)
    {
        lock (_lock)
        {
            account = null;
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

            if (_accounts.ContainsKey(username))
            {
                error = "username_taken";
                return false;
            }

            account = new Account(username, password, 0);
            _accounts[username] = account;
            Save();
            error = null;
            return true;
        }
    }

    public void AddPoints(string username, int points)
    {
        if (points <= 0) return;

        lock (_lock)
        {
            if (_accounts.TryGetValue(username, out Account? account))
            {
                account.Points += points;
                Save();
            }
        }
    }

    public int GetPoints(string username)
    {
        lock (_lock)
        {
            return _accounts.TryGetValue(username, out Account? account) ? account.Points : 0;
        }
    }

    private bool TryGet(string username, out Account? account)
    {
        return _accounts.TryGetValue(username, out account);
    }

    private static bool IsValidUsername(string username) =>
        !string.IsNullOrWhiteSpace(username) && username.Length <= 16;

    private static bool IsValidPassword(string password) =>
        !string.IsNullOrEmpty(password) && password.Length <= 64;

    private void Load()
    {
        if (!File.Exists(_path)) return;

        foreach (string line in File.ReadAllLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split(';', 3);
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[2], out int points)) continue;
            _accounts[parts[0]] = new Account(parts[0], parts[1], points);
        }
    }

    private void Save()
    {
        IEnumerable<string> lines = _accounts.Values
            .OrderBy(a => a.Username, StringComparer.OrdinalIgnoreCase)
            .Select(a => $"{a.Username};{a.Password};{a.Points}");
        File.WriteAllLines(_path, lines);
    }
}
