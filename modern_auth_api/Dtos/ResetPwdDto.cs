namespace modern_auth_api.Dtos
{
    public class ResetPwdDto
    {
        public required string Token { get; set; }
        public required string Password { get; set; }
    }
}
