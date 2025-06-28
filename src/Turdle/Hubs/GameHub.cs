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

    public Task LogOut(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "LogOut"))
        {
            _logger.LogInformation($"LogOut({roomCode})");
            return _roomManager.GetRoom(roomCode).LogOut(Context.ConnectionId);
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
                var room = _roomManager.GetRoom(roomCode, Context.ConnectionId);
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

    public Task RegisterAdminConnection(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RegisterAdminConnection"))
        {
            _logger.LogInformation($"RegisterAdminConnection({roomCode})");
            _roomManager.GetRoom(roomCode).RegisterAdminConnection(Context.ConnectionId);
            return Task.CompletedTask;
        }
    }

    public Task RegisterTvConnection(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RegisterTvConnection"))
        {
            _logger.LogInformation($"RegisterTvConnection({roomCode})");
            _roomManager.GetRoom(roomCode).RegisterTvConnection(Context.ConnectionId);
            return Task.CompletedTask;
        }
    }

    public Task ToggleReady(string roomCode, bool ready)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "ToggleReady"))
        {
            _logger.LogInformation($"ToggleReady({roomCode}, {ready})");
            return _roomManager.GetRoom(roomCode).ToggleReady(ready, Context.ConnectionId);
        }
    }

    public Task VoteToStart(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "VoteToStart"))
        {
            _logger.LogInformation($"VoteToStart({roomCode})");
            return _roomManager.GetRoom(roomCode).StartNewRound();
        }
    }

    public Task<Result<Board>> PlayGuess(string roomCode, string guess, int guessNumber)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "PlayGuess"))
        {
            _logger.LogInformation($"PlayGuess({roomCode}, {guess}, {guessNumber})");
            try
            {
                return _roomManager.GetRoom(roomCode).PlayGuess(Context.ConnectionId, guess, guessNumber);
            }
            catch (Exception e)
            {
                throw new HubException(e.Message);
            }
        }
    }

    public Task<string?> SuggestGuess(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "SuggestGuess"))
        {
            _logger.LogInformation($"SuggestGuess({roomCode})");
            return _roomManager.GetRoom(roomCode).SuggestGuess(Context.ConnectionId);
        }
    }

    public Task<Result<Board>> GiveUp(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "GiveUp"))
        {
            _logger.LogInformation($"GiveUp({roomCode})");
            try
            {
                return _roomManager.GetRoom(roomCode).GiveUp(Context.ConnectionId);
            }
            catch (Exception e)
            {
                throw new HubException(e.Message);
            }
        }
    }

    public Task<Result<Board>> RevealAbsentLetter(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RevealAbsentLetter"))
        {
            _logger.LogInformation($"RevealAbsentLetter({roomCode})");
            return _roomManager.GetRoom(roomCode).RevealAbsentLetter(Context.ConnectionId);
        }
    }

    public Task<Result<Board>> RevealPresentLetter(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "RevealPresentLetter"))
        {
            _logger.LogInformation($"RevealPresentLetter({roomCode})");
            return _roomManager.GetRoom(roomCode).RevealPresentLetter(Context.ConnectionId);
        }
    }

    public Task<Board> GetPlayerBoard(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "GetPlayerBoard"))
        {
            _logger.LogInformation($"GetPlayerBoard({roomCode})");
            return _roomManager.GetRoom(roomCode).GetPlayerBoard(Context.ConnectionId);
        }
    }

    public async Task UpdateGuessTimeLimit(string roomCode, int seconds)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdateGuessTimeLimit"))
        {
            await _roomManager.GetRoom(roomCode)
                .UpdateGameParameters(Context.ConnectionId, param => param.GuessTimeLimitSeconds = seconds);
        }
    }

    public async Task UpdateAnswerList(string roomCode, AnswerListType listType)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdateAnswerList"))
        {
            await _roomManager.GetRoom(roomCode)
                .UpdateGameParameters(Context.ConnectionId, param => param.AnswerList = listType);
        }
    }

    public async Task AddBot(string roomCode, string? personality)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "AddBot"))
        {
            await _roomManager.GetRoom(roomCode)
                .AddBot(Context.ConnectionId, personality);
        }
    }

    public async Task UpdateMaxGuesses(string roomCode, int maxGuesses)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "UpdateMaxGuesses"))
        {
            await _roomManager.GetRoom(roomCode)
                .UpdateGameParameters(Context.ConnectionId, param => param.MaxGuesses = maxGuesses);
        }
    }

    public async Task SendChat(string roomCode, string message)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "SendChat"))
        {
            var room = _roomManager.GetRoom(roomCode);
            await room.SendChat(Context.ConnectionId, message);
            await room.NotifyStoppedTyping(Context.ConnectionId);
        }
    }

    public async Task Typing(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "Typing"))
        {
            await _roomManager.GetRoom(roomCode)
                .NotifyTyping(Context.ConnectionId);
        }
    }

    public async Task StopTyping(string roomCode)
    {
        using (LogContext.Create(_logger, Context.ConnectionId, "StopTyping"))
        {
            await _roomManager.GetRoom(roomCode)
                .NotifyStoppedTyping(Context.ConnectionId);
        }
    }
}