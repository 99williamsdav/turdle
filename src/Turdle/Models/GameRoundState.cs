using Microsoft.AspNetCore.SignalR;
using Turdle.Models.Exceptions;
using Turdle.Utils;

namespace Turdle.Models;

public interface IRoundState<out TPlayer, out TBoard, out TRow, out TTile>
    where TPlayer : IPlayer<TBoard, TRow, TTile>
    where TBoard : IBoard<TRow, TTile>
    where TRow : IRow<TTile>
    where TTile : ITile
{
    RoundStatus Status { get; }
    int WordLength { get; }
    int MaxGuesses { get; }
    TPlayer[] Players { get; }
    DateTime? StartTime { get; }
    DateTime? EndTime { get; }
    int RoundNumber { get; }
    DateTime[] GuessDeadlines { get; }
    TimeSpan? GuessTimeLimit { get; }
    double? GuessTimeLimitMs { get; }
    DateTime? NextGuessDeadline { get; }
    int CurrentExpectedGuessCount { get; }
}

public class MaskedRoundState : IRoundState<MaskedPlayer, MaskedBoard, MaskedBoard.MaskedRow, MaskedBoard.MaskedTile>
{
    public RoundStatus Status { get; set; }
    public MaskedPlayer[] Players { get; set; }
    public int WordLength { get; set; }
    public int MaxGuesses { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int RoundNumber { get; set; }
    public DateTime[] GuessDeadlines { get; set; }
    public TimeSpan? GuessTimeLimit { get; set; }
    public double? GuessTimeLimitMs => GuessTimeLimit?.TotalMilliseconds;
    public DateTime? NextGuessDeadline { get; set; }
    public int CurrentExpectedGuessCount { get; set; }
}

public class InternalRoundState : IRoundState<Player, Board, Board.Row, Board.Tile>
{
    private readonly List<Player> _players = new List<Player>();
    private readonly IPointService _pointService;
    public Player[] Players { get; private set; } = Array.Empty<Player>();
    
    public string CorrectAnswer { get; }
    public int WordLength => CorrectAnswer.Length;
    public int MaxGuesses { get; private set; }
    
    public RoundStatus Status { get; set; } = RoundStatus.Waiting;
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    
    public TimeSpan? GuessTimeLimit { get; private set; }
    public double? GuessTimeLimitMs => GuessTimeLimit?.TotalMilliseconds;
    public DateTime[] GuessDeadlines { get; private set; }
    public DateTime? NextGuessDeadline { get; private set; }
    public int CurrentExpectedGuessCount { get; private set; } = GameParameters.FirstExpectedGuess - 1;

    public Dictionary<string, Board> BoardsByAlias { get; } = new Dictionary<string, Board>();
    
    public int RoundNumber { get; set; }

    public Board GetBoard(string alias) => BoardsByAlias[alias];

    public void SetBoard(string alias, Board board) => BoardsByAlias[alias] = board;

    public InternalRoundState(string correctAnswer, IPointService pointService)
    {
        CorrectAnswer = correctAnswer;
        _pointService = pointService;
        RoundNumber = 1;
        MaxGuesses = GameParameters.MaxGuesses;
    }

    public InternalRoundState(string correctAnswer, IEnumerable<Player> previousPlayers, int roundNumber, IPointService pointService)
    {
        CorrectAnswer = correctAnswer;
        RoundNumber = roundNumber;
        _pointService = pointService;
        _players = previousPlayers.Select(x => x.CopyForNewGame()).ToList();
        Players = _players.ToArray();
        Status = RoundStatus.Ready;
        RecalculateRanking();
        MaxGuesses = GameParameters.MaxGuesses;
    }

    public Player RemovePlayer(string alias)
    {
        var player = _players.Single(x => x.Alias == alias);
        BoardsByAlias.Remove(alias);
        _players.Remove(player);
        Players = _players.ToArray();
        
        if (Status == RoundStatus.Playing && _players.All(x => x.Board?.Status is BoardStatus.Solved or BoardStatus.Failed))
            Finish();
        
        return player;
    }

