namespace modern_auth_api.Models
{
    public class VerifyTokenModel
    {
        public long UserId { get; set; }
        public DateTime ExpireTime { get; set; }
        public DateTime CreateTime { get; set; }
        public bool IsUsed { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool Active { get; set; }
        public DateTime UserUpdateTime { get; set; }
        public DateTime UserTokenUpdateTime { get; set; }
        public long TokenId { get; set; }
    }
}
