using Microsoft.AspNetCore.SignalR;
using Turdle.Hubs;
using Turdle.Models;
using Turdle.ViewModel;

namespace Turdle;

public class RoomManager
{
    private static Random _random = new Random();
    private const string RoomCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    
    private readonly ILogger<RoomManager> _logger;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IHubContext<AdminHub> _adminHubContext;
    private readonly WordService _wordService;
    private readonly IPointService _pointService;
    private readonly IWordAnalysisService _wordAnalyst;
    private readonly Board _fakeReadyBoard;

    private readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();

    public RoomManager(ILogger<RoomManager> logger, IHubContext<GameHub> hubContext, IHubContext<AdminHub> adminHubContext, 
        WordService wordService, IPointService pointService, IWordAnalysisService wordAnalyst)
    {
        _logger = logger;
        _hubContext = hubContext;
        _adminHubContext = adminHubContext;
        _wordService = wordService;
        _pointService = pointService;
        _wordAnalyst = wordAnalyst;

        _fakeReadyBoard = new Board();
        _fakeReadyBoard.AddRow("EVERY", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("GAMER", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("SEEMS", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("READY", "START", null, null, 1, pointService);
    }
    
    public string CreateRoom(string adminConnectionId)
    {
        var roomCode = "";
        while (roomCode == "" || _rooms.ContainsKey(roomCode))
        {
            roomCode = GenerateRoomCode();
        }
        
        var room = new Room(_hubContext, _adminHubContext, _wordService, _pointService, _logger, _wordAnalyst, roomCode, adminConnectionId);
        _rooms.Add(roomCode, room);
        return roomCode;
    }

    private string GenerateRoomCode()
    {
        var roomCode = "";
        for (var i = 0; i < 5; i++)
        {
            roomCode += RoomCodeCharacters[_random.Next(0, RoomCodeCharacters.Length)];
        }
        
        return roomCode;
    }
    
    public Room GetRoom(string roomCode)
    {
        if (_rooms.ContainsKey(roomCode))
        {
            return _rooms[roomCode];
        }

        throw new Exception("Room does not exist");
    }

    public RoomSummary[] GetRooms()
    {
        return _rooms.Values.Select(x => x.ToSummary()).ToArray();
    }

    public Board GetFakeReadyBoard()
    {
        return _fakeReadyBoard;
    }
}