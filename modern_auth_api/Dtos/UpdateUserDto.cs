namespace modern_auth_api.Dtos
{
    public class UpdateUserDto
    {
        public required long Id { get; set; }
        public string? Name { get; set; }
        public IFormFile? Picture { get; set; }
    }
}
