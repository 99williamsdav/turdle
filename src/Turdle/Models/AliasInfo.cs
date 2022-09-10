namespace Turdle.Models;

public record AliasInfo(string? Alias, AliasInfo.GameStatus Status)
{
    public enum GameStatus
    {
        NotRegistered,
        RegisteredConnected,
        RegisteredDisconnected
    }
}