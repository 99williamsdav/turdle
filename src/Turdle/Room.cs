using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Turdle.Hubs;
using Turdle.Models;
using Turdle.Utils;
using Turdle.ViewModel;
using Timer = System.Timers.Timer;

namespace Turdle;

public class Room
{
    private const int StartCountdownSeconds = 5;
    
    private readonly ILogger<RoomManager> _logger;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IHubContext<AdminHub> _adminHubContext;
    private readonly WordService _wordService;
    private readonly IPointService _pointService;
    private readonly IWordAnalysisService _wordAnalyst;
    private readonly DateTime _createdOn = DateTime.Now;
    
    private InternalRoundState _internalRoundState;

    private List<InternalRoundState> _previousRoundStates = new List<InternalRoundState>();

    // TODO have this a mapping to alias only (shouldn't be handling players or boards here)
    private readonly ConcurrentDictionary<string, Player> _playersByConnectionId =
        new ConcurrentDictionary<string, Player>();

    private readonly ConcurrentDictionary<string, object> _adminConnections = new ConcurrentDictionary<string, object>();
    private readonly ConcurrentDictionary<string, object> _tvConnections = new ConcurrentDictionary<string, object>();

    private string? _adminConnectionId;
    private readonly string _roomCode;

    private readonly object _stateLock = new object();
    
    private Timer? _startTimer;
    private Timer? _guessTimer;

    private readonly Func<Task> _roomSummaryUpdatedCallback;

    public Room(
        IHubContext<GameHub> hubContext, IHubContext<AdminHub> adminHubContext, 
        WordService wordService, 
        IPointService pointService, 
        ILogger<RoomManager> logger, 
        IWordAnalysisService wordAnalyst, 
        string roomCode, 
        string adminConnectionId, Func<Task> roomSummaryUpdatedCallback)
    {
        _hubContext = hubContext;
        _adminHubContext = adminHubContext;
        _wordService = wordService;
        _pointService = pointService;
        _logger = logger;
        _wordAnalyst = wordAnalyst;
        _roomSummaryUpdatedCallback = roomSummaryUpdatedCallback;
        _roomCode = roomCode;
        // TODO leave null until game has started? 
        _internalRoundState = new InternalRoundState(wordService.GetRandomWord(GameParameters.WordLength), _pointService);
    }

    public RoomSummary ToSummary()
    {
        return new RoomSummary
        {
            CreatedOn = _createdOn,
            RoomCode = _roomCode,
            RoundNumber = _previousRoundStates.Count + 1,
            Players = _internalRoundState.Players.Select(x => x.Mask()).ToArray(),
            AdminAlias = _adminConnectionId != null && _playersByConnectionId.TryGetValue(_adminConnectionId, out var admin) 
                ? admin.Alias 
                : "",
            CurrentRoundStatus = _internalRoundState.Status
        };
    }

    public async Task HardResetAll()
    {
        _playersByConnectionId.Clear();
        _previousRoundStates = new List<InternalRoundState>();
        _internalRoundState = new InternalRoundState(_wordService.GetRandomWord(GameParameters.WordLength), _pointService);
        await BroadcastRoundState(_internalRoundState, _internalRoundState.Mask());
        await _roomSummaryUpdatedCallback();
    }

    public async Task<IRoundState<IPlayer<IBoard<IRow<ITile>, ITile>, IRow<ITile>, ITile>, IBoard<IRow<ITile>, ITile>, IRow<ITile>, ITile>> GetGameState()
    {
        lock (_stateLock)
        {
            return _internalRoundState.Status == RoundStatus.Finished
                ? _internalRoundState
                : _internalRoundState.Mask();
        }
    }

    public async Task<AliasInfo.GameStatus> GetAliasStatus(string alias)
    {
        var player = _internalRoundState.Players.SingleOrDefault(x => x.Alias == alias);
        if (player == null)
            return AliasInfo.GameStatus.NotRegistered;

        return player.IsConnected
            ? AliasInfo.GameStatus.RegisteredConnected
            : AliasInfo.GameStatus.RegisteredDisconnected;
    }

