using Monitoring.Shared.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Monitoring.Server.Services;

public class TcpServerService
{
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public event Action<string>? LogReceived;
    public event Action<NetworkMessage>? DeviceRegistered;
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<string>? DeviceDisconnected;

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

                    NetworkMessage? message =
                        JsonSerializer.Deserialize<NetworkMessage>(json);

                    if (message is null)
                    {
                        continue;
                    }

                    LogReceived?.Invoke(
                        $"RX {message.DeviceId} {message.Type}");

                    MessageReceived?.Invoke(message);

                    if (message.Type == MessageType.Register)
                    {
                        registeredDeviceId = message.DeviceId;

                        ClientConnection connection = new(
                            client,
                            writer);

                        _clients[message.DeviceId] = connection;

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
