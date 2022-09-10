namespace Turdle.Models.Exceptions;

public class AliasAlreadyTakenException : Exception
{
    public AliasAlreadyTakenException(string alias) : base(
        $"Alias '{alias}' has already been taken by a connected user")
    {
    }
}