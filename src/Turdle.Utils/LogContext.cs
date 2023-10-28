using Microsoft.Extensions.Logging;

namespace Turdle.Utils;

public class LogContext<T> : IDisposable
{
    private readonly IDisposable? _connectionId;
    private readonly IDisposable? _methodName;
    private readonly ILogger<T> _logger;
    private readonly DateTime _startTime;

    public LogContext(ILogger<T> logger, string connectionId, string methodName)
    {
        _startTime = DateTime.Now;
        _logger = logger;
        // _connectionId = log4net.LogicalThreadContext.Stacks["ConnectionId"].Push(connectionId);
        // _methodName = log4net.LogicalThreadContext.Stacks["MethodName"].Push($"{typeof(T).Name}.{methodName}");
        
        log4net.LogicalThreadContext.Properties["ConnectionId"] = connectionId;
        log4net.LogicalThreadContext.Properties["MethodName"] = $"{typeof(T).Name}.{methodName}";
        _logger.LogInformation("+");
    }
        
    public void Dispose()
    {
        var duration = DateTime.Now - _startTime;
        _logger.LogInformation($"({duration.TotalMilliseconds:n0}ms)");
        _connectionId?.Dispose();
        _methodName?.Dispose();
        log4net.LogicalThreadContext.Properties["ConnectionId"] = null;
        log4net.LogicalThreadContext.Properties["MethodName"] = null;
    }
}

public static class LogContext
{
    public static LogContext<T> Create<T>(ILogger<T> logger, string connectionId, string methodName) =>
        new LogContext<T>(logger, connectionId, methodName);
}