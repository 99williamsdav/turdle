using Turdle.Models;

namespace Turdle.Persistence;

public class RoomStateSnapshot
{
    public string RoomCode { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string? ImagePath { get; set; }
    public string? AdminConnectionId { get; set; }
    public List<string> AdminConnections { get; set; } = new();
    public List<string> TvConnections { get; set; } = new();
    public List<string> UsedBotPersonalities { get; set; } = new();
    public List<ChatMessage> ChatMessages { get; set; } = new();
    public GameParameters GameParameters { get; set; } = GameParameters.GetDefault();
    public InternalRoundStateSnapshot InternalRoundState { get; set; } = new();
    public List<InternalRoundStateSnapshot> PreviousRoundStates { get; set; } = new();
}

public class InternalRoundStateSnapshot
{
    public string CorrectAnswer { get; set; } = string.Empty;
    public RoundStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int RoundNumber { get; set; }
    public int MaxGuesses { get; set; }
    public double DefaultTimeLimitSeconds { get; set; }
    public List<PlayerSnapshot> Players { get; set; } = new();
}

public class PlayerSnapshot
{
    public string Alias { get; set; } = string.Empty;
    public int Points { get; set; }
    public int Rank { get; set; }
    public bool IsJointRank { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool IsConnected { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string? AvatarPath { get; set; }
    public bool Ready { get; set; }
    public bool IsBot { get; set; }
    public string? BotPersonality { get; set; }
}
