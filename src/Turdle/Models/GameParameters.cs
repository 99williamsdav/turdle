namespace Turdle.Models;

public class GameParameters
{
    public const int FirstExpectedGuess = 1;

    public const bool ShowKnownOpponentTiles = true;
    public const bool ShowKnownOpponentWords = false;

    public int MaxGuesses { get; set; } = 6;

    public int GuessTimeLimitSeconds { get; set; } = 30;

    public AnswerListType AnswerList { get; set; } = AnswerListType.FiveLetterEasy;

    // TODO implement
    public bool UseNaughtyWordList { get; set; } = true;

    // TODO better way of syncing alias?
    public string? AdminAlias { get; set; }

    public GameParameters Clone() => (GameParameters)this.MemberwiseClone();

    private GameParameters() { }

    public static GameParameters Default = new GameParameters();

    public static GameParameters GetDefault() => Default.Clone();
}