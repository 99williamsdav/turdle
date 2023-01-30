﻿using System.Collections.Concurrent;
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
    private readonly IHubContext<GameHub> _gameHubContext;
    private readonly IHubContext<AdminHub> _adminHubContext;
    private readonly IHubContext<HomeHub> _homeHubContext;
    private readonly WordService _wordService;
    private readonly IPointService _pointService;
    private readonly IWordAnalysisService _wordAnalyst;
    private readonly Board _fakeReadyBoard;
    
    private readonly ConcurrentDictionary<string, object> _homeConnections = new ConcurrentDictionary<string, object>();
    private readonly ConcurrentDictionary<string, Room> _roomConnectionCache = new ConcurrentDictionary<string, Room>();

    private readonly ConcurrentDictionary<string, Room> _rooms = new ConcurrentDictionary<string, Room>();

    public RoomManager(ILogger<RoomManager> logger, IHubContext<GameHub> gameHubContext, IHubContext<AdminHub> adminHubContext, IHubContext<HomeHub> homeHubContext, 
        WordService wordService, IPointService pointService, IWordAnalysisService wordAnalyst)
    {
        _logger = logger;
        _gameHubContext = gameHubContext;
        _adminHubContext = adminHubContext;
        _wordService = wordService;
        _pointService = pointService;
        _wordAnalyst = wordAnalyst;
        _homeHubContext = homeHubContext;

        _fakeReadyBoard = new Board();
        _fakeReadyBoard.AddRow("EVERY", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("GAMER", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("SEEMS", "START", null, null, 1, pointService);
        _fakeReadyBoard.AddRow("READY", "START", null, null, 1, pointService);
    }
    
    public async Task<string> CreateRoom(string adminConnectionId)
    {
        var roomCode = "";
        while (roomCode == "" || _rooms.ContainsKey(roomCode))
        {
            roomCode = GenerateRoomCode();
        }

        var room = new Room(_gameHubContext, _adminHubContext, _wordService, _pointService, _logger, _wordAnalyst,
            roomCode, BroadcastRooms);
        _rooms.TryAdd(roomCode, room);

        await BroadcastRooms();
        return roomCode;
    }
    
    public Room GetRoom(string roomCode, string? connectionId = null)
    {
        if (_rooms.TryGetValue(roomCode, out var room))
        {
            if (connectionId != null)
                _roomConnectionCache.TryAdd(connectionId, room);
            
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

    private string GenerateRoomCode()
    {
        var roomCode = "";
        for (var i = 0; i < 5; i++)
        {
            roomCode += RoomCodeCharacters[_random.Next(0, RoomCodeCharacters.Length)];
        }
        
        return roomCode;
    }
}