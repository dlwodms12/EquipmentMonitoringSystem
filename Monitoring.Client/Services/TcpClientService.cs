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

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Task? _receiveTask;
    private Task? _pingTask;

    private string? _deviceId;
    private string? _deviceName;

    private bool _isRegistered;

    public event Action<string>? LogReceived;
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public async Task<bool> ConnectAsync(
        string serverIp,
        int serverPort)
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

            // 장비 목록 응답을 받아야 하므로,
            // Register보다 먼저 수신 반복을 시작한다.
            _receiveTask = ReceiveLoopAsync(_cts.Token);

            return true;
        }
        catch (Exception ex)
        {
            ConnectionChanged?.Invoke(false);
            LogReceived?.Invoke($"연결 실패: {ex.Message}");

            return false;
        }
    }

    public async Task RequestDeviceListAsync()
    {
        NetworkMessage request = new()
        {
            Type = MessageType.DeviceListRequest,
            SentAt = DateTime.Now
        };

        await SendAsync(request);
    }

    public async Task RegisterAsync(
    string deviceId,
    string deviceName)
    {
        if (_writer is null)
        {
            LogReceived?.Invoke(
                "서버에 연결되어 있지 않아 등록할 수 없습니다.");

            return;
        }

        if (_isRegistered)
        {
            LogReceived?.Invoke("이미 장비 등록이 완료되었습니다.");
            return;
        }

        _deviceId = deviceId;
        _deviceName = deviceName;

        NetworkMessage registerMessage = new()
        {
            Type = MessageType.Register,
            DeviceId = _deviceId,
            DeviceName = _deviceName,
            SentAt = DateTime.Now
        };

        await SendAsync(registerMessage);
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
                    LogReceived?.Invoke(
                        "서버 연결이 종료되었습니다.");

                    ConnectionChanged?.Invoke(false);
                    break;
                }

                NetworkMessage? message =
                    JsonSerializer.Deserialize<NetworkMessage>(json);

                if (message is null)
                {
                    continue;
                }

                LogReceived?.Invoke($"RX SERVER {message.Type}");

                if (message.Type == MessageType.RegisterAccepted)
                {
                    _isRegistered = true;

                    if (_cts is not null && _pingTask is null)
                    {
                        _pingTask = PingLoopAsync(_cts.Token);
                    }
                }

                if (message.Type == MessageType.RegisterRejected)
                {
                    _deviceId = null;
                    _deviceName = null;

                    LogReceived?.Invoke(
                        $"장비 등록 거절: {message.Status}");
                }

                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            // 창 종료 시 정상적으로 발생
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                LogReceived?.Invoke($"수신 오류: {ex.Message}");
                ConnectionChanged?.Invoke(false);
            }
        }
    }

    private async Task PingLoopAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_deviceId) ||
            string.IsNullOrWhiteSpace(_deviceName))
        {
            return;
        }

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
                    DeviceId = _deviceId,
                    DeviceName = _deviceName,
                    RequestId = Guid.NewGuid().ToString(),
                    SentAt = DateTime.Now
                };

                await SendAsync(pingMessage);
            }
        }
        catch (OperationCanceledException)
        {
            // 창 종료 시 정상적으로 발생
        }
    }

    public async Task SendAsync(NetworkMessage message)
    {
        await _sendLock.WaitAsync();

        try
        {
            if (_writer is null)
            {
                LogReceived?.Invoke(
                    "서버에 연결되어 있지 않습니다.");

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

        _writer = null;
        _reader = null;
        _client = null;

        ConnectionChanged?.Invoke(false);

        _isRegistered = false;
        _deviceId = null;
        _deviceName = null;
    }
}