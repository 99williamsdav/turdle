using Turdle.Bots;

namespace Turdle.Models;

// TODO rename to represent that its scope is per game?
public class Player : IPlayer<Board, Board.Row, Board.Tile>
{
    public string Alias { get; set; }

    public IBot? Bot { get; private set; }
    public bool IsBot => Bot != null;
    
    public int Points { get; set; }
    public int Rank { get; set; } = 1;
    public bool IsJointRank { get; set; } = true;
    public string ConnectionId { get; set; }
    public string? IpAddress { get; set; }
    public bool IsConnected { get; set; }
    public DateTime RegisteredAt { get; set; }

    public string? AvatarPath { get; set; }
    
    public bool Ready { get; set; }
    public Board? Board { get; set; }

    public Player(string alias, string connectionId, string ipAddress, string? avatarPath = null)
    {
        Alias = alias;
        ConnectionId = connectionId;
        IpAddress = ipAddress;
        IsConnected = true;
        RegisteredAt = DateTime.Now;
        AvatarPath = avatarPath;
    }

    public void SetAvatarPath(string? avatarPath)
    {
        AvatarPath = avatarPath;
    }

    public Player(string alias, IBot bot, string? avatarPath = null)
    {
        Alias = alias;
        Bot = bot;
        ConnectionId = $"{alias}{Guid.NewGuid()}";
        IsConnected = true;
        RegisteredAt = DateTime.Now;
        AvatarPath = avatarPath;
    }

    public Player CopyForNewGame()
    {
        return Bot == null
            ? new Player(Alias, ConnectionId, IpAddress!, AvatarPath)
            {
                Points = Points,
                Rank = Rank,
                IsJointRank = IsJointRank,
                IsConnected = IsConnected,
                RegisteredAt = RegisteredAt
            }
            : new Player(Alias, Bot, AvatarPath)
            {
                Points = Points,
                Rank = Rank,
                ConnectionId = ConnectionId,
                IsJointRank = IsJointRank,
                IsConnected = IsConnected,
                RegisteredAt = RegisteredAt
            };
    }

    public MaskedPlayer Mask()
    {
        return new MaskedPlayer
        {
            Alias = Alias,
            IsBot = IsBot,
            Points = Points,
            Rank = Rank,
            IsJointRank = IsJointRank,
            IsConnected = IsConnected,
            ConnectionId = ConnectionId,
            AvatarPath = AvatarPath,
            Ready = Ready,
            Board = Board?.Mask()
        };
    }
}

public interface IPlayer<out TBoard, out TRow, out TTile> 
    where TBoard : IBoard<TRow, TTile>
    where TRow : IRow<TTile>
    where TTile : ITile
{
    string Alias { get; }
    bool IsBot { get; }
    int Points { get; }
    int Rank { get; }
    bool IsJointRank { get; }
    string ConnectionId { get; }
    bool IsConnected { get; }
    string? AvatarPath { get; }
    bool Ready { get; }
    TBoard? Board { get; }
}

public class MaskedPlayer : IPlayer<MaskedBoard, MaskedBoard.MaskedRow, MaskedBoard.MaskedTile>
{
    public string Alias { get; set; }
    public bool IsBot { get; set; }

    public int Points { get; set; }
    public int Rank { get; set; } = 1;
    public bool IsJointRank { get; set; } = true;
    public string ConnectionId { get; set; }
    public bool IsConnected { get; set; }

    public string? AvatarPath { get; set; }

    public bool Ready { get; set; }
    public MaskedBoard? Board { get; set; }
}