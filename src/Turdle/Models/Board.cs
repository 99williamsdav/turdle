namespace Turdle.Models;

public interface ITile
{
    TileStatus Status { get; }
    int Position { get; }
}

public class Board : IBoard<Board.Row, Board.Tile>
{
    private readonly List<Row> _rows = new List<Row>();
    public Row[] Rows { get; private set; } = Array.Empty<Row>();
    public List<PointAdjustment> CurrentRowPointsAdjustments { get; private set; } = new List<PointAdjustment>();

    public HashSet<LetterPosition> CorrectLetters { get; } = new HashSet<LetterPosition>();
    public HashSet<LetterPosition> PresentLetters { get; } = new HashSet<LetterPosition>();
    public HashSet<char> AbsentLetters { get; } = new HashSet<char>();
    public Dictionary<char, int> PresentLetterCounts { get; } = new Dictionary<char, int>();
    public Dictionary<char, TileStatus> LetterStatuses { get; } = new Dictionary<char, TileStatus>();
    
    public DateTime StartTime { get; } = DateTime.Now;
    public BoardStatus Status { get; private set; } = BoardStatus.Playing;
    public bool IsFinished => Status is BoardStatus.Failed or BoardStatus.Solved;
    public int? SolvedOrder { get; private set; }
    public double? CompletionTimeMs { get; set; }
    public int Points { get; private set; }
    public int Rank { get; set; } = 1;
    public bool IsJointRank { get; set; } = true;

    // Used for hydrating other player's boards with letters that we already know about
    public Dictionary<string, char> KnownLetterStatusHashes { get; } = new Dictionary<string, char>();

    public Dictionary<string, Tile[]> KnownWordHashes { get; } = new Dictionary<string, Tile[]>();

    public event EventHandler PointsUpdated;

    public MaskedBoard Mask() => new MaskedBoard
    {
        Rows = _rows.Select(x => x.Mask()).ToArray(),
        Status = Status,
        SolvedOrder = SolvedOrder,
        Points = Points,
        CurrentRowPoints = CurrentRowPointsAdjustments.Sum(x => x.Points),
        Rank = Rank,
        IsJointRank = IsJointRank,
        CompletionTimeMs = CompletionTimeMs
    };
    

