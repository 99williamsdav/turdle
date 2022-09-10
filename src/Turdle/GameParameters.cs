namespace Turdle;

// TODO turn into injectable singleton?
public static class GameParameters
{
    public static int WordLength { get; set; } = 5;
    public static int MaxGuesses { get; set; } = 6;
    
    public static int GuessTimeLimitSeconds { get; set; } = 30;
    public const int FirstExpectedGuess = 1;

    public const bool ShowKnownOpponentTiles = true;
    public const bool ShowKnownOpponentWords = false;
    
    public static bool UseNaughtyWordList { get; set; } = true;
}