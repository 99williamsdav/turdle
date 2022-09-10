namespace Turdle.Utils;

public static class Extensions
{
    private static readonly Random Random = new Random();
    
    public static T PickRandom<T>(this IList<T> set)
    {
        return set[Random.Next(0, set.Count - 1)];
    }

    public static string GetOrdinal(this int value, bool includeNumber = false)
    {
        var suffix = GetOrdinalSuffix(value);
        return includeNumber ? $"{value}{suffix}" : suffix;
    }

    private static string GetOrdinalSuffix(int value)
    {
        if (value == 11 || value == 12 || value == 13)
            return "th";
        switch (value % 10) {
            case 1:  return "st";
            case 2:  return "nd";
            case 3:  return "rd";
            default: return "th";
        }
    }
}