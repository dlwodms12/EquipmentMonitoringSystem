using System.IO;
using Microsoft.Data.Sqlite;
using Monitoring.Server.Models;

namespace Monitoring.Server.Services;

public class DeviceDatabaseService
{
    private readonly string _connectionString;

    public DeviceDatabaseService()
    {
        string databasePath = Path.Combine(
            AppContext.BaseDirectory,
            "monitoring.db");

        _connectionString = $"Data Source={databasePath}";
    }

    public async Task InitializeAsync()
    {
        await using SqliteConnection connection =
            new(_connectionString);

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Devices (
                DeviceId TEXT PRIMARY KEY,
                DeviceName TEXT NOT NULL,
                Status TEXT NOT NULL,
                Temperature REAL NOT NULL,
                Voltage REAL NOT NULL,
                Battery INTEGER NOT NULL,
                IsConnected INTEGER NOT NULL,
                LastSeen TEXT NULL
            );

            INSERT OR IGNORE INTO Devices
            (DeviceId, DeviceName, Status, Temperature, Voltage, Battery, IsConnected)
            VALUES
            ('Device-001', '장비 1', '정보 없음', 0, 0, 0, 0),
            ('Device-002', '장비 2', '정보 없음', 0, 0, 0, 0),
            ('Device-003', '장비 3', '정보 없음', 0, 0, 0, 0);
            """;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DeviceRecord?> GetDeviceAsync(string deviceId)
    {
        await using SqliteConnection connection =
            new(_connectionString);

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT DeviceId, DeviceName, Status,
                   Temperature, Voltage, Battery,
                   IsConnected, LastSeen
            FROM Devices
            WHERE DeviceId = $deviceId;
            """;

        command.Parameters.AddWithValue("$deviceId", deviceId);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DeviceRecord
        {
            DeviceId = reader.GetString(0),
            DeviceName = reader.GetString(1),
            Status = reader.GetString(2),
            Temperature = reader.GetDouble(3),
            Voltage = reader.GetDouble(4),
            Battery = reader.GetInt32(5),
            IsConnected = reader.GetInt32(6) == 1,
            LastSeen = reader.IsDBNull(7)
                ? null
                : DateTime.Parse(reader.GetString(7))
        };
    }
}
