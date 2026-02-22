using modern_auth_api.Enum;

namespace modern_auth_api.Dtos
{
    public class VerifyTokenDto
    {
        public required string Token { get; set; }
        public TokenTypeEnum Type { get; set; }
    }
}
