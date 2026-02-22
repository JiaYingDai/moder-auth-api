using modern_auth_api.Enum;

namespace modern_auth_api.Dtos
{
    public class CheckTokenDto
    {
        public required string Token { get; set; }
        public required TokenTypeEnum Type { get; set; }
    }
}
