using System.Net.Sockets;
using System.Text.Json;
using Monitoring.Shared.Models;

const string serverIp = "127.0.0.1";
const int serverPort = 5000;

const string deviceId = "Device-001";
const string deviceName = "장비 1";

using TcpClient client = new();

Console.WriteLine("서버 연결을 시도합니다.");

// ConnectAsync : 비동기적으로 서버에 연결, 서버가 접속을 허용할 때까지 대기
await client.ConnectAsync(serverIp, serverPort);

Console.WriteLine("서버에 연결되었습니다.");

using NetworkStream stream = client.GetStream();
using StreamReader reader = new(stream);
using StreamWriter writer = new(stream) { AutoFlush = true };

NetworkMessage registerMessage = new()
{
    Type = MessageType.Register,
    DeviceId = deviceId,
    DeviceName = deviceName,
    SentAt = DateTime.Now
};

await SendMessageAsync(writer, registerMessage);

Console.WriteLine("Register 메시지를 전송했습니다.");

while (true)
{
    await Task.Delay(TimeSpan.FromSeconds(5));

    NetworkMessage pingMessage = new()
    {
        Type = MessageType.PingRequest,
        DeviceId = deviceId,
        DeviceName = deviceName,
        RequestId = Guid.NewGuid().ToString(),
        SentAt = DateTime.Now
    };

    await SendMessageAsync(writer, pingMessage);

    Console.WriteLine(
        $"{DateTime.Now:HH:mm:ss} TX | PingRequest");

    string? responseJson = await reader.ReadLineAsync();

    if (responseJson is null)
    {
        Console.WriteLine("서버와의 연결이 끊어졌습니다.");
        break;
    }

    NetworkMessage? response =
        JsonSerializer.Deserialize<NetworkMessage>(responseJson);

    if (response?.Type == MessageType.PingResponse)
    {
        Console.WriteLine(
            $"{DateTime.Now:HH:mm:ss} RX | PingResponse");
    }
}

static async Task SendMessageAsync(
    StreamWriter writer,
    NetworkMessage message)
{
    string json = JsonSerializer.Serialize(message);

    await writer.WriteLineAsync(json);
}
