using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using ChatGpt;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Turdle.Bots;
using Turdle.Hubs;
using Turdle.Models;
using Turdle.Persistence;
using Turdle.ViewModel;

namespace Turdle;

public class RoomManager
{
    private static readonly Random _random = new Random();
    private const string RoomCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly string[] _adjectives;
    private readonly string[] _animals;

    private readonly ILogger<RoomManager> _logger;
    private readonly IHubContext<GameHub> _gameHubContext;
    private readonly IHubContext<AdminHub> _adminHubContext;
    private readonly IHubContext<HomeHub> _homeHubContext;
    private readonly WordService _wordService;
    private readonly IPointService _pointService;
    private readonly IWordAnalysisService _wordAnalyst;
    private readonly BotFactory _botFactory;
    private readonly PersonalityAvatarService _avatarService;
    private readonly RoomAvatarService _roomAvatarService;
    private readonly RoomBufferSettings _roomBufferSettings;
    private readonly IRoomStateRepository _roomStateRepository;

    private readonly Board _fakeReadyBoard;

    private readonly ConcurrentDictionary<string, object> _homeConnections = new ConcurrentDictionary<string, object>();
    private readonly ConcurrentDictionary<string, Room> _roomConnectionCache = new ConcurrentDictionary<string, Room>();
    private readonly ConcurrentDictionary<string, Room> _rooms = new ConcurrentDictionary<string, Room>();
    private readonly ConcurrentQueue<Room> _prewarmedRooms = new ConcurrentQueue<Room>();

    private readonly object _roomCodeReservationLock = new object();
    private readonly HashSet<string> _prewarmedRoomCodes = new HashSet<string>();
    private readonly SemaphoreSlim _warmupSemaphore = new SemaphoreSlim(1, 1);