    public Board PlayGuess(string alias, string guess, int guessNumber, (string, int)[]? similarWords = null)
    {
        if (Status != RoundStatus.Playing)
            throw new HubException("Cannot play guess when game is not in-play.");
        
        var board = GetBoard(alias);
        var guessNum = board.Rows.Length + 1;
        if (guessNum != guessNumber)
            throw new HubException($"Guess number ({guessNumber}) is out of sync with server ({guessNum}). Rejecting guess.");
        
        var playedOrder = _players.Any(x => x.Board?.Status == BoardStatus.Solved && x.Board?.Rows.Length < guessNum)
            ? (int?)null : _players.Count(x => x.Board?.Rows.Length >= guessNum) + 1;
        var solvedCount = _players.Count(x => x.Board?.Status == BoardStatus.Solved);
        
        board.AddRow(guess, CorrectAnswer, playedOrder, solvedCount, _players.Count, _pointService, maxGuesses: MaxGuesses);
        
        if (board.IsFinished && _players.All(x => x.Board?.IsFinished == true))
            Finish();

        return board;
    }

    public string? SuggestGuess(WordService wordService, string alias)
    {
        var board = GetBoard(alias);
        var suggestedGuess = GetSuggestedGuess(wordService, board);
        if (suggestedGuess == null)
            return null;
        
        var pointCost = _pointService.GetPointCostForSuggestedGuess();
        if (pointCost > 0)
            board.AddPointAdjustment(PointAdjustmentReason.GuessSuggested, -pointCost);

        return suggestedGuess;
    }

    public Player[] ForceGuess(WordService wordService)
    {
        NextGuessDeadline = DateTime.Now + GuessTimeLimit.Value;
        if (CurrentExpectedGuessCount < MaxGuesses)
            CurrentExpectedGuessCount++;
        
        var suggestedGuessPointCost = _pointService.GetPointCostForSuggestedGuess();
        var slowPlayers = _players.Where(x => x.Board != null && !x.Board.IsFinished && x.Board.Rows.Length < CurrentExpectedGuessCount).ToArray();
        foreach (var player in slowPlayers)
        {
            var suggestedGuess = GetSuggestedGuess(wordService, player.Board);
            if (CurrentExpectedGuessCount < MaxGuesses && suggestedGuess != null)
            {
                if (suggestedGuessPointCost != 0)
                    player.Board.AddPointAdjustment(PointAdjustmentReason.GuessSuggested, -suggestedGuessPointCost);
                player.Board.AddRow(suggestedGuess, CorrectAnswer, null, null, _players.Count, wasForced: true);
            }
            else
            {
                RevealAbsentLetter(board: player.Board);
            }
        }

        return slowPlayers;
    }

    private string? GetSuggestedGuess(WordService wordService, Board board)
    {
        var validGuesses = wordService.GetPossibleValidGuesses(board, WordLength).Except(Enumerable.Repeat(CorrectAnswer, 1)).ToArray();
        if (!validGuesses.Any())
            return null;
        // TODO:
        // rank and return least-beneficial suggestion
        // potentially allow invalid words

        return validGuesses.PickRandom();
    }

    public Board RevealAbsentLetter(string? alias = null, Board? board = null)
    {
        if (Status != RoundStatus.Playing)
            throw new HubException("Cannot reveal absent letter when game is not in-play.");
        
        if (board == null)
            board = GetBoard(alias);
        var remainingAbsentLetters = Const.Alphabet.Except(board.AbsentLetters).Except(CorrectAnswer).ToArray();
        if (remainingAbsentLetters.Length == 0)
            return board;
        
        board.RevealAbsentLetter(remainingAbsentLetters.PickRandom());
        var pointCost = _pointService.GetPointCostForRevealingAbsentLetter();
        if (pointCost != 0)
            board.AddPointAdjustment(PointAdjustmentReason.AbsentLetterRevealed, -pointCost);

        return board;
    }

