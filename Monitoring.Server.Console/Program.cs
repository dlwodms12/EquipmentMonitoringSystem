using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Monitoring.Shared.Models;

const int port = 5000;

// var : 변수 선언 시 타입을 컴파일러가 추론
// TcpListener : TCP 연결을 수신하는 클래스
// IPAddress.Loopback : 로컬 호스트(127.0.0.1)를 나타내는 IP 주소
// 추후 다른 PC가 접속하게 하려면 아래와 같이 변경
// IPAddress.Any : 모든 네트워크 인터페이스에서 수신 대기
var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();

Console.WriteLine($"서버가 {port} 포트에서 접속을 기다립니다.");

while (true)
{
    // AcceptTcpClientAsync : 비동기적으로 클라이언트 연결을 수락하는 메서드
    // await : 비동기 작업이 완료될 때까지 기다림
    // 클라이언트가 접속할 때까지 이 지점에서 대기하며, 접속이 이루어지면 TcpClient 객체를 반환
    TcpClient client = await listener.AcceptTcpClientAsync();

    Console.WriteLine(
        $"클라이언트 접속: {client.Client.RemoteEndPoint}");

    // HandleClientAsync(client) : 클라이언트와의 통신을 처리하는 비동기 메서드 호출
    // _ = HandleClientAsync(client) : 반환값(Task)을 무시하고, 비동기 작업을 백그라운드에서 실행하도록 함
    _ = HandleClientAsync(client);
}

// 연결된 클라이언트와의 통신을 처리하는 비동기 메서드
static async Task HandleClientAsync(TcpClient client)
{
    // using : 메서드가 끝나거나 오류가 날 때 연결과 스트림을 자동으로 정리(TCP 연결은 사용 후 닫아야 함)
    using (client)
    {
        // stream : TCP 연결 통로
        using NetworkStream stream = client.GetStream();
        // StreamReader : 스트림에서 텍스트를 읽는 클래스
        using StreamReader reader = new(stream);
        // StreamWriter : 스트림에 텍스트를 쓰는 클래스, AutoFlush = true : 버퍼를 자동으로 비움 = 즉시 전송
        using StreamWriter writer = new(stream) { AutoFlush = true };

        try
        {
            while (true)
            {
                // ReadLineAsync : 스트림에서 한 줄을 비동기적으로 읽음, 클라이언트가 메시지를 보낼 때까지 대기
                // 클라이언트는 JSON 메세지 하나를 보내고 줄바꿈 \n을 붙임.
                // string? : null 허용 문자열, 클라이언트가 연결을 끊으면 null 반환
                string? json = await reader.ReadLineAsync();

                // 연결이 끊긴 경우 연결 자원을 정리
                if (json is null)
                {
                    Console.WriteLine("클라이언트 연결이 종료되었습니다.");
                    break;
                }

                // JsonSerializer.Deserialize : JSON 문자열을 객체로 변환
                NetworkMessage? message =
                    JsonSerializer.Deserialize<NetworkMessage>(json);

                //변환에 실패했거나 비어있는 데이터가 들어온 경우 무시하고 다음 루프 진행
                if (message is null)
                {
                    continue;
                }

                // 받은 데이터의 타입과 장비 ID를 콘솔에 출력
                // RX는 Receive(수신)의 약자, TX는 Transmit(송신)의 약자
                Console.WriteLine(
                    $"RX | {message.Type} | {message.DeviceId}");

                // 클라이언트가 처음 접속했을 때 장비 등록 메시지를 보내면 서버에서 장비 등록 완료 메시지를 출력
                if (message.Type == MessageType.Register)
                {
                    Console.WriteLine(
                        $"장비 등록 완료: {message.DeviceId} / {message.DeviceName}");
                }

                // 받은 메시지 타입이 PingRequest이면 PingResponse 메시지를 만들어 클라이언트로 전송
                if (message.Type == MessageType.PingRequest)
                {
                    // 서버의 응답을 담은 NetworkMessage 객체 생성, 클라이언트가 보낸 메시지의 DeviceId, DeviceName, RequestId를 그대로 사용
                    NetworkMessage response = new()
                    {
                        Type = MessageType.PingResponse,
                        DeviceId = message.DeviceId,
                        DeviceName = message.DeviceName,
                        RequestId = message.RequestId,
                        SentAt = DateTime.Now
                    };

                    // JsonSerializer.Serialize : 객체를 JSON 문자열로 변환
                    string responseJson =
                        JsonSerializer.Serialize(response);

                    // WriteLineAsync : 스트림에 한 줄을 비동기적으로 쓰고, 줄바꿈(\n)을 자동으로 추가, 클라이언트로 전송
                    await writer.WriteLineAsync(responseJson);

                    // 서버가 보낸 메시지를 콘솔에 출력
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
