using Google.Apis.Auth;
using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Models;

namespace modern_auth_api.Interface
{
    public interface IUserService
    {
        Task<LoginResponseDto> ValifyGoogleUserAsync(GoogleJsonWebSignature.Payload payload);
        Task<ServiceResult> RegisterUserAsync(RegisterDto dto);
        Task<ServiceResult<LoginResponseDto>> LoginCheckAsync(LoginDto dto);
        Task<ServiceResult> LogoutDeleteTokenAsync(string refreshToken);
        Task<ServiceResult> RegisterVerifyAsync(VerifyTokenDto dto);
        Task<ServiceResult> RequestVerificationMailAsync(RequestVerificationMailDto dto);
        Task<UserInfoDto?> GetUserInfoAsync(long userId, string baseUrl);
        Task<ServiceResult<string>> EditUserAsync(UpdateUserDto dto, string baseUrl);
        Task<bool> RemoveAvatarAsync(long userId);
        Task<ServiceResult> ResetPwdAsync(ResetPwdDto dto);
    }
}