    public List<TileError> AddRow(string guess, string correctAnswer, int? playedOrder, int? solvedCount,
        int playerCount, IPointService? pointService = null, (string, int)[]? similarWords = null, bool wasForced = false, int maxGuesses = 6)
    {
        if (guess.Length != correctAnswer.Length)
            throw new InvalidOperationException($"Word must be {correctAnswer.Length} letters");
        if (_rows.Count == maxGuesses)
            throw new InvalidOperationException("Board is already complete");
        
        guess = guess.ToUpper();
        var row = new Row(guess, correctAnswer, playedOrder, _rows.Count + 1, CurrentRowPointsAdjustments, wasForced);
        CurrentRowPointsAdjustments = new List<PointAdjustment>();
        _rows.Add(row);
        Rows = _rows.ToArray();

        var errors = GetRowErrors(row);
        row.SetErrors(errors.ToArray());

        var rowPresentLetters = new List<char>();
        foreach (var tile in row.Tiles)
        {
            switch (tile.Status)
            {
                case TileStatus.Correct:
                    CorrectLetters.Add(tile.LetterPosition);
                    rowPresentLetters.Add(tile.Letter);
                    LetterStatuses[tile.Letter] = TileStatus.Correct;
                    break;
                case TileStatus.Present:
                    PresentLetters.Add(tile.LetterPosition);
                    rowPresentLetters.Add(tile.Letter);
                    if (!LetterStatuses.TryGetValue(tile.Letter, out _))
                        LetterStatuses[tile.Letter] = TileStatus.Present;
                    break;
                case TileStatus.Absent:
                    AbsentLetters.Add(tile.Letter);
                    if (!LetterStatuses.TryGetValue(tile.Letter, out _))
                        LetterStatuses[tile.Letter] = TileStatus.Absent;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (GameParameters.ShowKnownOpponentTiles)
                KnownLetterStatusHashes[tile.StatusHash] = tile.Letter;
            if (GameParameters.ShowKnownOpponentWords)
                KnownWordHashes[row.WordHash] = row.Tiles;
        }

        foreach (var letterGrp in rowPresentLetters.GroupBy(x => x))
        {
            if (PresentLetterCounts.TryGetValue(letterGrp.Key, out var count))
                PresentLetterCounts[letterGrp.Key] = Math.Max(count, letterGrp.Count());
            else
                PresentLetterCounts[letterGrp.Key] = letterGrp.Count();
        }

        if (row.IsCorrect)
        {
            Status = BoardStatus.Solved;
            SolvedOrder = solvedCount.Value + 1;
            CompletionTimeMs = (DateTime.Now - StartTime).TotalMilliseconds;
        }
        else if (_rows.Count == maxGuesses)
            Status = BoardStatus.Failed;

        if (pointService != null)
        {
            var rowPoints = pointService.GetPointsForGuess(row, SolvedOrder, playerCount);
            row.AddPointsAdjustments(rowPoints);
            RecalculatePoints();
        }

        return errors;
    }

    public void GiveUp()
    {
        if (Status != BoardStatus.Playing)
            throw new InvalidOperationException($"Cannot give up while status is {Status}");

        Status = BoardStatus.Failed;
    }

    public void AddPointAdjustment(PointAdjustmentReason reason, int points)
    {
        CurrentRowPointsAdjustments.Add(new PointAdjustment(reason, points));
        RecalculatePoints();
    }

    public void RevealAbsentLetter(char letter)
    {
        AbsentLetters.Add(letter);
        LetterStatuses[letter] = TileStatus.Absent;
    }

    public void RevealPresentLetter(char letter)
    {
        PresentLetters.Add(new LetterPosition(letter, null));
        
        if (PresentLetterCounts.TryGetValue(letter, out var count))
            PresentLetterCounts[letter] = count + 1;
        else
            PresentLetterCounts[letter] = 1;
        
        if (!LetterStatuses.TryGetValue(letter, out _))
            LetterStatuses[letter] = TileStatus.Present;
    }

    private List<TileError> GetRowErrors(Row row)
    {
        var errors = new List<TileError>();
        foreach (var tile in row.Tiles)
        {
            if (tile.Status == TileStatus.Correct)
                continue;
            if (PresentLetters.Contains(tile.LetterPosition))
                errors.Add(new TileError(tile.LetterPosition, HardModeError.PresentLetterPlayedInSamePlace));
            if (tile.Status == TileStatus.Absent && AbsentLetters.Contains(tile.Letter))
                errors.Add(new TileError(tile.LetterPosition, HardModeError.AbsentLetterPlayed));
            if (CorrectLetters.Any(lp => lp.Position == tile.Position))
            {
                var correctLetterPosition = CorrectLetters.Single(lp => lp.Position == tile.Position);
                errors.Add(new TileError(correctLetterPosition, HardModeError.CorrectLetterMissed));
            }
        }

        foreach (var letterGrp in PresentLetterCounts)
        {
            var presentCount = row.Tiles.Count(x => x.Letter == letterGrp.Key);
            if (presentCount < letterGrp.Value)
            {
                var diff = letterGrp.Value - presentCount;
                // missed correct letters already covered, so subtract number of those from new errors
                var existingErrorCount = errors.Where(x => x.Error == HardModeError.CorrectLetterMissed).Count(x => x.LetterPosition?.Letter == letterGrp.Key);
                for (var i = 0; i < diff - existingErrorCount; i++)
                {
                    errors.Add(new TileError(new LetterPosition(letterGrp.Key, null), HardModeError.PresentLetterMissed));
                }
            }
        }

        return errors;
    }

    private void RecalculatePoints()
    {
        var prevPoints = Points;
        var currentRowAdjustments = CurrentRowPointsAdjustments.Sum(x => x.Points);
        Points = _rows.Sum(x => x.PointsAwarded) + currentRowAdjustments;
        if (Points != prevPoints)
            PointsUpdated?.Invoke(this, EventArgs.Empty);
    }
    
    public class Row : IRow<Tile>
    {
        public Tile[] Tiles { get; private set; }
        public bool IsCorrect { get; private set; }
        public TileError[] Errors { get; private set; }
        public DateTime PlayedAt { get; } = DateTime.Now;
        
        // null if irrelevant because answer was found by another player earlier 
        public int? PlayedOrder { get; }
        public PointAdjustment[] PointsAdjustments { get; private set; }
        public int PointsAwarded { get; private set; }
        public int GuessNumber { get; private set; }
        public bool WasForced { get; private set; }
        public string WordHash => this.ToString().GetHashCode().ToString("X");
        
        public (string, int)[]? SimilarWords { get; private set; }

        public Row(string guess, string correctAnswer, int? playedOrder, int guessNumber, 
            IList<PointAdjustment> inPlayPointAdjustments, 
            bool wasForced = false, (string, int)[]? similarWords = null)
        {
            PlayedOrder = playedOrder;
            GuessNumber = guessNumber;
            PointsAdjustments = inPlayPointAdjustments.ToArray();
            PointsAwarded = inPlayPointAdjustments.Sum(x => x.Points);
            WasForced = wasForced;
            SimilarWords = similarWords;
            
            var correctLetters = guess.Where((c, i) => c == correctAnswer[i]).ToArray();
            var presentLetters = new List<char>();
            
            Tiles = guess.Select((c, i) =>
            {
                var instance = guess.Substring(0, i + 1).Count(x => x == c);
                var letterPosition = new LetterPosition(c, i);
                
                if (c == correctAnswer[i])
                    return new Tile(letterPosition, TileStatus.Correct);
                if (correctAnswer.Contains(c))
                {
                    var answerLetterCount = correctAnswer.Count(x => x == c);
                    if (answerLetterCount > presentLetters.Count(x => x == c) + correctLetters.Count(x => x == c))
                    {
                        presentLetters.Add(c);
                        return new Tile(letterPosition, TileStatus.Present);
                    }
                }
                    
                return new Tile(letterPosition, TileStatus.Absent);
            }).ToArray();

            IsCorrect = guess == correctAnswer;
        }

        public MaskedBoard.MaskedRow Mask() => new MaskedBoard.MaskedRow
        {
            Tiles = Tiles.Select(x => x.Mask()).ToArray(),
            IsCorrect = IsCorrect,
            PlayedAt = PlayedAt,
            GuessNumber = GuessNumber,
            PlayedOrder = PlayedOrder,
            PointsAwarded = PointsAwarded,
            PointsAdjustments = PointsAdjustments.Select(x => x.Mask()).ToArray(),
            WasForced = WasForced,
            WordHash = WordHash
        };

        public void SetErrors(TileError[] errors)
        {
            Errors = errors;
        }

        public void AddPointsAdjustments(IEnumerable<PointAdjustment> adjustments)
        {
            PointsAdjustments = PointsAdjustments.Concat(adjustments).ToArray();
            PointsAwarded = PointsAdjustments.Sum(x => x.Points);
        }
        
        public override string ToString()
        {
            return new string(Tiles.Select(x => x.Letter).ToArray());
        }
    }

    // Instance: what instance of this letter in the word was it
    public record struct LetterPosition(char Letter, int? Position);

    public record TileError(LetterPosition? LetterPosition, HardModeError Error)
    {
        public override string ToString()
        {
            // TODO humanize
            return LetterPosition != null ? $"{Error} ({LetterPosition?.Letter})" : Error.ToString();
        }

        public string ToMaskedString() => Error.ToString();
    }

    public record PointAdjustment(PointAdjustmentReason Reason, int Points, string? Description = null,
        string? MaskedDescription = null)
    {
        public PointAdjustment Mask() => new PointAdjustment(Reason, Points, MaskedDescription);
    }

    public record Tile(LetterPosition LetterPosition, TileStatus Status) : ITile
    {
        public MaskedBoard.MaskedTile Mask() => new(Position, Status, StatusHash);
        public char Letter => LetterPosition.Letter;
        public int Position => LetterPosition.Position.Value;

        public string StatusHash => GetStatusHash();

        public string GetStatusHash()
        {
            switch (Status)
            {
                case TileStatus.None:
                case TileStatus.Correct:
                case TileStatus.Present:
                    return $"{Letter}{Status}{Position}".GetHashCode().ToString("X");
                case TileStatus.Absent:
                    return $"{Letter}{Status}".GetHashCode().ToString("X");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString()
        {
            return $"{Letter} ({Status})";
        }
    }
}

public enum BoardStatus
{
    Playing,
    Solved,
    Failed
}

public enum TileStatus
{
    None,
    Correct,
    Present,
    Absent
}

public enum HardModeError
{
    AbsentLetterPlayed,
    PresentLetterPlayedInSamePlace,
    CorrectLetterMissed,
    PresentLetterMissed
}

public enum PointAdjustmentReason
{
    GuessSuggested,
    WordNotPlayedInTime,
    AbsentLetterRevealed,
    PresentLetterRevealed,
    CorrectSolutionOrder,
    CorrectSolutionGuessNumber,
    ValidGuessOrder,
    HardModeError // TODO split out into types?
}