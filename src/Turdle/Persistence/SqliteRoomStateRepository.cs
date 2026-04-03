using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Turdle.Persistence;

public class SqliteRoomStateRepository : IRoomStateRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteRoomStateRepository> _logger;

    public SqliteRoomStateRepository(IOptions<RoomPersistenceSettings> settings, ILogger<SqliteRoomStateRepository> logger)
    {
        _logger = logger;
        _connectionString = settings.Value.ConnectionString;
        EnsureDatabaseExists();
    }

    public async Task<RoomStateSnapshot?> Get(string roomCode)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM rooms WHERE room_code = $roomCode LIMIT 1";
        command.Parameters.AddWithValue("$roomCode", roomCode);
        var payload = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<RoomStateSnapshot>(payload);
    }

    public async Task<IReadOnlyCollection<RoomStateSnapshot>> GetBuffered()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM rooms WHERE is_buffered = 1";

        using var reader = await command.ExecuteReaderAsync();
        var snapshots = new List<RoomStateSnapshot>();
        while (await reader.ReadAsync())
        {
            var payload = reader.GetString(0);
            var snapshot = JsonConvert.DeserializeObject<RoomStateSnapshot>(payload);
            if (snapshot != null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    public async Task Upsert(RoomStateSnapshot snapshot)
    {
        var payload = JsonConvert.SerializeObject(snapshot);
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
    INSERT INTO rooms (room_code, payload, created_on_utc, updated_on_utc, is_buffered)
    VALUES ($roomCode, $payload, $created, $updated, $isBuffered)
ON CONFLICT(room_code) DO UPDATE SET
    payload = excluded.payload,
    updated_on_utc = excluded.updated_on_utc,
    is_buffered = excluded.is_buffered";

        command.Parameters.AddWithValue("$roomCode", snapshot.RoomCode);
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$created", snapshot.CreatedOn.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        command.Parameters.AddWithValue("$isBuffered", snapshot.IsBuffered ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    private void EnsureDatabaseExists()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource))
        {
            var directory = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS rooms (
    room_code TEXT NOT NULL PRIMARY KEY,
    payload TEXT NOT NULL,
    created_on_utc TEXT NOT NULL,
    updated_on_utc TEXT NOT NULL,
    is_buffered INTEGER NOT NULL DEFAULT 0
);";
        command.ExecuteNonQuery();

        using var migrateCommand = connection.CreateCommand();
        migrateCommand.CommandText = @"
ALTER TABLE rooms ADD COLUMN is_buffered INTEGER NOT NULL DEFAULT 0;";

        try
        {
            migrateCommand.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name"))
        {
            // Column already exists; safe to continue.
        }

        _logger.LogInformation("SQLite room persistence initialized");
    }
}
