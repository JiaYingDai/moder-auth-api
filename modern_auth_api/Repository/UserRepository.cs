using Dapper;
using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Interface;
using modern_auth_api.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace modern_auth_api.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly PostgresContext _context;
        private readonly IDbConnection _db;
        public UserRepository(PostgresContext context)
        {
            _context = context;
            _db = context.Database.GetDbConnection();
        }

        /// <summary>
        /// Isert使用者資料庫
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task<long> AddAsync(User entity)
        {
            await _context.Users.AddAsync(entity);
            await _context.SaveChangesAsync();

            return entity.Id;
        }

        /// <summary>
        /// 獲取資料庫該使用者資料 (By Email, Provider)
        /// </summary>
        /// <param name="email"></param>
        /// <param name="provider"></param>
        /// <returns>password_hash</returns>
        public async Task<User?> SelectUserByEmailAsync(string email, string provider)
        {
            // string sql = "SELECT name, passwordHash, picture, role FROM users WHERE email = @Email AND provider = @Provider";
            string sql = "SELECT id, name, email, provider, create_time, update_time, provider_key, picture, role, password_hash, active, is_email_verified FROM users WHERE email = @Email AND provider = @Provider";
            return await _db.QueryFirstOrDefaultAsync<User?>(sql, new { Email = email, Provider = provider });
        }

        /// <summary>
        /// 獲取資料庫該使用者資料 (By UserId)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<User?> SelectUserByIdAsync(long id)
        {
            string sql = "SELECT id, auth_id, name, email, provider, password_hash, create_time, update_time, picture, role FROM users WHERE id = @Id";
            return await _db.QueryFirstOrDefaultAsync<User?>(sql, new { Id = id });
        }

        /// <summary>
        /// 編輯使用者資訊
        /// </summary>
        /// /// <param name="userId"></param>
        /// <param name="name"></param>
        /// <param name="picture"></param>
        /// <returns></returns>
        public async Task<int> UpdateAsync(long id, string? name, string? picture, DateTime updateTime)
        {
            return await _context.Users
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.Name, u => name ?? u.Name)  // 如果name是null，就用原本資料庫裡的u.Name
                    .SetProperty(u => u.Picture, u => picture ?? u.Picture)// 如果picture是null，就用原本資料庫裡的u.Picture
                    .SetProperty(u => u.UpdateTime, updateTime)); 
        }

        /// <summary>
        /// 將picture移除 (update為null)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updateTime"></param>
        /// <returns>afftedRows</returns>
        public async Task<int> UpdatePicNullAsync(long id, DateTime updateTime)
        {
            return await _context.Users
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.Picture, u => null)
                .SetProperty(u => u.UpdateTime, updateTime));
        }

        /// <summary>
        /// 更新密碼
        /// </summary>
        /// <param name="id"></param>
        /// <param name="password"></param>
        /// <returns>afftedRows</returns>
        public async Task<bool> UpdatePwdAsync(long id, string passwordHash)
        {
            int affectedRows = await _context.Users
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.PasswordHash, passwordHash));
            return affectedRows > 0;
        }

        /// <summary>
        /// 更新User狀態，IsEmailVerified和IsActice = true
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<bool> UpdateUserValidAsync(long userId, bool active, bool isEmailVerified, DateTime userUpdateTime)
        {
            // update users
            int affectedUserRows = await _context.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.Active, active)
                    .SetProperty(u => u.IsEmailVerified, isEmailVerified)
                    .SetProperty(u => u.UpdateTime, userUpdateTime)
                    );

            // 成功更新才回傳true
            return affectedUserRows > 0;
        }

        public async Task DeleteUserAsync(long userId)
        {
            // delete users
            await _context.Users
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync();
        }
    }
}
