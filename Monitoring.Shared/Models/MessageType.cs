using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Shared.Models;

//메세지 타입을 정의
public enum MessageType
{
    // 처음 접속할 때 장비 정보를 서버에 넘기는 용도
    Register, 
    PingRequest,
    PingResponse,
    SystemInfoRequest,
    SystemInfoResponse,
    // 장비 리스트 요청 및 응답 타입 추가
    DeviceListRequest,
    DeviceListResponse,
    // 장비 등록 승인 및 거부 타입 추가
    RegisterAccepted,
    RegisterRejected
}
