using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Turdle.Bots;
using Turdle.ChatGpt;
using Turdle.Hubs;
using Turdle.Models;
using Turdle.Utils;
using Turdle.ViewModel;
using Timer = System.Timers.Timer;

namespace Turdle;

public class Room
{
    private const int StartCountdownSeconds = 5;

    private static string[] _botPersonalities = new[] { "donald trump", "jesus", "a clown", "a child", "karl marx", 
        "shakespeare", "martin luther king", "greta thunberg", "albert einstein", "santa", "a horrible person",
        "maggie thatcher", "gollum", "david attenborough", "stephen hawking", "homer simpson",
        "your mum", "your dad", "a silly goose", "a farmer", "dracula", "satan", "a sex pervert",
        "cardi b", "churchill", "dr dre", "taylor swift", "jack the ripper", "elon musk", "bill gates", "nigel farage",
        "barack obama", "the king", "an alcoholic", "pablo escobar", "lara croft", "a pirate", "gandhi", "freud" };

    private readonly ILogger<RoomManager> _logger;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IHubContext<AdminHub> _adminHubContext;
    private readonly WordService _wordService;
    private readonly IPointService _pointService;
    private readonly IWordAnalysisService _wordAnalyst;
    private readonly BotFactory _botFactory;
    private readonly ChatGptService _chatGptService;

    private readonly DateTime _createdOn = DateTime.Now;

    private InternalRoundState _internalRoundState;
    private GameParameters _gameParameters;

    private List<InternalRoundState> _previousRoundStates = new List<InternalRoundState>();
    private List<string> _usedBotPersonalities = new List<string>();
    private List<ChatMessage> _chatMessages = new List<ChatMessage>();

    // TODO have this a mapping to alias only (shouldn't be handling players or boards here)
    private readonly ConcurrentDictionary<string, Player> _playersByConnectionId =
        new ConcurrentDictionary<string, Player>();

    private readonly ConcurrentDictionary<string, object> _adminConnections = new ConcurrentDictionary<string, object>();
    private readonly ConcurrentDictionary<string, object> _tvConnections = new ConcurrentDictionary<string, object>();

    private string? _adminConnectionId;
    private readonly string _roomCode;

    private readonly object _stateLock = new object();
    private readonly object _chatLock = new object();

    private Timer? _startTimer;
    private List<(Timer Timer, Player[] Players)> _guessTimers = new();

    private readonly Func<Task> _roomSummaryUpdatedCallback;

