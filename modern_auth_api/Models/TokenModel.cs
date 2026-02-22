using modern_auth_api.Enum;

namespace modern_auth_api.Models
{
    public class TokenModel
    {
        public string Token { get; set; } = string.Empty;
        public TokenTypeEnum Type { get; set; }
    }
}
