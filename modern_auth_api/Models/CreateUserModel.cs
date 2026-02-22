using modern_auth_api.Enum;

namespace modern_auth_api.Models
{
    public class CreateUserModel
    {
        public required string Name { get; set; }

        public required string Email { get; set; }

        public string? PasswordHash { get; set; }

        public required ProviderEnum Provider { get; set; }
        public required string ProviderKey { get; set; }
        public string? Picture { get; set; }
        public required RoleEnum Role { get; set; }
    }
}