    public Room(
        IHubContext<GameHub> hubContext, IHubContext<AdminHub> adminHubContext,
        WordService wordService,
        IPointService pointService,
        ILogger<RoomManager> logger,
        IWordAnalysisService wordAnalyst,
        string roomCode,
        Func<Task> roomSummaryUpdatedCallback,
        BotFactory botFactory, 
        ChatGptService chatGptService)
    {
        _hubContext = hubContext;
        _adminHubContext = adminHubContext;
        _wordService = wordService;
        _pointService = pointService;
        _logger = logger;
        _wordAnalyst = wordAnalyst;
        _roomSummaryUpdatedCallback = roomSummaryUpdatedCallback;
        _roomCode = roomCode;
        _gameParameters = GameParameters.GetDefault();
        // TODO leave null until game has started? 
        _internalRoundState = new InternalRoundState(wordService.GetRandomWord(_gameParameters.AnswerList), _pointService, _gameParameters, _wordService);
        _botFactory = botFactory;
        _chatGptService = chatGptService;
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
        _gameParameters = GameParameters.GetDefault();
        _internalRoundState = new InternalRoundState(_wordService.GetRandomWord(_gameParameters.AnswerList), _pointService, _gameParameters, _wordService);
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

    public Task<GameParameters> GetGameParameters() => Task.FromResult(this._gameParameters);

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
        bool paramsChanged = false;
        lock (_stateLock)
        {
            _playersByConnectionId.Remove(connectionId, out _);
            player = _internalRoundState.RegisterPlayer(alias, connectionId, ipAddress);
            _playersByConnectionId[connectionId] = player;

            // make sure player has a timer
            if (_internalRoundState.Status == RoundStatus.Playing && !_guessTimers.Any(x => x.Players.Contains(player)))
            {
                var timeUntilDeadline = player.Board!.NextGuessDeadline!.Value - DateTime.Now;
                var timer = new Timer(timeUntilDeadline.TotalMilliseconds);
                timer.Elapsed +=
                    async (sender, e) => await GuessDeadlineReached(timer, new[] { player } );
                timer.Start();
                _guessTimers.Add((timer, new[] { player }));
            }

            // make first player admin
            if (_adminConnectionId == null)
            {
                _adminConnectionId = connectionId;
                _gameParameters.AdminAlias = alias;
                paramsChanged = true;
            }
            // if admin has refreshed
            else if (_playersByConnectionId.TryGetValue(_adminConnectionId, out var admin) &&
                admin == player &&
                connectionId != _adminConnectionId)
            {
                _adminConnectionId = connectionId;
            }

            maskedRoundState = _internalRoundState.Mask();
        }
        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        if (paramsChanged)
            await BroadcastParameters();
        await _roomSummaryUpdatedCallback();

        // TODO replace this with admin option
        if (_adminConnectionId == connectionId)
        {
            //var personality1 = _botPersonalities.PickRandom();
            //await AddBot(connectionId, personality1);
            //var personality2 = _botPersonalities.PickRandom();
            //await AddBot(connectionId, personality2);
            //var personality3 = _botPersonalities.PickRandom();
            //await AddBot(connectionId, personality3);
            //await AddBot(connectionId, _botPersonalities.PickRandom());
            //await AddBot(connectionId, _botPersonalities.PickRandom());
            //await AddBot(connectionId, _botPersonalities.PickRandom());
        }

        return player;
    }

    public async Task<Player> AddBot(string connectionId, string? personality)
    {
        if (connectionId != _adminConnectionId && !_adminConnections.ContainsKey(connectionId))
            throw new InvalidOperationException("Connection does not have permission to change parameters.");

        IBot bot;
        Player? player;
        MaskedRoundState maskedRoundState;

        lock (_stateLock)
        {
            if (string.IsNullOrEmpty(personality))
            {
                var unchosenPersonalities = _botPersonalities.Except(_usedBotPersonalities).ToArray();
                personality = unchosenPersonalities.PickRandom();
            }

            _usedBotPersonalities.Add(personality);
            bot = _botFactory.CreateBot(new BotParams(BotType.ChatGptPersonality, personality));
            //var smackTalk = await bot.GetSmackTalk();

            var existingCount = _internalRoundState.Players.Count(x => x.Alias.Contains(personality));
            var alias = existingCount > 0 ? $"{personality} {existingCount + 1}" : personality;
            player = _internalRoundState.RegisterBot(bot, alias);
            _playersByConnectionId[player.ConnectionId] = player;

            maskedRoundState = _internalRoundState.Mask();
        }

        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        await _roomSummaryUpdatedCallback();

        Task.Run(async () =>
        {
            try
            {
                await bot.Init();
                await ToggleReady(true, player.ConnectionId);
                var smackTalk = await bot.GetSmackTalk();
                if (smackTalk != null)
                {
                    await NotifyTyping(player.ConnectionId);
                    var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(1000, 3000));
                    await Task.Delay(delay);
                    await SendChat(player.ConnectionId, smackTalk);
                    await NotifyStoppedTyping(player.ConnectionId);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error initialising bot {player.Alias}");
            }
        });

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
        bool paramsChanged = false;
        lock (_stateLock)
        {
            player = _internalRoundState.RemovePlayer(alias);
            if (_playersByConnectionId.TryRemove(player.ConnectionId, out _))
            {
                // TODO call JS disconnect function
            }

            if (_adminConnectionId == player.ConnectionId)
            {
                _adminConnectionId = _playersByConnectionId.Count > 0 ? _playersByConnectionId.First().Key : null;
                _gameParameters.AdminAlias = _playersByConnectionId.Count > 0 ? _playersByConnectionId.First().Value.Alias : null;
                paramsChanged = true;
            }
            
            if (_internalRoundState.Status == RoundStatus.Playing &&
                _internalRoundState.Players.All(x => x.Board.Status is BoardStatus.Solved or BoardStatus.Failed))
                _internalRoundState.Finish();
            if (_internalRoundState.Status == RoundStatus.Waiting &&
                _internalRoundState.Players.All(x => x.Ready))
                _internalRoundState.Status = RoundStatus.Ready;
                
            maskedRoundState = _internalRoundState.Mask();
        }
        
        await BroadcastRoundState(_internalRoundState, maskedRoundState, player.ConnectionId);
        if (paramsChanged)
            await BroadcastParameters();
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

            var newRoundNumber = 1;
            if (_internalRoundState.Status == RoundStatus.Finished)
            {
                _previousRoundStates.Add(_internalRoundState);
                newRoundNumber = _internalRoundState.RoundNumber + 1;
                // TODO remove any players no longer connected?
            }

            _internalRoundState = new InternalRoundState(_wordService.GetRandomWord(_gameParameters.AnswerList),
                _internalRoundState.Players, newRoundNumber, _pointService, _gameParameters.Clone(), _wordService);

            _internalRoundState.StartNew(DateTime.Now.AddSeconds(StartCountdownSeconds));
            
            _startTimer?.Stop();
            _startTimer = new System.Timers.Timer(StartCountdownSeconds * 1000);
            _startTimer.Elapsed += async ( sender, e ) => await StartRoundInternal();
            _startTimer.Start();

            _guessTimers.ForEach(x => x.Timer.Stop());

            maskedRoundState = _internalRoundState.Mask();
        }
        
