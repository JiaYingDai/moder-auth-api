using Dapper;
using modern_auth_api.Entity;
using modern_auth_api.Interface;
using modern_auth_api.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Xml.Linq;

namespace modern_auth_api.Repository
{
    public class UsersTokenRepository : IUsersTokenRepository
    {
        private readonly PostgresContext _context;
        private readonly IDbConnection _db;
        public UsersTokenRepository(PostgresContext context)
        {
            _context = context;
            _db = context.Database.GetDbConnection();
        }

        /// <summary>
        /// Insert New Token
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task InsertTokenAsync(UsersToken entity)
        {
            await _context.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 尋找有無存在Token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<VerifyTokenModel?> SelectTokenAsync(string token, string type)
        {
            var sql = @"
                        SELECT 
                            u.id AS userId,
                            ut.id AS tokenId,
                            ut.expire_time, 
                            u.is_email_verified, 
                            u.active ,
                            u.update_time AS userUpdateTime
                        FROM users_token AS ut
                        INNER JOIN users AS u ON ut.users_id = u.id 
                        WHERE ut.token = @Token AND ut.type=@Type";

            return await _db.QueryFirstOrDefaultAsync<VerifyTokenModel>(sql, new { Token = token, Type = type });
        }

        /// <summary>
        /// 刪除已使用token
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task DeleteTokenAsync(long tokenId)
        {
            // delete uers_token
            await _context.UsersTokens
                .Where(ut => ut.Id == tokenId)
                .ExecuteDeleteAsync();
        }
    }
}
