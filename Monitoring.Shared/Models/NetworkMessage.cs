using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Shared.Models;

//각 장비(클라이언트)에서 서버로 전송되는 메시지의 구조를 정의하는 클래스
public class NetworkMessage
{
    public MessageType Type { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.Now;

    public string Status { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public double Voltage { get; set; }

    public int Battery { get; set; }

    // 장비 선택을 위한 목록 속성 추가
    public List<DeviceSummary> Devices { get; set; } = new();
}