        await BroadcastRoundState(_internalRoundState, maskedRoundState);
        var allConnections = _internalRoundState.Players.Where(x => !x.IsBot).Select(x => x.ConnectionId)
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

                // work out player time limit handicaps
                var playerTimeLimits = _internalRoundState.Players.GroupBy(x => x.Board?.NextGuessDeadline);
                _guessTimers = playerTimeLimits.Where(grp => grp.Key != null).Select(grp =>
                {
                    var players = grp.ToArray();
                    var timeUntilDeadline = grp.Key.Value - DateTime.Now;
                    var timer = new Timer(timeUntilDeadline.TotalMilliseconds);
                    timer.Elapsed += 
                        async (sender, e) => await GuessDeadlineReached(timer, players);
                    timer.Start();
                    return (timer, players);
                }).ToList();
            }

            InitBotGuesses();

            _logger.LogInformation($"Broadcasting all initial player boards.");
            foreach (var player in _internalRoundState.Players.Where(x => !x.IsBot))
            {
                await _hubContext.Clients.Clients(player.ConnectionId).SendAsync("BoardUpdated", player.Board);
            }
            _logger.LogInformation("Finished broadcasting initial player boards.");

            await BroadcastRoundState(_internalRoundState, maskedRoundState);
            await _roomSummaryUpdatedCallback();
        }
    }

    private async Task GuessDeadlineReached(Timer timer, Player[] players)
    {
        using (LogContext.Create(_logger, "Timer", "GuessDeadlineReached"))
        {
            MaskedRoundState maskedRoundState;
            Player[] impactedPlayers;
            lock (_stateLock)
            {
                if (_internalRoundState.Status != RoundStatus.Playing)
                {
                    timer.Stop();
                    return;
                }
            
                impactedPlayers = _internalRoundState.ForceGuess(_wordService, players);
                maskedRoundState = _internalRoundState.Mask();
            }

            _logger.LogInformation($"Broadcasting {impactedPlayers.Length} updated player boards.");
            foreach (var player in impactedPlayers.Where(x => !x.IsBot))
            {
                await _hubContext.Clients.Clients(player.ConnectionId).SendAsync("BoardUpdated", player.Board);
            }
            _logger.LogInformation("Finished broadcasting updated player boards.");
        
            await BroadcastRoundState(_internalRoundState, maskedRoundState);
        }
    }

    private void InitBotGuesses()
    {
        using (LogContext.Create(_logger, "Bots", "PlayBotGuesses"))
        {
            var bots = _internalRoundState.Players.Where(x => x.IsBot && x.Board?.IsFinished == false).ToArray();
            foreach (var player in bots)
            {
                var botGuessTask = Task.Run(async () =>
                {
                    try
                    {
                        var (guess, guessNumber, delay) = await _internalRoundState.GetBotGuess(player);
                        await PlayDelayedBotGuess(player, guess, guessNumber, delay);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Error on bot guess thread {player.Alias}");
                    }
                });
            }
        }
    }

    private async Task PlayDelayedBotGuess(Player player, string guess, int guessNumber, TimeSpan delay)
    {
        await Task.Delay(delay);
        var result = await PlayGuess(player.ConnectionId, guess, guessNumber);
        if (!result.IsSuccess)
            throw new Exception($"PlayGuess failed with error: {result.ErrorMessage}");
        if (result.Response.IsFinished)
            return;

        var (nextGuess, nextGuessNumber, nextDelay) = await _internalRoundState.GetBotGuess(player);
        await PlayDelayedBotGuess(player, nextGuess, nextGuessNumber, nextDelay);
    }

    public async Task<Result<Board>> PlayGuess(string connectionId, string guess, int guessNumber)
    {
        _logger.LogInformation($"PlayGuess({connectionId}, {guess}, {guessNumber})");

        if (guess.Length != _internalRoundState.CorrectAnswer.Length)
            return new Result<Board>("Incorrect length");

        if (!_wordService.IsWordAccepted(guess))
            return new Result<Board>("Not in word list");

        var similarWords = new (string, int)[] { };
        //var similarWords = await _wordAnalyst.GetSimilarWords(guess);

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

    public async Task<Result<Board>> GiveUp(string connectionId)
    {
        Board board;
        MaskedRoundState maskedRoundState;
        lock (_stateLock)
        {
            var player = _playersByConnectionId[connectionId];
            board = _internalRoundState.GiveUp(player.Alias);
            maskedRoundState = _internalRoundState.Mask();
        }

        await BroadcastRoundState(_internalRoundState, maskedRoundState);

        return new Result<Board>(board);
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
            var allConnections = _internalRoundState.Players.Where(x => !x.IsBot).Select(x => x.ConnectionId)
                .Concat(_tvConnections.Keys)
                .Concat(_adminConnections.Keys)
                .Concat(additionalConnectionIds);
            await _hubContext.Clients.Clients(allConnections).SendAsync("GameStateUpdated", internalRoundState);
            return;
        }
        
        var completeConnectionIds = _internalRoundState.Players
            .Where(x => x.Board is { IsFinished: true } && !x.IsBot)
            .Select(x => x.ConnectionId!)
            .Concat(_adminConnections.Keys)
            .ToArray();
        var maskedConnectionIds = _internalRoundState.Players
            .Where(x => x.Board is not { IsFinished: true } && !x.IsBot)
            .Select(x => x.ConnectionId!)
            .Concat(_tvConnections.Keys)
            .Concat(additionalConnectionIds)
            .ToArray();
        await _hubContext.Clients.Clients(completeConnectionIds).SendAsync("GameStateUpdated", internalRoundState);
        await _hubContext.Clients.Clients(maskedConnectionIds).SendAsync("GameStateUpdated", maskedRoundState);
        _logger.LogInformation($"Finished broadcasting round state ({(DateTime.Now - startTime).TotalMilliseconds:N1}ms).");
    }

    private async Task BroadcastParameters()
    {
        _logger.LogInformation("Broadcasting parameters.");
        var startTime = DateTime.Now;
        var allConnections = _internalRoundState.Players.Where(x => !x.IsBot).Select(x => x.ConnectionId)
            .Concat(_tvConnections.Keys)
            .Concat(_adminConnections.Keys);
        await _hubContext.Clients.Clients(allConnections).SendAsync("GameParametersUpdated", _gameParameters);

        _logger.LogInformation($"Finished broadcasting parameters ({(DateTime.Now - startTime).TotalMilliseconds:N1}ms).");
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

    public async Task UpdateGameParameters(string connectionId, Action<GameParameters> callback)
    {
        if (connectionId != _adminConnectionId && !_adminConnections.ContainsKey(connectionId))
            throw new InvalidOperationException("Connection does not have permission to change parameters.");

        callback(_gameParameters);

        await BroadcastParameters();
    }

    public async Task SendChat(string connectionId, string message)
    {
        if (!_playersByConnectionId.TryGetValue(connectionId, out var player))
        {
            throw new HubException("Unrecognised connection");
        }

        var chatMessage = new ChatMessage(player.Alias, DateTime.Now, message);

        lock (_chatLock) 
        {
            _chatMessages.Add(chatMessage);
        }

        var allConnections = _internalRoundState.Players.Where(x => !x.IsBot).Select(x => x.ConnectionId)
            .Concat(_tvConnections.Keys)
            .Concat(_adminConnections.Keys);
        await _hubContext.Clients.Clients(allConnections).SendAsync("ChatMessageReceived", chatMessage);

        // If a player sent it, ask a bot for a reply
        if (!player.IsBot && _internalRoundState.Players.Any(x => x.IsBot))
        {
            Player? recipient = message.StartsWith("@")
                ? _internalRoundState.Players.FirstOrDefault(x => message.StartsWith($"@{x.Alias}", StringComparison.InvariantCultureIgnoreCase))
                : _internalRoundState.Players.Where(x => x.IsBot).ToArray().PickRandom();

            if (recipient?.Bot != null)
            {
                var strippedMessage = message.Replace($"@{recipient.Alias} ", "", StringComparison.InvariantCultureIgnoreCase);
                Task.Run(async () =>
                {
                    try
                    {
                        var reply = await recipient.Bot.GetChatReply(strippedMessage);
                        if (reply != null)
                        {
                            reply = $"@{player.Alias} {reply}";
                            await NotifyTyping(recipient.ConnectionId);
                            var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(1000, 3000));
                            await Task.Delay(delay);
                            await SendChat(recipient.ConnectionId, reply);
                            await NotifyStoppedTyping(recipient.ConnectionId);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Error asking bot for chat reply");
                    }
                });
            }
        }
    }

    public async Task NotifyTyping(string connectionId)
    {
        if (!_playersByConnectionId.TryGetValue(connectionId, out var player))
            return;

        var otherConnections = _internalRoundState.Players.Where(x => !x.IsBot && x.ConnectionId != connectionId)
            .Select(x => x.ConnectionId)
            .Concat(_tvConnections.Keys)
            .Concat(_adminConnections.Keys);

        await _hubContext.Clients.Clients(otherConnections).SendAsync("PlayerTyping", player.Alias);
    }

    public async Task NotifyStoppedTyping(string connectionId)
    {
        if (!_playersByConnectionId.TryGetValue(connectionId, out var player))
            return;

        var otherConnections = _internalRoundState.Players.Where(x => !x.IsBot && x.ConnectionId != connectionId)
            .Select(x => x.ConnectionId)
            .Concat(_tvConnections.Keys)
            .Concat(_adminConnections.Keys);

        await _hubContext.Clients.Clients(otherConnections).SendAsync("PlayerStoppedTyping", player.Alias);
    }


    public ChatMessage[] GetChatMessages() => _chatMessages.ToArray();
}