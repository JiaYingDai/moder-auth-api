using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Extensions;
using modern_auth_api.Interface;
using modern_auth_api.Models;
using modern_auth_api.Service;
using System;
using System.Text.Json;
using static Dapper.SqlMapper;

namespace modern_auth_api.Repository
{
    public class RedisUsersTokenRepository : IUsersTokenRepository
    {
        private readonly Redis _redis;
        public RedisUsersTokenRepository(Redis redis)
        {
            _redis = redis;
        }

        public async Task DeleteTokenAsync(long tokenId)
        {
            string indexKey = $"idx:token_id:{tokenId}";
            var value = await _redis.GetValue(indexKey);

            if (value == null) return;

            // 解析value
            var valObj = JsonSerializer.Deserialize<TokenModel>(value);
            if (valObj == null) return;
            string token = valObj.Token;
            TokenTypeEnum type = valObj.Type;

            string key = $"{type.ToRedisPrefix()}:{token}";

            // 刪除兩筆Redis (key=key, key=indexKey)
            await _redis.DeleteValue(key);
            await _redis.DeleteValue(indexKey);
        }

        public async Task InsertTokenAsync(UsersToken entity)
        {
            string key = "unknown";
            if (System.Enum.TryParse<TokenTypeEnum>(entity.Type, true, out var typeEnum))
            {
                key = $"{typeEnum.ToRedisPrefix()}:{entity.Token}"; // key:token e.g. "rt:abc12345"
            }

            long tokenId = await _redis.GetIncrementTokenId();
            // 建立索引
            string indexKey = $"idx:token_id:{tokenId}";

            var tokenData = new VerifyTokenModel{ 
                TokenId = tokenId,
                UserId = entity.UsersId,
                CreateTime = entity.CreateTime,
                ExpireTime = entity.ExpireTime
            };
            string value = JsonSerializer.Serialize(tokenData);

            // 建立主資料
            await _redis.SetValue(new RedisModel
            {
                Key = key,
                Value = value,
                ExpireTimeSpan = (entity.ExpireTime - DateTime.UtcNow).Add(TimeSpan.FromMinutes(5)) // 比Cookie再多5分鐘緩衝時間
            });

            // 索引資料(用id找token)
            await _redis.SetValue(new RedisModel
            {
                Key = indexKey,
                Value = JsonSerializer.Serialize(new TokenModel{ 
                    Token = entity.Token,
                    Type = typeEnum,
                }),
                ExpireTimeSpan = (entity.ExpireTime - DateTime.UtcNow).Add(TimeSpan.FromMinutes(5)) // 比Cookie再多5分鐘緩衝時間
            });
        }

        public async Task<VerifyTokenModel?> SelectTokenAsync(string token, string type)
        {
            string key = "unknown";
            if (System.Enum.TryParse<TokenTypeEnum>(type, true, out var typeEnum))
            {
                key = $"{typeEnum.ToRedisPrefix()}:{token}";
            }

            string? value = await _redis.GetValue(key);

            if (value == null) {
                return null;
            }

            return JsonSerializer.Deserialize<VerifyTokenModel?>(value);
        }
    }
}
