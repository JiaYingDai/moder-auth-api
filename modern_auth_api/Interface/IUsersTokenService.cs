using modern_auth_api.Dtos;
using modern_auth_api.Enum;
using modern_auth_api.Models;

namespace modern_auth_api.Interface
{
    public interface IUsersTokenService
    {
        Task<string> CreateTokenAsync(long userId, TokenTypeEnum type);
        string CreateJWT(long userId);
        Task<ServiceResult<VerifyTokenModel>> CheckTokenAsync(string token, TokenTypeEnum type);
        Task UpdateRegisterTokenAsync(VerifyTokenModel model);
        Task DeleteTokenAsync(long tokenId);
        Task<ServiceResult<LoginResponseDto>> RefreshTokenAsync(string token);
    }
}
