namespace Turdle.Models;

public class Result<T>
{
    public T Response { get; set; }
    
    public bool IsSuccess { get; set; }
    
    public string ErrorMessage { get; set; }

    public Result(T response)
    {
        Response = response;
        IsSuccess = true;
    }

    public Result(string errorMessage)
    {
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }
}

public class VoidResult
{
    public bool IsSuccess { get; set; }
    
    public string ErrorMessage { get; set; }
}