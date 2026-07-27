using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Monitoring.Shared.Models;

const int port = 5000;

var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();

Console.WriteLine($"서버가 {port} 포트에서 접속을 기다립니다.");

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();

    Console.WriteLine(
        $"클라이언트 접속: {client.Client.RemoteEndPoint}");

    _ = HandleClientAsync(client);
}

static async Task HandleClientAsync(TcpClient client)
{
    using (client)
    {
        using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream);
        using StreamWriter writer = new(stream) { AutoFlush = true };

        try
        {
            while (true)
            {
                string? json = await reader.ReadLineAsync();

                if (json is null)
                {
                    Console.WriteLine("클라이언트 연결이 종료되었습니다.");
                    break;
                }

                NetworkMessage? message =
                    JsonSerializer.Deserialize<NetworkMessage>(json);

                if (message is null)
                {
                    continue;
                }

                Console.WriteLine(
                    $"RX | {message.Type} | {message.DeviceId}");

                if (message.Type == MessageType.Register)
                {
                    Console.WriteLine(
                        $"장비 등록 완료: {message.DeviceId} / {message.DeviceName}");
                }

                if (message.Type == MessageType.PingRequest)
                {
                    NetworkMessage response = new()
                    {
                        Type = MessageType.PingResponse,
                        DeviceId = message.DeviceId,
                        DeviceName = message.DeviceName,
                        RequestId = message.RequestId,
                        SentAt = DateTime.Now
                    };

                    string responseJson =
                        JsonSerializer.Serialize(response);

                    await writer.WriteLineAsync(responseJson);

                    Console.WriteLine(
                        $"TX | PingResponse | {message.DeviceId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"통신 오류: {ex.Message}");
        }
    }
    
}
