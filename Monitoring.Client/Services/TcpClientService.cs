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
    private Task? _pingTask;

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event Action<string>? LogReceived;
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    // ConnectAsync 메서드는 서버에 비동기적으로 연결을 시도하고, 연결 성공 여부를 반환
    // 연결이 성공하면 NetworkStream을 생성하고 StreamReader와 StreamWriter를 초기화하며, 서버로부터 메시지를 수신하는 ReceiveLoopAsync 작업을 시작
    // 연결 실패 시 예외를 처리하고 로그를 기록
    public async Task<bool> ConnectAsync(string serverIp,int serverPort)
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

            // 서버 메시지를 받는 작업은 연결 직후부터 시작
            _ = ReceiveLoopAsync(_cts.Token);

            return true;
        }
        catch (Exception ex)
        {
            ConnectionChanged?.Invoke(false);
            LogReceived?.Invoke($"연결 실패: {ex.Message}");

            return false;
        }
    }

    // RequestDeviceListAsync 메서드는 서버에 장비 목록 요청 메시지를 전송하는 비동기 메서드
    public Task RequestDeviceListAsync()
    {
        NetworkMessage request = new()
        {
            Type = MessageType.DeviceListRequest,
            SentAt = DateTime.Now
        };

        return SendAsync(request);
    }

    // RegisterAsync 메서드는 서버에 장비 등록 메시지를 전송하고, Ping 작업을 시작하는 비동기 메서드
    public async Task RegisterAsync(
        string deviceId,
        string deviceName)
    {
        NetworkMessage registerMessage = new()
        {
            Type = MessageType.Register,
            DeviceId = deviceId,
            DeviceName = deviceName,
            SentAt = DateTime.Now
        };

        await SendAsync(registerMessage);

        // 이미 Ping 작업이 시작됐다면 다시 시작하지 않음
        if (_pingTask is null && _cts is not null)
        {
            _pingTask = PingLoopAsync(
                deviceId,
                deviceName,
                _cts.Token);
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

                // MainWindow에 메시지를 전달
                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            // 프로그램 종료 시 정상적으로 발생
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
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    token);

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
            // 프로그램 종료 시 정상적으로 발생
        }
    }

    public async Task SendAsync(NetworkMessage message)
    {
        await _sendLock.WaitAsync();

        try
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
        finally
        {
            _sendLock.Release();
        }
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