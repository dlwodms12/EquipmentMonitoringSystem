using System.IO;
using Microsoft.Data.Sqlite;
using Monitoring.Server.Models;
using Monitoring.Shared.Models;

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

    // 서버 DB에서 장비 목록을 가져오는 메서드, DeviceSummary 객체 리스트를 반환, 장비 ID와 이름만 포함
    public async Task<List<DeviceSummary>> GetDeviceSummariesAsync()
    {
        List<DeviceSummary> devices = new();

        await using SqliteConnection connection =
            new(_connectionString);

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText = """
        SELECT DeviceId, DeviceName
        FROM Devices
        ORDER BY DeviceId;
        """;

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            devices.Add(new DeviceSummary
            {
                DeviceId = reader.GetString(0),
                DeviceName = reader.GetString(1)
            });
        }

        return devices;
    }
    // 서버 DB에서 장비의 연결 상태를 업데이트하는 메서드, 장비 ID와 연결 상태를 매개변수로 받아 DB를 갱신
    public async Task UpdateConnectionAsync(
    string deviceId,
    bool isConnected)
    {
        await using SqliteConnection connection =
            new(_connectionString);

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        if (isConnected)
        {
            command.CommandText = """
            UPDATE Devices
            SET IsConnected = 1,
                LastSeen = $lastSeen
            WHERE DeviceId = $deviceId;
            """;

            command.Parameters.AddWithValue(
                "$lastSeen",
                DateTime.UtcNow.ToString("O"));
        }
        else
        {
            command.CommandText = """
            UPDATE Devices
            SET IsConnected = 0
            WHERE DeviceId = $deviceId;
            """;
        }

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        await command.ExecuteNonQueryAsync();
    }

    // 서버 DB에서 장비의 상태 정보를 업데이트하는 메서드, NetworkMessage 객체를 매개변수로 받아 DB를 갱신
    public async Task UpdateSystemInfoAsync(
    NetworkMessage message)
    {
        await using SqliteConnection connection =
            new(_connectionString);

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText = """
        UPDATE Devices
        SET Status = $status,
            Temperature = $temperature,
            Voltage = $voltage,
            Battery = $battery,
            IsConnected = 1,
            LastSeen = $lastSeen
        WHERE DeviceId = $deviceId;
        """;

        command.Parameters.AddWithValue(
            "$status",
            message.Status);

        command.Parameters.AddWithValue(
            "$temperature",
            message.Temperature);

        command.Parameters.AddWithValue(
            "$voltage",
            message.Voltage);

        command.Parameters.AddWithValue(
            "$battery",
            message.Battery);

        command.Parameters.AddWithValue(
            "$lastSeen",
            DateTime.UtcNow.ToString("O"));

        command.Parameters.AddWithValue(
            "$deviceId",
            message.DeviceId);

        await command.ExecuteNonQueryAsync();
    }

    // 서버 DB에서 모든 장비의 연결 상태를 초기화하는 메서드, 서버 시작 시 호출되어 모든 장비를 연결되지 않은 상태로 설정
    public async Task ResetConnectionStatesAsync()
    {
        await using SqliteConnection connection =
            new(_connectionString);

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText = """
        UPDATE Devices
        SET IsConnected = 0;
        """;

        await command.ExecuteNonQueryAsync();
    }
}
