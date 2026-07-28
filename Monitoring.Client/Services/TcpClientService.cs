using Monitoring.Shared.Models;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace Monitoring.Client.Services;

public class TcpClientService
{
    // 서버와의 TCP 연결을 관리하는 TcpClient 인스턴스
    private TcpClient? _client;
    // 서버로부터 데이터를 읽어오는 StreamReader 인스턴스
    private StreamReader? _reader;
    // 서버로 데이터를 보내는 StreamWriter 인스턴스
    private StreamWriter? _writer;
    // 연결 상태를 관리하고 비동기 작업을 취소할 수 있는 CancellationTokenSource 인스턴스
    private CancellationTokenSource? _cts;
    // 서버로 데이터를 보내는 작업을 동기화하기 위한 SemaphoreSlim 인스턴스 (한 번에 하나의 쓰기 작업만 허용)
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // 이벤트를 통해 로그 메시지를 외부에 전달
    // LogListBox에 로그를 표시하기 위해 사용
    public event Action<string>? LogReceived;
    // 이벤트를 통해 서버로부터 수신된 메시지를 외부에 전달
    public event Action<NetworkMessage>? MessageReceived;
    // 이벤트를 통해 연결 상태 변경을 외부에 전달 (true: 연결됨, false: 연결 끊김)
    public event Action<bool>? ConnectionChanged;

    // 클라이언트 창이 열린 뒤 호출되는 메서드
    public async Task ConnectAsync(
        string serverIp,
        int serverPort,
        string deviceId,
        string deviceName)
    {
        // 통신 작업을 취소할 수 있는 객체 생성
        _cts = new CancellationTokenSource();

        try
        {
            // TCP Client 객체 생성
            _client = new TcpClient();

            // 로그 이벤트를 통해 서버 연결 시도 메시지 전달
            LogReceived?.Invoke("서버 연결을 시도합니다.");

            // TCP 서버에 연결 시도 (비동기)
            await _client.ConnectAsync(
                serverIp,
                serverPort,
                _cts.Token);

            //연결이 성공하면 NetworkStream을 가져와 StreamReader와 StreamWriter를 초기화
            NetworkStream stream = _client.GetStream();

            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };

            ConnectionChanged?.Invoke(true);
            LogReceived?.Invoke("서버에 연결되었습니다.");

            // 서버에 연결되면 장치 등록 메시지를 전송
            NetworkMessage registerMessage = new()
            {
                Type = MessageType.Register,
                DeviceId = deviceId,
                DeviceName = deviceName,
                SentAt = DateTime.Now
            };

            await SendAsync(registerMessage);

            // 서버로부터 메시지를 수신하는 루프와 핑 메시지를 주기적으로 보내는 루프를 시작
            // _ = 는 반환된 Task를 무시하고, 비동기 작업을 백그라운드에서 실행하도록 함
            _ = ReceiveLoopAsync(_cts.Token);
            _ = PingLoopAsync(deviceId, deviceName, _cts.Token);
        }
        catch (Exception ex)
        {
            ConnectionChanged?.Invoke(false);
            LogReceived?.Invoke($"연결 실패: {ex.Message}");
        }
    }

    // 서버로부터 메시지를 수신하는 루프를 비동기적으로 실행하는 메서드
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
                //서버로부터 한 줄씩 데이터를 읽어옴 (비동기)
                string? json = await _reader.ReadLineAsync(token);

                // 서버가 연결을 종료하면 json이 null이 되므로, 연결 종료 시 로그를 남기고 이벤트를 발생시킴
                if (json is null)
                {
                    LogReceived?.Invoke("서버 연결이 종료되었습니다.");
                    ConnectionChanged?.Invoke(false);
                    break;
                }

                // 읽어온 JSON 문자열을 NetworkMessage 객체로 역직렬화
                NetworkMessage? message =
                    JsonSerializer.Deserialize<NetworkMessage>(json);

                if (message is null)
                {
                    continue;
                }

                // 서버로부터 수신된 메시지 타입을 로그로 남기고, MessageReceived 이벤트를 발생시켜 외부에서 처리할 수 있도록 함
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

    // 서버로 주기적으로 핑 메시지를 보내는 루프를 비동기적으로 실행하는 메서드
    private async Task PingLoopAsync(
        string deviceId,
        string deviceName,
        CancellationToken token)
    {
        try
        {
            // IsCancellationRequested 속성을 통해 취소 요청이 들어오면 루프를 종료하도록 함
            while (!token.IsCancellationRequested)
            {
                // 개발 중에는 5초, 완성 시에는 1분으로 변경
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                // Ping 메시지를 생성하여 서버로 전송
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
    // 서버로 메시지를 전송하는 메서드, 동시에 여러 쓰기 작업이 발생하지 않도록 SemaphoreSlim을 사용하여 동기화
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
