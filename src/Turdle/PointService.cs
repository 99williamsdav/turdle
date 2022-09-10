using Turdle.Models;
using Turdle.Utils;

namespace Turdle;

// TODO move parameters to DB
public class PointService : IPointService
{
    private PointSchedule _pointSchedule = new PointSchedule
    {
        PointScaleType = PointScaleType.Dynamic,
        ValidAnswerOrderPoints = new[]
        {
            new[] { 0 },
            new[] { 20, 15, 10, 5, 0 },
            new[] { 20, 15, 10, 5, 0 },
            new[] { 20, 15, 10, 5, 0 },
            new[] { 0 },
            new[] { 0 },
        },
        FirstValidAnswerPoints = new[]
        {
            0, 20, 20, 20, 0, 0
        },
        CorrectWordOrderPoints = new[] { 200, 150, 100, 50, 20, 10 },
        FirstCorrectAnswerPoints = 200,
        SolutionGuessNumberPoints = new[] { 250, 200, 150, 100, 80, 50 },
        HardModeErrorPoints = new Dictionary<HardModeError, int>
        {
            { HardModeError.AbsentLetterPlayed, -5 },
            { HardModeError.CorrectLetterMissed, -10 },
            { HardModeError.PresentLetterMissed, -10 },
            { HardModeError.PresentLetterPlayedInSamePlace, -10 },
        },
        SuggestedGuessCostPoints = 50,
        RevealedAbsentCostPoints = 10,
        RevealedPresentCostPoints = 100
    };

    public PointSchedule GetPointSchedule() => _pointSchedule;
    
    public void UpdatePointSchedule(PointSchedule newSchedule)
    {
        _pointSchedule = newSchedule;
    }

    public int GetPointCostForSuggestedGuess() => _pointSchedule.SuggestedGuessCostPoints;
    public int GetPointCostForRevealingAbsentLetter() => _pointSchedule.RevealedAbsentCostPoints;
    public int GetPointCostForRevealingPresentLetter() => _pointSchedule.RevealedPresentCostPoints;

    public List<Board.PointAdjustment> GetPointsForGuess(Board.Row row, int? boardSolvedOrder, int playerCount)
    {
        var pointAdjustments = new List<Board.PointAdjustment>();
        if (row.IsCorrect && boardSolvedOrder != null)
        {
            var solutionGuessPoints = row.GuessNumber <= _pointSchedule.SolutionGuessNumberPoints.Length
                ? _pointSchedule.SolutionGuessNumberPoints[row.GuessNumber - 1]
                : _pointSchedule.SolutionGuessNumberPoints.Last();
            if (solutionGuessPoints != 0)
                pointAdjustments.Add(new(PointAdjustmentReason.CorrectSolutionGuessNumber, solutionGuessPoints,
                    $"Solved in {row.GuessNumber} guesses"));
            
            var correctAnswerOrderPoints = GetCorrectAnswerOrderPoints(boardSolvedOrder.Value, playerCount);
            if (correctAnswerOrderPoints != 0)
                pointAdjustments.Add(new(PointAdjustmentReason.CorrectSolutionOrder, correctAnswerOrderPoints,
                    $"Solved {boardSolvedOrder.Value.GetOrdinal(true)}"));
        }

        pointAdjustments.AddRange(row.Errors.Select(x =>
            new Board.PointAdjustment(PointAdjustmentReason.HardModeError, _pointSchedule.HardModeErrorPoints[x.Error],
                x.ToString(), x.ToMaskedString())));
        if (!row.Errors.Any() && row.PlayedOrder != null)
        {
            var guessOrderPoints = GetValidGuessOrderPoints(row.GuessNumber, row.PlayedOrder.Value, playerCount);
            if (guessOrderPoints != 0)
                pointAdjustments.Add(new(PointAdjustmentReason.ValidGuessOrder, guessOrderPoints,
                    $"Made {row.GuessNumber.GetOrdinal(true)} guess {row.PlayedOrder.Value.GetOrdinal(true)}"));
        }

        return pointAdjustments;
    }

    private int GetValidGuessOrderPoints(int guessNumber, int playedOrder, int playerCount)
    {
        var rowOrderPoints = guessNumber <= _pointSchedule.ValidAnswerOrderPoints.Length
            ? _pointSchedule.ValidAnswerOrderPoints[guessNumber - 1]
            : _pointSchedule.ValidAnswerOrderPoints.Last();
        var firstValidGuessOrderPoints = guessNumber <= _pointSchedule.FirstValidAnswerPoints.Length
            ? _pointSchedule.FirstValidAnswerPoints[guessNumber - 1]
            : _pointSchedule.FirstValidAnswerPoints.Last();
        return _pointSchedule.PointScaleType switch
        {
            PointScaleType.Fixed => rowOrderPoints.Length >= playedOrder ? rowOrderPoints[playedOrder - 1] : 0,
            PointScaleType.Dynamic => Convert.ToInt32(firstValidGuessOrderPoints * GetMaxPointRatio(playedOrder, playerCount)),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private int GetCorrectAnswerOrderPoints(int boardSolvedOrder, int playerCount)
    {
        return _pointSchedule.PointScaleType switch
        {
            PointScaleType.Fixed => _pointSchedule.CorrectWordOrderPoints.Length >= boardSolvedOrder
                ? _pointSchedule.CorrectWordOrderPoints[boardSolvedOrder - 1]
                : 0,
            PointScaleType.Dynamic => Convert.ToInt32(_pointSchedule.FirstCorrectAnswerPoints * GetMaxPointRatio(boardSolvedOrder, playerCount)),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    // 1st = 100%
    // last = 0%
    // 2nd of 3 = 50%
    private static double GetMaxPointRatio(int order, int playerCount)
    {
        if (playerCount == 1)
            return 1d;
        return 1d / (playerCount) * (playerCount - order + 1);
    }
}

public interface IPointService
{
    PointSchedule GetPointSchedule();
    void UpdatePointSchedule(PointSchedule newSchedule);
    List<Board.PointAdjustment> GetPointsForGuess(Board.Row row, int? boardSolvedOrder, int playerCount);
    int GetPointCostForSuggestedGuess();
    int GetPointCostForRevealingAbsentLetter();
    int GetPointCostForRevealingPresentLetter();
}

public enum PointScaleType
{
    Fixed,
    Dynamic
}