    public RoomManager(
        ILogger<RoomManager> logger,
        IHubContext<GameHub> gameHubContext,
        IHubContext<AdminHub> adminHubContext,
        IHubContext<HomeHub> homeHubContext,
        WordService wordService,
        IPointService pointService,
        IWordAnalysisService wordAnalyst,
        BotFactory botFactory,
        PersonalityAvatarService avatarService,
        RoomAvatarService roomAvatarService,
        IOptions<RoomBufferSettings> roomBufferSettings,
        IRoomStateRepository roomStateRepository)
    {
        _logger = logger;
        _gameHubContext = gameHubContext;
        _adminHubContext = adminHubContext;
        _wordService = wordService;
        _pointService = pointService;
        _wordAnalyst = wordAnalyst;
        _homeHubContext = homeHubContext;
        _botFactory = botFactory;
        _avatarService = avatarService;
        _roomAvatarService = roomAvatarService;
        _roomBufferSettings = roomBufferSettings.Value;
        _roomStateRepository = roomStateRepository;

        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("Turdle.Resources.Adjectives.txt"))
        using (var reader = new StreamReader(stream!))
        {
            _adjectives = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        using (var stream = assembly.GetManifestResourceStream("Turdle.Resources.Animals.txt"))
        using (var reader = new StreamReader(stream!))
        {
            _animals = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        _fakeReadyBoard = new Board();
        _fakeReadyBoard.AddRow("EVERY", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("GAMER", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("SEEMS", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("READY", "START", null, null, 1, pointService);

        if (_roomBufferSettings.TargetSize > 0)
        {
            _ = Task.Run(InitialisePrewarmedBuffer);
        }
    }

    public async Task<string> CreateRoom(string adminConnectionId)
    {
        if (TryTakePrewarmedRoom(out var prewarmedRoom))
        {
            if (!_rooms.TryAdd(prewarmedRoom.RoomCode, prewarmedRoom))
            {
                throw new InvalidOperationException($"Prewarmed room code collision: {prewarmedRoom.RoomCode}");
            }

            _logger.LogInformation(
                "Using prewarmed room {roomCode}. Remaining buffered rooms: {bufferCount}",
                prewarmedRoom.RoomCode,
                _prewarmedRooms.Count);

            if (_roomBufferSettings.TargetSize > 0)
            {
                _ = Task.Run(FillPrewarmedBuffer);
            }

            await PersistRoom(prewarmedRoom, isBuffered: false);
            await BroadcastRooms();
            return prewarmedRoom.RoomCode;
        }

        var roomCode = ReserveNewRoomCode(reserveForBuffer: false);
        var room = CreateRoomInstance(roomCode);
        _rooms.TryAdd(roomCode, room);

        _logger.LogInformation("Initialising new room because prewarmed buffer is empty");
        await room.Init();

        await PersistRoom(room, isBuffered: false);
        await BroadcastRooms();
        return roomCode;
    }

    private async Task InitialisePrewarmedBuffer()
    {
        await RestorePersistedPrewarmedRooms();
        await FillPrewarmedBuffer();
    }

    private async Task RestorePersistedPrewarmedRooms()
    {
        var bufferedSnapshots = await _roomStateRepository.GetBuffered();
        foreach (var snapshot in bufferedSnapshots)
        {
            if (_prewarmedRooms.Count >= _roomBufferSettings.TargetSize)
            {
                break;
            }

            if (_rooms.ContainsKey(snapshot.RoomCode))
            {
                continue;
            }

            _logger.LogInformation($"Loaded prewarmed room {snapshot.RoomCode} into buffer from DB");
            AddPrewarmedRoomCodeReservation(snapshot.RoomCode);
            var room = CreateRoomInstance(snapshot.RoomCode);
            room.RestoreFromSnapshot(snapshot);
            _prewarmedRooms.Enqueue(room);
        }

        if (bufferedSnapshots.Count > 0)
        {
            _logger.LogInformation(
                "Reloaded {bufferedCount} prewarmed rooms from SQLite. Buffered rooms: {queueCount}",
                bufferedSnapshots.Count,
                _prewarmedRooms.Count);
        }
    }

    public async Task<Room> GetRoom(string roomCode, string? connectionId = null)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            room = await TryLoadPersistedRoom(roomCode);
            if (room != null)
                _rooms.TryAdd(roomCode, room);
        }

        if (room != null)
        {
            if (connectionId != null)
            {
                _roomConnectionCache.TryAdd(connectionId, room);
            }

            return room;
        }

        throw new Exception("Room does not exist");
    }

    public RoomSummary[] GetRooms()
    {
        return _rooms.Values.Select(x => x.ToSummary()).ToArray();
    }

    private async Task BroadcastRooms()
    {
        var rooms = GetRooms();
        await _homeHubContext.Clients.Clients(_homeConnections.Keys).SendAsync("RoomsUpdated", rooms);
    }

    public Board GetFakeReadyBoard()
    {
        return _fakeReadyBoard;
    }

    public void RegisterHomeConnection(string connectionId)
    {
        _homeConnections.TryAdd(connectionId, null);
    }

    public void DisconnectHome(string connectionId)
    {
        _homeConnections.TryRemove(connectionId, out _);
    }

    public async Task PlayerDisconnected(string connectionId)
    {
        // TODO Remove this logic so we can reconnect again?
        if (_roomConnectionCache.TryGetValue(connectionId, out var room))
        {
            await room.PlayerDisconnected(connectionId);
        }

        _roomConnectionCache.TryRemove(connectionId, out _);
    }

    public async Task PlayerReconnected(string connectionId)
    {
        if (_roomConnectionCache.TryGetValue(connectionId, out var room))
        {
            await room.PlayerReconnected(connectionId);
        }

        _roomConnectionCache.TryRemove(connectionId, out _);
    }

    private async Task FillPrewarmedBuffer()
    {
        await _warmupSemaphore.WaitAsync();
        try
        {
            while (_prewarmedRooms.Count < _roomBufferSettings.TargetSize)
            {
                var roomCode = ReserveNewRoomCode(reserveForBuffer: true);

                try
                {
                    var room = CreateRoomInstance(roomCode);
                    _logger.LogInformation("Prewarming room {roomCode}", roomCode);
                    await room.Init();
                    _prewarmedRooms.Enqueue(room);
                    await PersistRoom(room, isBuffered: true);
                    _logger.LogInformation(
                        "Prewarmed room {roomCode}. Buffered rooms: {bufferCount}",
                        roomCode,
                        _prewarmedRooms.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to prewarm room {roomCode}", roomCode);
                    ReleasePrewarmedRoomCode(roomCode);
                    await Task.Delay(TimeSpan.FromSeconds(_roomBufferSettings.RetryDelaySeconds));
                }
            }
        }
        finally
        {
            _warmupSemaphore.Release();
        }
    }

    private Room CreateRoomInstance(string roomCode)
    {
        return new Room(
            _gameHubContext,
            _adminHubContext,
            _wordService,
            _pointService,
            _logger,
            _wordAnalyst,
            roomCode,
            BroadcastRooms,
            _botFactory,
            _avatarService,
            _roomAvatarService,
                snapshot => PersistRoom(snapshot, isBuffered: false));
    }

    private async Task<Room?> TryLoadPersistedRoom(string roomCode)
    {
        var snapshot = await _roomStateRepository.Get(roomCode);
        if (snapshot == null)
        {
            return null;
        }

        var loadedRoom = CreateRoomInstance(roomCode);
        loadedRoom.RestoreFromSnapshot(snapshot);

        if (_rooms.TryAdd(roomCode, loadedRoom))
        {
            _logger.LogInformation("Restored room {roomCode} from SQLite", roomCode);
            return loadedRoom;
        }

        _rooms.TryGetValue(roomCode, out var existing);
        return existing;
    }

    private Task PersistRoom(Room room, bool isBuffered) => PersistRoom(room.ToSnapshot(), isBuffered);

    private Task PersistRoom(RoomStateSnapshot snapshot, bool isBuffered)
    {
        snapshot.IsBuffered = isBuffered;
        return _roomStateRepository.Upsert(snapshot);
    }

    private bool TryTakePrewarmedRoom(out Room room)
    {
        if (_prewarmedRooms.TryDequeue(out room!))
        {
            ReleasePrewarmedRoomCode(room.RoomCode);
            return true;
        }

        room = null!;
        return false;
    }

    private string ReserveNewRoomCode(bool reserveForBuffer)
    {
        lock (_roomCodeReservationLock)
        {
            var roomCode = "";
            while (roomCode == "" || _rooms.ContainsKey(roomCode) || _prewarmedRoomCodes.Contains(roomCode))
            {
                roomCode = GenerateRoomCode();
            }

            if (reserveForBuffer)
            {
                _prewarmedRoomCodes.Add(roomCode);
            }

            return roomCode;
        }
    }

    private void ReleasePrewarmedRoomCode(string roomCode)
    {
        lock (_roomCodeReservationLock)
        {
            _prewarmedRoomCodes.Remove(roomCode);
        }
    }

    private void AddPrewarmedRoomCodeReservation(string roomCode)
    {
        lock (_roomCodeReservationLock)
        {
            _prewarmedRoomCodes.Add(roomCode);
        }
    }

    private string GenerateRoomCode()
    {
        // Keep the original 5 letter code generation for reference, but this path is never taken.
        if (false)
        {
            var code = "";
            for (var i = 0; i < 5; i++)
            {
                code += RoomCodeCharacters[_random.Next(RoomCodeCharacters.Length)];
            }

            return code;
        }

        var adjective = _adjectives[_random.Next(_adjectives.Length)];
        var animal = _animals[_random.Next(_animals.Length)];
        return adjective + animal;
    }
}
