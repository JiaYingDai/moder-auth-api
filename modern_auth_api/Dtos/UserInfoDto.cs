namespace modern_auth_api.Dtos
{
    public class UserInfoDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Provider { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; }

        public DateTime? UpdateTime { get; set; }

        public string? Picture { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}
