using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Interface;
using modern_auth_api.Models;
using Microsoft.IdentityModel.Tokens;
using Supabase.Gotrue;
using System.Diagnostics.Eventing.Reader;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace modern_auth_api.Service
{
    public class UsersTokenService : IUsersTokenService
    {
        private readonly IUsersTokenRepository _tokenRepo;
        private readonly IUserRepository _userRepository;
        public IConfiguration _configuration;
        private PostgresContext _dbContext;
        private readonly ILogger<UsersTokenService> _logger;

        public UsersTokenService(IUsersTokenRepository tokenRepo, IConfiguration configuration, IUserRepository userRepository, PostgresContext postgresContext, ILogger<UsersTokenService> logger)
        {
            _tokenRepo = tokenRepo;
            _configuration = configuration;
            _userRepository = userRepository;
            _dbContext = postgresContext;
            _logger = logger;
        }

        /// <summary>
        /// 產生token並Insert進table
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>token</returns>
        public async Task<string> CreateTokenAsync(long userId, TokenTypeEnum type)
        {
            // 1. 產生token (GUID)
            string token = Guid.NewGuid().ToString("N");

            // 2. 決定過期時間
            int expireMins = type switch { 
                TokenTypeEnum.register => _configuration.GetValue<int>("TokenSetting:ExpireMins:Register"),
                TokenTypeEnum.forgotpwd => _configuration.GetValue<int>("TokenSetting:ExpireMins:ForgetPwd"),
                TokenTypeEnum.refresh => _configuration.GetValue<int>("TokenSetting:ExpireMins:Refresh"),
                _ => _configuration.GetValue<int>("TokenSetting:ExpireMins:Default") // 預設值
            };

            // 3. 組裝entity
            var userToken = new UsersToken()
            {
                Token = token,
                CreateTime = DateTime.UtcNow,
                ExpireTime = DateTime.UtcNow.AddMinutes(expireMins),
                UsersId = userId,
                Type = type.ToString(),
            };

            // 4. Insert
            await _tokenRepo.InsertTokenAsync(userToken);

            return token;
        }

        /// <summary>
        /// 產生JWT
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>tokenString</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public string CreateJWT(long userId)
        {
            // 1. 組裝使用者的身分識別(Claims)
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()), // User ID
                };

            // 2. 製作簽章鑰匙
            var securityKey = _configuration["SecurityKey"];
            // 2-1. 檢查是否存在
            if (string.IsNullOrEmpty(securityKey))
            {
                throw new InvalidOperationException("SecurityKey Not Found.");
            }

            // 2-2. 檢查長度 (HMACSHA256至少16字元)
            if (securityKey.Length < 16)
            {
                throw new InvalidOperationException("SecurityKey Length Error.");
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. 產生JWT Token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddMinutes(_configuration.GetValue<int>("TokenSetting:ExpireMins:LoginJWT")), // 過期時間
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return tokenString;
        }

        /// <summary>
        /// 檢查資料庫token是否存在/有效
        /// </summary>
        /// <param name="token"></param>
        /// <param name="type"></param>
        /// <returns>ServiceResult<VerifyTokenModel></returns>
        public async Task<ServiceResult<VerifyTokenModel>> CheckTokenAsync(string token, TokenTypeEnum type)
        {
            // 1. 資料庫查找token
            VerifyTokenModel? model = await _tokenRepo.SelectTokenAsync(token, type.ToString());

            // 2. 沒找到token，回傳false
            if (model == null)
            {
                return ServiceResult<VerifyTokenModel>.Fail(
                        ErrorCodeEnum.Auth_InvalidToken,
                        "無效的Token");
            }

            // 3. Email已驗證成功，回傳false (Type = register)
            if (model.IsEmailVerified && (type == TokenTypeEnum.register))
            {
                return ServiceResult<VerifyTokenModel>.Fail(
                        ErrorCodeEnum.Auth_AlreadyVerified, 
                        "此帳號已完成驗證，請直接登入");
            }

            // 5. token超過設定的到期時間，回傳false
            if (model.ExpireTime < DateTime.UtcNow)
            {
                return ServiceResult<VerifyTokenModel>.Fail(
                        ErrorCodeEnum.Auth_TokenExpired,
                        "Token已過期，請重新申請");
            }

            // 6. 全部通過，回傳Data (model)，把model交給下一個需要的人
            return ServiceResult<VerifyTokenModel>.Success(
                        model);
        }

        /// <summary>
        /// Update註冊驗證通過者的user狀態和token狀態
        /// </summary>
        /// <param name="model"></param>
        /// <returns>true / false</returns>
        public async Task UpdateRegisterTokenAsync(VerifyTokenModel model)
        {
            // 開啟交易 (update token + update users)
            using (var transaction = _dbContext.Database.BeginTransaction())
            {
                try {
                    // delete token
                    await DeleteTokenAsync(model.TokenId);

                    // update users的結果
                    bool active = true;
                    bool isEmailVerified = true;
                    DateTime userUpdateTime = DateTime.UtcNow;
                    bool isUserUpdate = await _userRepository.UpdateUserValidAsync(model.UserId, active, isEmailVerified, userUpdateTime);

                    if (!isUserUpdate) {
                        throw new Exception($"Register Transaction Failed. UserId: {model.UserId} 更新失敗");
                    }

                    // 都沒報錯，正式提交Commit
                    await transaction.CommitAsync();
                }
                catch (Exception ex) {
                    // 發生任何錯誤，全部還原 (Rollback)
                    await transaction.RollbackAsync();
                    throw new Exception($"Register Transaction Failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Delete註冊驗證通過者的token
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task DeleteTokenAsync(long tokenId)
        {
            // 1. delete users_token的結果
            await _tokenRepo.DeleteTokenAsync(tokenId);
        }

        /// <summary>
        /// 檢查RefreshToken是否有效，有效就給予新的JWT + Refresh Token
        /// </summary>
        /// <param name="token"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<ServiceResult<LoginResponseDto>> RefreshTokenAsync(string token)
        {
            // 1. 檢查RefreshToken是否有效
            var checkResult = await CheckTokenAsync(token, TokenTypeEnum.refresh);

            // 無效，回傳
            if (!checkResult.IsSuccess || checkResult.Data == null)
            {
                return ServiceResult<LoginResponseDto>.Fail(checkResult.ErrorCode, checkResult.Message);
            }

            // 2. 有效，刪除舊的Refresh Token
            await DeleteTokenAsync(checkResult.Data.TokenId);

            // 3. 建立新的JWT, Refresh Token
            var userId = checkResult.Data.UserId;
            // 3-1. JWT
            string tokenString = CreateJWT(userId);
            // 3-2. Refresh Token
            string refreshToken = await CreateTokenAsync(userId, TokenTypeEnum.refresh);

            return ServiceResult<LoginResponseDto>.Success(
                new LoginResponseDto
                {
                    Token = tokenString,
                    RefreshToken = refreshToken
                });
        }
    }
}
