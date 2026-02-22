namespace modern_auth_api.Interface
{
    public interface IHashService
    {
        string PasswordHashSalt(string password);
        bool Verify(string password, string passwordHash);
    }
}
