using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Turdle.Models;
using Turdle.Models.Exceptions;
using Turdle.Utils;

namespace Turdle.Hubs;

public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly RoomManager _roomManager;

    public GameHub(RoomManager roomManager, ILogger<GameHub> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "OnDisconnectedAsync"))
        {
            _logger.LogInformation($"OnDisconnectedAsync({exception?.Message})");
            await _roomManager.PlayerDisconnected(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
    
    public override async Task OnConnectedAsync()
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "OnConnectedAsync"))
        {
            _logger.LogInformation($"OnConnectedAsync()");
            await _roomManager.PlayerReconnected(Context.ConnectionId);
            await base.OnConnectedAsync();
        }
    }

    public async Task LogOut(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "LogOut"))
        {
            _logger.LogInformation($"LogOut({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            await room.LogOut(Context.ConnectionId);
        }
    }

    public async Task<Result<Player>> RegisterAlias(string roomCode, string alias)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RegisterAlias"))
        {
            var feature = Context.Features.Get<IHttpConnectionFeature>();
            _logger.LogInformation($"RegisterAlias({roomCode}, {alias}, {feature?.RemoteIpAddress})");
            try
            {
                var room = await _roomManager.GetRoom(roomCode, Context.ConnectionId);
                var player = await room.RegisterAlias(alias, Context.ConnectionId, feature.RemoteIpAddress.ToString());
                return new Result<Player>(player);
            }
            catch (AliasAlreadyTakenException e)
            {
                _logger.LogError(e, "Error registering alias");
                return new Result<Player>(e.Message);
            }
        }
    }

    public async Task RegisterAdminConnection(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RegisterAdminConnection"))
        {
            _logger.LogInformation($"RegisterAdminConnection({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            room.RegisterAdminConnection(Context.ConnectionId);
        }
    }

    public async Task RegisterTvConnection(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RegisterTvConnection"))
        {
            _logger.LogInformation($"RegisterTvConnection({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            room.RegisterTvConnection(Context.ConnectionId);
        }
    }

    public async Task ToggleReady(string roomCode, bool ready)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "ToggleReady"))
        {
            _logger.LogInformation($"ToggleReady({roomCode}, {ready})");
            var room = await _roomManager.GetRoom(roomCode);
            await room.ToggleReady(ready, Context.ConnectionId);
        }
    }

    public async Task VoteToStart(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "VoteToStart"))
        {
            _logger.LogInformation($"VoteToStart({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            await room.StartNewRound();
        }
    }

    public async Task<Result<Board>> PlayGuess(string roomCode, string guess, int guessNumber)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "PlayGuess"))
        {
            _logger.LogInformation($"PlayGuess({roomCode}, {guess}, {guessNumber})");
            try
            {
                var room = await _roomManager.GetRoom(roomCode);
                return await room.PlayGuess(Context.ConnectionId, guess, guessNumber);
            }
            catch (Exception e)
            {
                throw new HubException(e.Message);
            }
        }
    }

    public async Task<string?> SuggestGuess(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "SuggestGuess"))
        {
            _logger.LogInformation($"SuggestGuess({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            return await room.SuggestGuess(Context.ConnectionId);
        }
    }

    public async Task<Result<Board>> GiveUp(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "GiveUp"))
        {
            _logger.LogInformation($"GiveUp({roomCode})");
            try
            {
                var room = await _roomManager.GetRoom(roomCode);
                return await room.GiveUp(Context.ConnectionId);
            }
            catch (Exception e)
            {
                throw new HubException(e.Message);
            }
        }
    }

    public async Task<Result<Board>> RevealAbsentLetter(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RevealAbsentLetter"))
        {
            _logger.LogInformation($"RevealAbsentLetter({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            return await room.RevealAbsentLetter(Context.ConnectionId);
        }
    }

    public async Task<Result<Board>> RevealPresentLetter(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RevealPresentLetter"))
        {
            _logger.LogInformation($"RevealPresentLetter({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            return await room.RevealPresentLetter(Context.ConnectionId);
        }
    }

    public async Task<Board> GetPlayerBoard(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "GetPlayerBoard"))
        {
            _logger.LogInformation($"GetPlayerBoard({roomCode})");
            var room = await _roomManager.GetRoom(roomCode);
            return await room.GetPlayerBoard(Context.ConnectionId);
        }
    }

    public async Task UpdateGuessTimeLimit(string roomCode, int seconds)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdateGuessTimeLimit"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.UpdateGameParameters(Context.ConnectionId, param => param.GuessTimeLimitSeconds = seconds);
        }
    }

    public async Task UpdateAnswerList(string roomCode, AnswerListType listType)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdateAnswerList"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.UpdateGameParameters(Context.ConnectionId, param => param.AnswerList = listType);
        }
    }

    public async Task AddBot(string roomCode, string? personality)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "AddBot"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.AddBot(Context.ConnectionId, personality);
        }
    }

    public async Task KickPlayer(string roomCode, string alias)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "KickPlayer"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.KickPlayer(alias, Context.ConnectionId);
        }
    }

    public async Task UpdateMaxGuesses(string roomCode, int maxGuesses)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdateMaxGuesses"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.UpdateGameParameters(Context.ConnectionId, param => param.MaxGuesses = maxGuesses);
        }
    }

    public async Task SendChat(string roomCode, string message)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "SendChat"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.SendChat(Context.ConnectionId, message);
            await room.NotifyStoppedTyping(Context.ConnectionId);
        }
    }

    public async Task Typing(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "Typing"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.NotifyTyping(Context.ConnectionId);
        }
    }

    public async Task StopTyping(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "StopTyping"))
        {
            var room = await _roomManager.GetRoom(roomCode);
            await room.NotifyStoppedTyping(Context.ConnectionId);
        }
    }
}