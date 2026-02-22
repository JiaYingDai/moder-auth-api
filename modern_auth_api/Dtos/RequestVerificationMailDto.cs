using modern_auth_api.Enum;

namespace modern_auth_api.Dtos
{
    public class RequestVerificationMailDto
    {
        public required string Email { get; set; }
        public required string CallBackUrl { get; set; }
        public required TokenTypeEnum Type { get; set; }
    }
}
