namespace modern_auth_api.Dtos
{
    public class GoogleLoginDto
    {
        /// <summary>
        /// Google傳來的token
        /// </summary>
        public required string Credential { get; set; }
    }
}