    public async Task<Player> RegisterAlias(string alias, string connectionId, string ipAddress)
    {
        Player? player;
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            _playersByConnectionId.Remove(connectionId, out _);
            player = _internalRoundState.RegisterPlayer(alias, connectionId, ipAddress);
            _playersByConnectionId.TryAdd(connectionId, player);
            _adminConnectionId ??= connectionId;

            maskedRoundState = _internalRoundState.Mask();
        }
        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        await _roomSummaryUpdatedCallback();
        return player;
    }

    public void RegisterAdminConnection(string connectionId)
    {
        _adminConnections.TryAdd(connectionId, null);
    }
    
    public void RegisterTvConnection(string connectionId)
    {
        _tvConnections.TryAdd(connectionId, null);
    }

    public async Task PlayerDisconnected(string connectionId)
    {
        MaskedRoundState? gameState = null;
        bool registeredPlayerDisconnected = false;
        lock (_stateLock)
        {
            if (_playersByConnectionId.TryGetValue(connectionId, out var player))
            {
                player.IsConnected = false;
                if (_internalRoundState.Status == RoundStatus.Ready)
                {
                    player.Ready = false;
                    _internalRoundState.Status = RoundStatus.Waiting;
                }

                registeredPlayerDisconnected = true;
                gameState = _internalRoundState.Mask();
            }

        }

        if (registeredPlayerDisconnected)
        {
            await BroadcastRoundState(_internalRoundState, gameState);
            await _roomSummaryUpdatedCallback();
        }
    }

    public async Task PlayerReconnected(string connectionId)
    {
        MaskedRoundState? gameState = null;
        bool reconnected = false;
        lock (_stateLock)
        {
            if (_playersByConnectionId.TryGetValue(connectionId, out var player))
            {
                player.IsConnected = true;
                reconnected = true;
                gameState = _internalRoundState.Mask();
            }
        }
        
        if (reconnected)
            await BroadcastRoundState(_internalRoundState, gameState);
    }

    public async Task LogOut(string connectionId)
    {
        if (_playersByConnectionId.TryGetValue(connectionId, out var player))
        {
            await KickPlayer(player.Alias);
        }

        _adminConnections.Remove(connectionId, out _);
        _tvConnections.Remove(connectionId, out _);
    }

    // TODO check from admin connection
    public async Task KickPlayer(string alias)
    {
        MaskedRoundState maskedRoundState;
        Player player;
        lock (_stateLock)
        {
            player = _internalRoundState.RemovePlayer(alias);
            if (_playersByConnectionId.TryRemove(player.ConnectionId, out _))
            {
                // TODO call JS disconnect function
            }

            if (_adminConnectionId == player.ConnectionId)
                _adminConnectionId = _playersByConnectionId.Count > 0 ? _playersByConnectionId.First().Key : null;
            
            if (_internalRoundState.Status == RoundStatus.Playing &&
                _internalRoundState.Players.All(x => x.Board.Status is BoardStatus.Solved or BoardStatus.Failed))
                _internalRoundState.Finish();
            if (_internalRoundState.Status == RoundStatus.Waiting &&
                _internalRoundState.Players.All(x => x.Ready))
                _internalRoundState.Status = RoundStatus.Ready;
                
            maskedRoundState = _internalRoundState.Mask();
        }
        
        await BroadcastRoundState(_internalRoundState, maskedRoundState, player.ConnectionId);
        await _roomSummaryUpdatedCallback();
    }

    public async Task DisconnectPlayer(string alias)
    {
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            var player = _internalRoundState.Players.SingleOrDefault(x => x.Alias == alias);
            if (player == null)
                return;
            player.IsConnected = false;

            if (_internalRoundState.Status == RoundStatus.Ready)
            {
                player.Ready = false;
                _internalRoundState.Status = RoundStatus.Waiting;
            }
                
            maskedRoundState = _internalRoundState.Mask();
        }
        
        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        await _roomSummaryUpdatedCallback();
    }

    public async Task ToggleReady(bool ready, string connectionId)
    {
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            var player = _playersByConnectionId[connectionId];
            player.Ready = ready;
            if (!ready && _internalRoundState.Status == RoundStatus.Ready)
            {
                _internalRoundState.Status = RoundStatus.Waiting;
            } else
            {
                _internalRoundState.Status = _internalRoundState.Players.All(x => x.Ready) ? RoundStatus.Ready : RoundStatus.Waiting;
            }

            maskedRoundState = _internalRoundState.Mask();
        }
        
        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        await _roomSummaryUpdatedCallback();
    }

    // TODO check from admin connection?
    public async Task StartNewRound()
    {
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            if (_internalRoundState.Status == RoundStatus.Waiting)
                throw new HubException("Cannot start game when not everyone is ready.");
            if (_internalRoundState.Status == RoundStatus.Playing)
                throw new HubException("Cannot start game when one is still in-play.");
            if (_internalRoundState.Status == RoundStatus.Starting)
                throw new HubException("Game is already starting.");

            if (_internalRoundState.Status == RoundStatus.Finished)
            {
                _previousRoundStates.Add(_internalRoundState);
                // TODO remove any players no longer connected?
                _internalRoundState = new InternalRoundState(_wordService.GetRandomWord(GameParameters.WordLength),
                    _internalRoundState.Players, _internalRoundState.RoundNumber + 1, _pointService);
            }

            _internalRoundState.StartNew(DateTime.Now.AddSeconds(StartCountdownSeconds));
            
            _startTimer?.Stop();
            _startTimer = new System.Timers.Timer(StartCountdownSeconds * 1000);
            _startTimer.Elapsed += async ( sender, e ) => await StartRoundInternal();
            _startTimer.Start();

            _guessTimer?.Stop();

            maskedRoundState = _internalRoundState.Mask();
        }
        
        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        var allConnections = _internalRoundState.Players.Select(x => x.ConnectionId)
            .Concat(_tvConnections.Keys)
            .Concat(_adminConnections.Keys);
        await _hubContext.Clients.Clients(allConnections).SendAsync("StartNewGame", new Board());
        await _roomSummaryUpdatedCallback();
    }

    private async Task StartRoundInternal()
    {
        using (LogContext.Create(_logger, "Timer", "StartRoundInternal"))
        {
            MaskedRoundState maskedRoundState;
            lock (_stateLock)
            {
                _internalRoundState.Status = RoundStatus.Playing;
                maskedRoundState = _internalRoundState.Mask();
                _startTimer?.Stop();
            
                var timeUntilDeadline = _internalRoundState.GuessDeadlines[0] - DateTime.Now;
                _guessTimer = new Timer(timeUntilDeadline.TotalMilliseconds);
                _guessTimer.Elapsed += async ( sender, e ) => await GuessDeadlineReached();
                _guessTimer.Start();
            }
            await BroadcastRoundState(_internalRoundState, maskedRoundState);
            await _roomSummaryUpdatedCallback();
        }
    }

    private async Task GuessDeadlineReached()
    {
        using (LogContext.Create(_logger, "Timer", "GuessDeadlineReached"))
        {
            MaskedRoundState maskedRoundState;
            Player[] impactedPlayers;
            lock (_stateLock)
            {
                if (_internalRoundState.Status != RoundStatus.Playing)
                {
                    _guessTimer?.Stop();
                    return;
                }
            
                impactedPlayers = _internalRoundState.ForceGuess(_wordService);
                maskedRoundState = _internalRoundState.Mask();
            }

            _logger.LogInformation($"Broadcasting {impactedPlayers.Length} updated player boards.");
            foreach (var player in impactedPlayers)
            {
                await _hubContext.Clients.Clients(player.ConnectionId).SendAsync("BoardUpdated", player.Board);
            }
            _logger.LogInformation("Finished broadcasting updated player boards.");
        
            await BroadcastRoundState(_internalRoundState, maskedRoundState);
        }
    }

    public async Task<Result<Board>> PlayGuess(string connectionId, string guess, int guessNumber)
    {
        if (!_wordService.IsWordAccepted(guess))
            return new Result<Board>("Not in word list");

        var similarWords = await _wordAnalyst.GetSimilarWords(guess);
        
        Board board;
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            var player = _playersByConnectionId[connectionId];
            board = _internalRoundState.PlayGuess(player.Alias, guess, guessNumber, similarWords);
            maskedRoundState = _internalRoundState.Mask();
        }

        await BroadcastRoundState(_internalRoundState, maskedRoundState);

        return new Result<Board>(board);
    }

    public async Task<string?> SuggestGuess(string connectionId)
    {
        string? guess = null;
        MaskedRoundState? maskedRoundState = null;
        lock (_stateLock)
        {
            if (_playersByConnectionId.TryGetValue(connectionId, out var player))
            {
                guess = _internalRoundState.SuggestGuess(_wordService, player.Alias);
                maskedRoundState = _internalRoundState.Mask();
            }
        }

        if (maskedRoundState != null)
        {
            await BroadcastRoundState(_internalRoundState, maskedRoundState);
        }

        return guess;
    }

    public async Task<Result<Board>> RevealAbsentLetter(string connectionId)
    {
        Board board;
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            var player = _playersByConnectionId[connectionId];
            board = _internalRoundState.RevealAbsentLetter(player.Alias);
            maskedRoundState = _internalRoundState.Mask();
        }

        await BroadcastRoundState(_internalRoundState, maskedRoundState);

        return new Result<Board>(board);
    }

    public async Task<Result<Board>> RevealPresentLetter(string connectionId)
    {
        Board board;
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            var player = _playersByConnectionId[connectionId];
            board = _internalRoundState.RevealPresentLetter(player.Alias);
            maskedRoundState = _internalRoundState.Mask();
        }

        await BroadcastRoundState(_internalRoundState, maskedRoundState);

        return new Result<Board>(board);
    }

    // TODO move into background task?
    private async Task BroadcastRoundState(InternalRoundState internalRoundState, MaskedRoundState maskedRoundState, params string[] additionalConnectionIds)
    {
        _logger.LogInformation("Broadcasting round state.");
        var startTime = DateTime.Now;
        if (internalRoundState.Status == RoundStatus.Finished)
        {
            var allConnections = _internalRoundState.Players.Select(x => x.ConnectionId)
                .Concat(_tvConnections.Keys)
                .Concat(_adminConnections.Keys)
                .Concat(additionalConnectionIds);
            await _hubContext.Clients.Clients(allConnections).SendAsync("GameStateUpdated", internalRoundState);
            return;
        }
        
        var completeConnectionIds = _internalRoundState.Players
            .Where(x => x.Board is { IsFinished: true })
            .Select(x => x.ConnectionId)
            .Concat(_adminConnections.Keys)
            .ToArray();
        var maskedConnectionIds = _internalRoundState.Players
            .Where(x => x.Board is not { IsFinished: true })
            .Select(x => x.ConnectionId)
            .Concat(_tvConnections.Keys)
            .Concat(additionalConnectionIds)
            .ToArray();
        await _hubContext.Clients.Clients(completeConnectionIds).SendAsync("GameStateUpdated", internalRoundState);
        await _hubContext.Clients.Clients(maskedConnectionIds).SendAsync("GameStateUpdated", maskedRoundState);
        _logger.LogInformation($"Finished broadcasting round state ({(DateTime.Now - startTime).TotalMilliseconds:N1}ms).");
    }

    public Task<Board> GetPlayerBoard(string connectionId)
    {
        lock (_stateLock)
        {
            if (_playersByConnectionId.TryGetValue(connectionId, out var player))
            {
                return Task.FromResult(_internalRoundState.GetBoard(player.Alias));
            }
            else
            {
                return Task.FromResult(new Board());
            }
        }
    }
}