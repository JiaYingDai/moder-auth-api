using modern_auth_api.Enum;
using System.Runtime.CompilerServices;

namespace modern_auth_api.Extensions
{
    public static class TokenTypeExtensions
    {
        public static string ToRedisPrefix(this TokenTypeEnum type)
        {
            return type switch {
                TokenTypeEnum.register => "reg",
                TokenTypeEnum.refresh => "rf",
                TokenTypeEnum.forgotpwd => "fg",
                _ => "unknown"
            };
        }
    }
}
