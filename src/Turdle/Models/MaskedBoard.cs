namespace Turdle.Models;

public interface IBoard<out TRow, out TTile> 
    where TRow : IRow<TTile>
    where TTile : ITile
{
    BoardStatus Status { get; }
    TRow[] Rows { get; }
    int? SolvedOrder { get; }
    int Points { get; }
    int Rank { get; }
    bool IsJointRank { get; }
    double? CompletionTimeMs { get; }
    bool IsFinished { get; }
    DateTime[] GuessDeadlines { get; }
    TimeSpan? GuessTimeLimit { get; }
    double? GuessTimeLimitMs { get; }
    DateTime? NextGuessDeadline { get; }
    int CurrentExpectedGuessCount { get; }
}

public interface IRow<out TTile>
    where TTile : ITile
{
    TTile[] Tiles { get; }
    bool IsCorrect { get; }
    DateTime PlayedAt { get; }
    int GuessNumber { get; }
    int? PlayedOrder { get; }
    int PointsAwarded { get; }
    bool WasForced { get; }
    Board.PointAdjustment[] PointsAdjustments { get; }
}

public class MaskedBoard : IBoard<MaskedBoard.MaskedRow, MaskedBoard.MaskedTile>
{
    public BoardStatus Status { get; set; }
    public MaskedRow[] Rows { get; set; }
    public int? SolvedOrder { get; set; }
    public int Points { get; set; }
    public int CurrentRowPoints { get; set; }
    public int Rank { get; set; }
    public bool IsJointRank { get; set; }
    public double? CompletionTimeMs { get; set; }
    public DateTime[] GuessDeadlines { get; set; }
    public TimeSpan? GuessTimeLimit { get; set; }
    public double? GuessTimeLimitMs => GuessTimeLimit?.TotalMilliseconds;
    public DateTime? NextGuessDeadline { get; set; }
    public int CurrentExpectedGuessCount { get; set; }

    public bool IsFinished => Status is BoardStatus.Failed or BoardStatus.Solved;

    public class MaskedRow : IRow<MaskedTile>
    {
        public MaskedTile[] Tiles { get; set; }
        public bool IsCorrect { get; set; }
        public DateTime PlayedAt { get; set; }
        public int GuessNumber { get; set; }
        public int? PlayedOrder { get; set; }
        public int PointsAwarded { get; set; }
        public bool WasForced { get; set; }
        public Board.PointAdjustment[] PointsAdjustments { get; set; }
        public string WordHash { get; set; }
    }

    public record MaskedTile(int Position, TileStatus Status, string? StatusHash) : ITile;
}