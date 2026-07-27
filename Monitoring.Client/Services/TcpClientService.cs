using Monitoring.Shared.Models;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;

namespace Monitoring.Client.Services;

public class TcpClientService
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;

    public event Action<string>? LogReceived;
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public async Task ConnectAsync(
        string serverIp,
        int serverPort,
        string deviceId,
        string deviceName)
    {
        _cts = new CancellationTokenSource();

        try
        {
            _client = new TcpClient();

            LogReceived?.Invoke("서버 연결을 시도합니다.");

            await _client.ConnectAsync(
                serverIp,
                serverPort,
                _cts.Token);

            NetworkStream stream = _client.GetStream();

            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };

            ConnectionChanged?.Invoke(true);
            LogReceived?.Invoke("서버에 연결되었습니다.");

            NetworkMessage registerMessage = new()
            {
                Type = MessageType.Register,
                DeviceId = deviceId,
                DeviceName = deviceName,
                SentAt = DateTime.Now
            };

            await SendAsync(registerMessage);

            _ = ReceiveLoopAsync(_cts.Token);
            _ = PingLoopAsync(deviceId, deviceName, _cts.Token);
        }
        catch (Exception ex)
        {
            ConnectionChanged?.Invoke(false);
            LogReceived?.Invoke($"연결 실패: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        if (_reader is null)
        {
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                string? json = await _reader.ReadLineAsync(token);

                if (json is null)
                {
                    LogReceived?.Invoke("서버 연결이 종료되었습니다.");
                    ConnectionChanged?.Invoke(false);
                    break;
                }

                NetworkMessage? message =
                    JsonSerializer.Deserialize<NetworkMessage>(json);

                if (message is null)
                {
                    continue;
                }

                LogReceived?.Invoke(
                    $"RX SERVER {message.Type}");

                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            // 프로그램 종료 시 정상적으로 발생할 수 있음
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"수신 오류: {ex.Message}");
            ConnectionChanged?.Invoke(false);
        }
    }

    private async Task PingLoopAsync(
        string deviceId,
        string deviceName,
        CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 개발 중에는 5초, 완성 시에는 1분으로 변경
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                NetworkMessage pingMessage = new()
                {
                    Type = MessageType.PingRequest,
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    RequestId = Guid.NewGuid().ToString(),
                    SentAt = DateTime.Now
                };

                await SendAsync(pingMessage);
            }
        }
        catch (OperationCanceledException)
        {
            // 프로그램 종료 시 정상적으로 발생할 수 있음
        }
    }

    public async Task SendAsync(NetworkMessage message)
    {
        if (_writer is null)
        {
            LogReceived?.Invoke("서버에 연결되어 있지 않습니다.");
            return;
        }

        string json = JsonSerializer.Serialize(message);

        await _writer.WriteLineAsync(json);

        LogReceived?.Invoke(
            $"TX SERVER {message.Type}");
    }

    public void Disconnect()
    {
        _cts?.Cancel();

        _writer?.Dispose();
        _reader?.Dispose();
        _client?.Dispose();

        ConnectionChanged?.Invoke(false);
    }
}
