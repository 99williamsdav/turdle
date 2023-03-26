using Microsoft.AspNetCore.Mvc;
using Turdle.Models;
using Turdle.Utils;
using Turdle.ViewModel;

namespace Turdle.Controllers;

[ApiController]
public class GameController : ControllerBase
{
    private readonly ILogger<GameController> _logger;
    private readonly RoomManager _roomManager;

    public GameController(RoomManager roomManager, ILogger<GameController> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    [HttpGet]
    [Route("GetRooms")]
    public Task<RoomSummary[]> GetRooms()
    {
        using (LogContext.Create(_logger, "API", "GetRooms"))
        {
            return Task.FromResult(_roomManager.GetRooms());
        }
    }

    [HttpGet]
    [Route("GetGameState")]
    public Task<IRoundState<IPlayer<IBoard<IRow<ITile>, ITile>, IRow<ITile>, ITile>, IBoard<IRow<ITile>, ITile>, IRow<ITile>, ITile>> GetGameState(string roomCode)
    {
        using (LogContext.Create(_logger, "API", "GetGameState"))
        {
            return _roomManager.GetRoom(roomCode).GetGameState();
        }
    }

    [HttpGet]
    [Route("GetGameParameters")]
    public Task<GameParameters> GetGameParameters(string roomCode)
    {
        using (LogContext.Create(_logger, "API", "GetGameParameters"))
        {
            return _roomManager.GetRoom(roomCode).GetGameParameters();
        }
    }

    [HttpGet]
    [Route("GetFakeReadyBoard")]
    public Board GetFakeReadyBoard()
    {
        using (LogContext.Create(_logger, "API", "GetFakeReadyBoard"))
        {
            return _roomManager.GetFakeReadyBoard();
        }
    }

    [HttpGet]
    [Route("GetPreviousAlias")]
    public async Task<AliasInfo> GetPreviousAlias()
    {
        using (LogContext.Create(_logger, "API", "GetPreviousAlias"))
        {
            string? alias = HttpContext.Request.Cookies["LastAlias"];
            if (alias == null)
                return new AliasInfo(null, AliasInfo.GameStatus.NotRegistered);
            //var status = await _roomManager.GetRoom(roomCode).GetAliasStatus(alias);
            // TODO think about how this works across rooms
            return new AliasInfo(alias, AliasInfo.GameStatus.NotRegistered);
        }
    }
}