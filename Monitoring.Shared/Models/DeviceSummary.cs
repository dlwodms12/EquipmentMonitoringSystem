namespace Monitoring.Shared.Models;

// 장비 선택을 위한 간단한 장비 정보 클래스, DeviceRecord와 달리 장비 상태 정보는 포함하지 않음
public class DeviceSummary
{
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DisplayText =>
        $"{DeviceId} - {DeviceName}";
}
