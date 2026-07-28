namespace Monitoring.Server.Models;
// DB의 Devices 테이블 한 행을 C# 객체로 표현하는 클래스, 장비의 상태 정보를 담고 있음
public class DeviceRecord
{
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string Status { get; set; } = "정보 없음";

    public double Temperature { get; set; }

    public double Voltage { get; set; }

    public int Battery { get; set; }

    public bool IsConnected { get; set; }

    public DateTime? LastSeen { get; set; }
}
