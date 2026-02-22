using StackExchange.Redis;

namespace modern_auth_api.Models
{
    public class RedisModel
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public TimeSpan? ExpireTimeSpan { get; set; }
    }
}
