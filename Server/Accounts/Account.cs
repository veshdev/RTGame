namespace Server.Accounts;

public class Account(string username, string password, int points = 0)
{
    public string Username { get; set; } = username;
    public string Password { get; set; } = password;
    public int Points { get; set; } = points;
}