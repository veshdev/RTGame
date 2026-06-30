namespace Server.Accounts;

public sealed class AccountStore
{
    private readonly object _lock = new();
    private readonly HashSet<string> _loggedInUsers = new();

    public AccountStore()
    {
        Database.Initialize();
    }

    public bool TryLogin(string username, string password, out Account? account, out string? error)
    {
        account = null;
        error = null;

        lock (_lock)
        {
            if (_loggedInUsers.Contains(username))
            {
                error = "already_logged_in";
                return false;
            }
        }

        var accountData = Database.FindAccount(username);
        if (accountData == null)
        {
            error = "invalid_credentials";
            return false;
        }

        var (storedHash, points) = accountData.Value;
        if (storedHash != password)
        {
            error = "invalid_credentials";
            return false;
        }

        lock (_lock)
        {
            _loggedInUsers.Add(username);
        }

        account = new Account(username, storedHash, points);
        return true;
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

        var result = Database.RegisterAccount(username, password);
        switch (result)
        {
            case RegisterResult.Success:
                account = new Account(username, password, 0);
                return true;
            case RegisterResult.UsernameTaken:
                error = "username_taken";
                return false;
            case RegisterResult.DatabaseError:
                error = "db_error";
                return false;
            default:
                error = "db_error";
                return false;
        }
    }

    public void AddPoints(string username, int points)
    {
        if (points <= 0) return;
        Database.UpdatePoints(username, points);
    }

    public int GetPoints(string username)
    {
        return Database.GetPoints(username);
    }

    public void Logout(string username)
    {
        lock (_lock)
        {
            _loggedInUsers.Remove(username);
        }
    }

    private static bool IsValidUsername(string username) =>
        !string.IsNullOrWhiteSpace(username) && username.Length <= 16;

    private static bool IsValidPassword(string password) =>
        !string.IsNullOrEmpty(password) && password.Length <= 64;
}
