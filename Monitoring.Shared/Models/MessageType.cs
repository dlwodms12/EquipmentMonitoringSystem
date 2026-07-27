using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Shared.Models;

//메세지 타입을 정의
public enum MessageType
{
    Register, //처음 접속할 때 장비 정보를 서버에 넘기는 용도
    PingRequest,
    PingResponse,
    SystemInfoRequest,
    SystemInfoResponse
}
