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

    public Task<string> CreateRoom()
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "CreateRoom"))
        {
            _logger.LogInformation($"CreateRoom()");
            return Task.FromResult(_roomManager.CreateRoom(Context.ConnectionId));
        }
    }
}