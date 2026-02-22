using modern_auth_api.Enum;

namespace modern_auth_api.Dtos
{
    public class ResetPwdResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public ErrorCodeEnum ErrorCode { get; set; }
    }
}
