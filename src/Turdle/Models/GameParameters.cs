namespace Turdle.Models;

public class GameParameters
{
    public const int FirstExpectedGuess = 1;

    public const bool ShowKnownOpponentTiles = true;
    public const bool ShowKnownOpponentWords = false;

    public int WordLength { get; set; } = 5;
    public int MaxGuesses { get; set; } = 6;

    public int GuessTimeLimitSeconds { get; set; } = 30;

    // TODO implement
    public bool UseNaughtyWordList { get; set; } = true;

    public GameParameters Clone() => (GameParameters)this.MemberwiseClone();

    private GameParameters() { }

    public static GameParameters Default = new GameParameters();

    public static GameParameters GetDefault() => Default.Clone();
}