    public Board RevealPresentLetter(string? alias = null, Board? board = null)
    {
        if (Status != RoundStatus.Playing)
            throw new HubException("Cannot reveal absent letter when game is not in-play.");
        
        if (board == null)
            board = GetBoard(alias);

        var remainingPresentLetters = CorrectAnswer.ToList();
        foreach (var kvp in board.PresentLetterCounts)
        {
            for (var i = 0; i < kvp.Value; i++)
                remainingPresentLetters.Remove(kvp.Key);
        }
        
        if (remainingPresentLetters.Count == 0)
            return board;
        
        board.RevealPresentLetter(remainingPresentLetters.PickRandom());
        var pointCost = _pointService.GetPointCostForRevealingPresentLetter();
        if (pointCost != 0)
            board.AddPointAdjustment(PointAdjustmentReason.PresentLetterRevealed, -pointCost);

        return board;
    }

    public Player RegisterPlayer(string alias, string connectionId, string ipAddress)
    {
        var player = _players.SingleOrDefault(x => x.Alias == alias);
        if (player != null)
        {
            if (player.IsConnected && player.IpAddress != ipAddress)
                throw new AliasAlreadyTakenException(alias);
            
            player.ConnectionId = connectionId;
            player.IpAddress = ipAddress;
            player.IsConnected = true;
        }
        else
        {
            player = new Player(alias, connectionId, ipAddress);
            _players.Add(player);
            Players = _players.ToArray();
            RecalculateRanking();
            
            if (Status == RoundStatus.Ready)
                Status = RoundStatus.Waiting;
            else if (Status is RoundStatus.Playing or RoundStatus.Finished)
            {
                player.Board = new Board();
                SetBoard(player.Alias, player.Board);
                player.Board.PointsUpdated += BoardPointsUpdated;
            }
        }

        return player;
    }

    public void StartNew(DateTime startTime)
    {
        if (Status == RoundStatus.Waiting)
            throw new HubException("Cannot start game when not everyone is ready.");
        if (Status == RoundStatus.Playing)
            throw new HubException("Cannot start game when one is still in-play.");
        if (Status == RoundStatus.Starting)
            throw new HubException("Game is already starting.");

        Status = RoundStatus.Starting;
        StartTime = startTime;
        GuessTimeLimit = TimeSpan.FromSeconds(GameParameters.GuessTimeLimitSeconds);
        EndTime = startTime + GuessTimeLimit * MaxGuesses;
        NextGuessDeadline = startTime + GuessTimeLimit.Value;
        GuessDeadlines = Enumerable.Range(1, MaxGuesses).Select(i => startTime + (GuessTimeLimit.Value * i)).ToArray();
            
        foreach (var player in _players)
        {
            player.Board = new Board();
            SetBoard(player.Alias, player.Board);
            player.Board.PointsUpdated += BoardPointsUpdated;
        }
    }

    private void BoardPointsUpdated(object? board, EventArgs _) => RecalculateRanking();

    public void Finish()
    {
        Status = RoundStatus.Finished;
        
        foreach (var player in _players)
        {
            player.Points += player.Board?.Points ?? 0;
        }
        
        foreach (var player in _players)
        {
            player.Rank = _players.Count(x => x.Points > player.Points) + 1;
            player.IsJointRank = _players.Count(x => x.Points == player.Points) > 1;
        }
    }

    public void RecalculateRanking()
    {
        foreach (var player in _players)
        {
            if (BoardsByAlias.TryGetValue(player.Alias, out var board))
            {
                board.Rank = _players.Count(x => x.Board?.Points > player.Board?.Points) + 1;
                board.IsJointRank = _players.Count(x => x.Board?.Points == player.Board?.Points) > 1;
            }
        }
    }
    public MaskedRoundState Mask() => new MaskedRoundState
    {
        Status = Status,
        Players = _players.Select(x => x.Mask()).ToArray(),
        WordLength = WordLength,
        MaxGuesses = MaxGuesses,
        StartTime = StartTime,
        EndTime = EndTime,
        RoundNumber = RoundNumber,
        GuessDeadlines = GuessDeadlines,
        GuessTimeLimit = GuessTimeLimit,
        NextGuessDeadline = NextGuessDeadline,
        CurrentExpectedGuessCount = CurrentExpectedGuessCount
    };
}

public enum RoundStatus
{
    // Waiting for players to be ready
    Waiting,
    
    // All players ready, waiting to start
    Ready,
    
    // Counting down to start
    Starting,
    
    // Playing
    Playing,
    
    // Finished, waiting to start new game
    Finished
}