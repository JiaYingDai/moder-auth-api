using modern_auth_api.Entity;
using modern_auth_api.Models;

namespace modern_auth_api.Interface
{
    public interface IUsersTokenRepository
    {
        Task InsertTokenAsync(UsersToken entity);
        Task<VerifyTokenModel?> SelectTokenAsync(string token, string type);
        Task DeleteTokenAsync(long tokenId);
    }
}
