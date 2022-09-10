namespace Turdle.Models;

public class PointSchedule
{
    public PointScaleType PointScaleType { get; set; }
    
    // points awarded by order of submitting a valid answer for each row
    public int[][] ValidAnswerOrderPoints { get; set; }

    public int[] FirstValidAnswerPoints { get; set; }

    // points awarded by order of getting the correct answer
    public int[] CorrectWordOrderPoints { get; set; }

    public int FirstCorrectAnswerPoints { get; set; }
    
    // points awarded for which guess is the correct answer
    public int[] SolutionGuessNumberPoints { get; set; }
    
    // points lost for breaking hard-mode
    public IReadOnlyDictionary<HardModeError, int> HardModeErrorPoints { get; set; }

    public int SuggestedGuessCostPoints { get; set; }
    public int RevealedAbsentCostPoints { get; set; }
    public int RevealedPresentCostPoints { get; set; }
}