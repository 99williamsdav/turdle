using Microsoft.AspNetCore.SignalR;
using Turdle.Models;
using Turdle.Utils;

namespace Turdle.Hubs;

public class AdminHub : Hub
{
    private readonly ILogger<AdminHub> _logger;
    private readonly RoomManager _roomManager;
    private readonly IPointService _pointService;

    public AdminHub(RoomManager roomManager, ILogger<AdminHub> logger, IPointService pointService)
    {
        _roomManager = roomManager;
        _logger = logger;
        _pointService = pointService;
    }

    public async Task KickPlayer(string roomCode, string alias)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "KickPlayer"))
        {
            await _roomManager.GetRoom(roomCode).KickPlayer(alias);
        }
    }

    public async Task DisconnectPlayer(string roomCode, string alias)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "DisconnectPlayer"))
        {
            await _roomManager.GetRoom(roomCode).DisconnectPlayer(alias);
        }
    }

    // TODO move to global hard reset
    public async Task HardReset(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "HardReset"))
        {
            await _roomManager.GetRoom(roomCode).HardResetAll();
        }
    }

    public async Task UpdatePointSchedule(PointSchedule pointSchedule)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdatePointSchedule"))
        {
            _pointService.UpdatePointSchedule(pointSchedule);
        }
    }

    public async Task UpdateGuessTimeLimit(int seconds)
    {
        GameParameters.Default.GuessTimeLimitSeconds = seconds;
    }

    public async Task UpdateWordLength(int length)
    {
        GameParameters.Default.WordLength = length;
    }

    public async Task UpdateMaxGuesses(int maxGuesses)
    {
        GameParameters.Default.MaxGuesses = maxGuesses;
    }

    public async Task PingAll()
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "PingAll"))
        {
            await Clients.All.SendAsync("Ping");
        }
    }
}