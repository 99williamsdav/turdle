using Microsoft.AspNetCore.SignalR;
using Turdle.Utils;

namespace Turdle.Hubs;

public class HomeHub : Hub
{
    private readonly ILogger<HomeHub> _logger;
    private readonly RoomManager _roomManager;

    public HomeHub(RoomManager roomManager, ILogger<HomeHub> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "OnDisconnectedAsync"))
        {
            _logger.LogInformation($"OnDisconnectedAsync({exception?.Message})");
            _roomManager.DisconnectHome(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
    
    public override async Task OnConnectedAsync()
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "OnConnectedAsync"))
        {
            _logger.LogInformation($"OnConnectedAsync()");
            _roomManager.RegisterHomeConnection(Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }
    }

    public async Task<string> CreateRoom()
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "CreateRoom"))
        {
            _logger.LogInformation($"CreateRoom()");
            return await _roomManager.CreateRoom(Context.ConnectionId);
        }
    }
}