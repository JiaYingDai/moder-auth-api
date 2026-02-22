using modern_auth_api.Enum;

namespace modern_auth_api.Dtos
{
    public class RegisterDto
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public IFormFile? Picture { get; set; }
        public required string CallBackUrl { get; set; }
        public required TokenTypeEnum Type { get; set; }
    }
}
