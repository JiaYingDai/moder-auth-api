using modern_auth_api.Dtos;
using modern_auth_api.Entity;

namespace modern_auth_api.Interface
{
    public interface IUserRepository
    {
        Task<long> AddAsync(User entity);
        Task<User?> SelectUserByEmailAsync(string email, string provider);
        Task<User?> SelectUserByIdAsync(long id);
        Task<int> UpdateAsync(long id, string? name, string? picture, DateTime updateTime);
        Task<int> UpdatePicNullAsync(long id, DateTime updateTime);
        Task<bool> UpdatePwdAsync(long id, string passwordHash);
        Task<bool> UpdateUserValidAsync(long userId, bool active, bool isEmailVerified, DateTime userUpdateTime);
        Task DeleteUserAsync(long userId);
    }
}
