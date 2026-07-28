using Monitoring.Shared.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Linq;

namespace Monitoring.Server.Services;

public class TcpServerService
{
    // ConcurrentDictionary를 사용하여 장비 ID와 연결 정보를 관리
    // ConcurrentDictionary는 멀티스레드 환경에서 안전하게 데이터를 추가, 제거, 조회할 수 있는 컬렉션
    // 이를 통해 여러 클라이언트가 동시에 접속하고 메시지를 주고받는 상황에서도 데이터의 일관성을 유지
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    // Func<Task<List<DeviceSummary>>>를 통해 장비 목록을 비동기적으로 가져오는 메서드를 외부에서 주입받음
    private readonly Func<Task<List<DeviceSummary>>> _getDevicesAsync;

    // 이벤트를 통해 로그 메시지를 외부에 전달 (LogListBox에 로그를 표시하기 위해 사용)
    public event Action<string>? LogReceived;
    // 이벤트를 통해 장비 등록 시 외부에 전달 (장비 등록 시 UI 업데이트를 위해 사용)
    public event Action<NetworkMessage>? DeviceRegistered;
    // 이벤트를 통해 서버로부터 수신된 메시지를 외부에 전달 (UI 업데이트를 위해 사용)
    public event Action<NetworkMessage>? MessageReceived;
    // 이벤트를 통해 장비 연결 해제 시 외부에 전달 (장비 연결 해제 시 UI 업데이트를 위해 사용)
    public event Action<string>? DeviceDisconnected;

    // TcpServerService 생성자에서 장비 목록을 가져오는 메서드를 주입받음
    public TcpServerService(
    Func<Task<List<DeviceSummary>>> getDevicesAsync)
    {
        _getDevicesAsync = getDevicesAsync;
    }

    // 서버를 시작하고 지정된 포트에서 클라이언트 연결을 수락하는 비동기 메서드
    public Task StartAsync(int port)
    {
        _cts = new CancellationTokenSource();

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        LogReceived?.Invoke(
            $"서버가 {port} 포트에서 실행되었습니다.");

        _ = AcceptLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    // 클라이언트 연결을 수락하고 각 클라이언트에 대한 메시지 처리를 수행하는 비동기 루프 메서드
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client =
                    await _listener.AcceptTcpClientAsync(token);

                LogReceived?.Invoke(
                    $"클라이언트 접속: {client.Client.RemoteEndPoint}");

                // 각 클라이언트에 대한 메시지 처리를 비동기로 수행하도록 HandleClientAsync 메서드를 호출
                _ = HandleClientAsync(client, token);
            }
        }
        catch (OperationCanceledException)
        {
            // 서버 종료 시 정상적으로 발생
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"서버 오류: {ex.Message}");
        }
    }

    // 클라이언트와의 통신을 처리하는 비동기 메서드
    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken token)
    {
        using (client)
        {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream);
            using StreamWriter writer = new(stream)
            {
                AutoFlush = true
            };

            string? registeredDeviceId = null;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? json = await reader.ReadLineAsync(token);

                    if (json is null)
                    {
                        break;
                    }

                    // 수신한 JSON 문자열을 NetworkMessage 객체로 역직렬화
                    NetworkMessage? message =
                        JsonSerializer.Deserialize<NetworkMessage>(json);

                    if (message is null)
                    {
                        continue;
                    }

                    // 장비 목록 요청 메시지를 수신하면 장비 목록을 가져와 응답 메시지를 전송
                    if (message.Type == MessageType.DeviceListRequest)
                    {
                        List<DeviceSummary> devices =
                            await _getDevicesAsync();

                        // 이미 등록된 장비는 제외하고 응답 메시지에 포함
                        devices = devices.Where(device =>!_clients.ContainsKey(device.DeviceId)).ToList();

                        NetworkMessage response = new()
                        {
                            Type = MessageType.DeviceListResponse,
                            Devices = devices,
                            SentAt = DateTime.Now
                        };

                        await writer.WriteLineAsync(
                            JsonSerializer.Serialize(response));

                        LogReceived?.Invoke("TX DeviceListResponse");

                        continue;
                    }

                    LogReceived?.Invoke(
                        $"RX {message.DeviceId} {message.Type}");

                    MessageReceived?.Invoke(message);

                    if (message.Type == MessageType.Register)
                    {
                        ClientConnection connection = new(
                            client,
                            writer);

                        bool added = _clients.TryAdd(
                            message.DeviceId,
                            connection);

                        if (!added)
                        {
                            NetworkMessage rejectedResponse = new()
                            {
                                Type = MessageType.RegisterRejected,
                                DeviceId = message.DeviceId,
                                DeviceName = message.DeviceName,
                                Status = "이미 다른 클라이언트에서 사용 중인 장비입니다.",
                                SentAt = DateTime.Now
                            };

                            await writer.WriteLineAsync(
                                JsonSerializer.Serialize(rejectedResponse));

                            LogReceived?.Invoke(
                                $"Register 거절: {message.DeviceId}");

                            continue;
                        }

                        registeredDeviceId = message.DeviceId;

                        NetworkMessage acceptedResponse = new()
                        {
                            Type = MessageType.RegisterAccepted,
                            DeviceId = message.DeviceId,
                            DeviceName = message.DeviceName,
                            SentAt = DateTime.Now
                        };

                        await writer.WriteLineAsync(
                            JsonSerializer.Serialize(acceptedResponse));

                        DeviceRegistered?.Invoke(message);

                        LogReceived?.Invoke(
                            $"장비 등록 완료: {message.DeviceId}");
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

                        await writer.WriteLineAsync(
                            JsonSerializer.Serialize(response));

                        LogReceived?.Invoke(
                            $"TX {message.DeviceId} PingResponse");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 서버 종료 시 정상적으로 발생
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"통신 오류: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(registeredDeviceId))
                {
                    _clients.TryRemove(
                        registeredDeviceId,
                        out _);

                    DeviceDisconnected?.Invoke(
                        registeredDeviceId);
                }
            }
        }
    }

    // 지정된 장비 ID로 메시지를 전송하는 비동기 메서드
    public async Task SendAsync(
        string deviceId,
        NetworkMessage message)
    {
        if (!_clients.TryGetValue(
            deviceId,
            out ClientConnection? connection))
        {
            LogReceived?.Invoke(
                $"{deviceId} 장비가 연결되어 있지 않습니다.");

            return;
        }

        string json = JsonSerializer.Serialize(message);

        await connection.Writer.WriteLineAsync(json);

        LogReceived?.Invoke(
            $"TX {deviceId} {message.Type}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();

        foreach (ClientConnection connection in _clients.Values)
        {
            connection.Client.Dispose();
        }

        _clients.Clear();

        LogReceived?.Invoke("서버가 종료되었습니다.");
    }

    // ClientConnection 클래스는 각 클라이언트와의 연결 정보를 관리하는 내부 클래스
    private class ClientConnection
    {
        public TcpClient Client { get; }
        public StreamWriter Writer { get; }

        public ClientConnection(
            TcpClient client,
            StreamWriter writer)
        {
            Client = client;
            Writer = writer;
        }
    }
}
