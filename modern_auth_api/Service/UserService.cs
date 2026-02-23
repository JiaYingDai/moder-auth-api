using Google.Apis.Auth;
using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Interface;
using modern_auth_api.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Supabase.Gotrue;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Dapper.SqlMapper;
using User = modern_auth_api.Entity.User;
using ISupabaseClient = Supabase.Interfaces.ISupabaseClient<
    Supabase.Gotrue.User,
    Supabase.Gotrue.Session,
    Supabase.Realtime.RealtimeSocket,
    Supabase.Realtime.RealtimeChannel,
    Supabase.Storage.Bucket,
    Supabase.Storage.FileObject
>;

namespace modern_auth_api.Service
{
    public class UserService : IUserService
    {
        public IConfiguration _configuration;
        private readonly IUserRepository _repo;
        private readonly IHashService _hashService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IMailService _mailService;
        private readonly IUsersTokenService _usersTokenService;
        private PostgresContext _dbContext;
        private readonly ILogger<UserService> _logger;
        private ISupabaseClient _client;


        public UserService(IConfiguration configuration, IUserRepository repo, IHashService hashService, IFileStorageService fileStorageService, [FromKeyedServices("producer")]IMailService mailService, IUsersTokenService usersTokenService, PostgresContext dbContext, ILogger<UserService> logger, ISupabaseClient client)
        {
            _configuration = configuration;
            _repo = repo;
            _hashService = hashService;
            _fileStorageService = fileStorageService;
            _mailService = mailService;
            _usersTokenService = usersTokenService;
            _dbContext = dbContext;
            _logger = logger;
            _client = client;
        }

        /// <summary>
        /// 驗證GOOGLE登入
        /// </summary>
        /// <param name="dto"></param>
        /// <returns>LoginResponseDto</returns>
        public async Task<LoginResponseDto> ValifyGoogleUserAsync(GoogleJsonWebSignature.Payload payload)
        {
            // 1. 驗證成功，存DB
            // 1-1. 檢查User Table有無存在User
            User? oldUserEntity = await _repo.SelectUserByEmailAsync(payload.Email, ProviderEnum.Google.ToString());
            
            // 1-2. 組新會員entity
            User newUserEntity = new User()
            {
                AuthId = Guid.NewGuid().ToString(),
                Name = payload.Name,
                Email = payload.Email,
                Picture = payload.Picture,
                Provider = ProviderEnum.Google.ToString(),
                ProviderKey = payload.Subject,
                Role = RoleEnum.user.ToString(),
                CreateTime = DateTime.UtcNow,
                Active = true
            };
            // 1-3. DB不存在user->insert進DB再取id / 已存在user->直接取id
            var userId = oldUserEntity == null ? await _repo.AddAsync(newUserEntity) : oldUserEntity.Id;

            // 2. 驗證成功，組裝JWT, Refresh Token
            // 2-1. JWT
            string tokenString = _usersTokenService.CreateJWT(userId);
            // 2-2. Refresh Token
            string refreshToken = await _usersTokenService.CreateTokenAsync(userId, TokenTypeEnum.refresh);

            return new LoginResponseDto
                {
                    Token = tokenString,
                    RefreshToken = refreshToken
                };
        }

        /// <summary>
        /// 註冊使用者
        /// </summary>
        /// <param name="registerDto"></param>
        /// <returns>userId</returns>
        public async Task<ServiceResult> RegisterUserAsync(RegisterDto registerDto)
        {
            // 1-1. 檢查User Table有無存在User
            User? oldUserEntity = await _repo.SelectUserByEmailAsync(registerDto.Email, ProviderEnum.Local.ToString());

            // 1-2. 存在，回傳null(表示不是新建的id)
            if (oldUserEntity != null)
            {
                return ServiceResult.Fail(ErrorCodeEnum.User_EmailAlreadyExists, "已註冊的Email");
            }

            // 2. 不存在，處理DB資料準備insert
            // 2-1. 雜湊註冊密碼
            string passwordHash = _hashService.PasswordHashSalt(registerDto.Password);
            // 步驟 1: 先在 Supabase Auth 建立使用者
            var session = await _client.Auth.SignUp(registerDto.Email, passwordHash);

            if (session?.User == null)
            {
                return ServiceResult.Fail(ErrorCodeEnum.SystemError, "找不到使用者");
            }

            // 取得authId
            string authId = session.User.Id ?? string.Empty;

            // 2-2. 處理上傳圖片，存進指定路徑與生成DB資料
            String? picturePath = null;
            if (registerDto.Picture != null)
            {
                string folderName = _configuration["File.UploadFolder"] ?? "upload";
                picturePath = await _fileStorageService.SaveFileAsync(registerDto.Picture, folderName, authId);
            }

            // 2-3. 組裝存進DB資料
            User newUserEntity = new User()
            {
                AuthId = authId,
                Name = registerDto.Name,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                Picture = picturePath,
                Provider = ProviderEnum.Local.ToString(),
                ProviderKey = Guid.NewGuid().ToString(),
                Role = RoleEnum.user.ToString(),
                CreateTime = DateTime.UtcNow
            };

            // 2-4. insert users table
            long userId = await _repo.AddAsync(newUserEntity);

            // 3. 準備寄送驗證信
            try
            {
                await SendConfirmMailAsync(userId, registerDto.CallBackUrl, registerDto.Name, registerDto.Email, registerDto.Type);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"寄送驗證信失敗。Email: {registerDto.Email}, UserId: {userId}，準備回滾資料");

                // 因寄信失敗，須把剛剛新增的使用者刪除
                await _repo.DeleteUserAsync(userId);

                // 拋回異常
                return ServiceResult.Fail(ErrorCodeEnum.System_EmailSendFailed, "寄送註冊驗證信失敗");
            }
        }

        /// <summary>
        /// 註冊驗證
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<ServiceResult> RegisterVerifyAsync(VerifyTokenDto dto){
            // 1. 檢查token是否存在 / 有效
            var checkResult = await _usersTokenService.CheckTokenAsync(dto.Token, TokenTypeEnum.register);

            // 2. 驗證失敗，直接回傳，不要update
            if (!checkResult.IsSuccess || checkResult.Data == null)
            {
                return ServiceResult<VerifyTokenModel>.Fail(checkResult.ErrorCode, checkResult.Message);
            }

            // 3. 執行交易update
            try
            {
                await _usersTokenService.UpdateRegisterTokenAsync(checkResult.Data);
                // 更新成功
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                var userId = checkResult.Data.UserId;
                var tokenId = checkResult.Data.TokenId;
                _logger.LogError(ex, "註冊驗證流程失敗，UserId: {UserId}, TokenId: {TokenId}", userId, tokenId);
                return ServiceResult<VerifyTokenModel>.Fail(ErrorCodeEnum.SystemError, "系統忙碌中，請稍後再試");
            }
        }

        /// <summary>
        /// 重寄驗證信
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<ServiceResult> RequestVerificationMailAsync(RequestVerificationMailDto dto)
        {
            // 1. 檢查email有無存在db
            User? entity = await _repo.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString());

            // 2. 不存在，回傳錯誤
            if (entity == null)
            {
                return ServiceResult.Fail(ErrorCodeEnum.User_NotFound, "找不到使用者");
            }

            // 3. 存在但已驗證(for註冊用)，回傳錯誤
            if (entity.IsEmailVerified && dto.Type == TokenTypeEnum.register)
            {
                return ServiceResult.Fail(ErrorCodeEnum.Auth_AlreadyVerified, "此帳號已完成驗證，請直接登入");
            }

            // 3. 存在，準備寄送驗證信
            try
            {
                await SendConfirmMailAsync(entity.Id, dto.CallBackUrl, entity.Name, entity.Email, dto.Type);
            }
            catch (Exception ex) {
                _logger.LogError(ex, $"寄送驗證信失敗。Email: {dto.Email}, UserId: {entity.Id}");

                // 拋出異常
                return ServiceResult.Fail(ErrorCodeEnum.System_EmailSendFailed, "寄送註冊驗證信失敗，請稍後再試或聯繫客服");
            }

            return ServiceResult.Success();
        }

        /// <summary>
        /// 驗證使用者登入帳密
        /// </summary>
        /// <param name="dto"></param>
        /// <returns>tokenString</returns>
        public async Task<ServiceResult<LoginResponseDto>> LoginCheckAsync(LoginDto dto)
        {
            // 1. 驗證帳密
            // 1-1. 獲取資料庫該使用者密碼雜湊
            User? entity = await _repo.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString());

            // 1-2. 不存在使用者 || 不存在使用者密碼(防呆)
            if (entity == null || entity.PasswordHash == null)
            {
                return ServiceResult<LoginResponseDto>.Fail(ErrorCodeEnum.Auth_LoginFailed, "帳號或密碼錯誤");
            }

            // 1-3. 驗證使用者輸入密碼 vs 資料庫密碼
            string orgPasswordHash = entity.PasswordHash;

            // 1-4. 驗證失敗
            if (!_hashService.Verify(dto.Password, orgPasswordHash))
            {
                return ServiceResult<LoginResponseDto>.Fail(ErrorCodeEnum.Auth_LoginFailed, "帳號或密碼錯誤");
            }

            // 1-5. Email尚未驗證
            if (!entity.IsEmailVerified)
            {
                return ServiceResult<LoginResponseDto>.Fail(ErrorCodeEnum.User_EmailNotVerified, "帳號存在但未驗證Email");
            }

            // 1-6. User被鎖
            if (!entity.Active)
            {
                return ServiceResult<LoginResponseDto>.Fail(ErrorCodeEnum.User_AccountDisabled, "帳號被停權");
            }

            // 2. 驗證成功，組裝JWT, Refresh Token
            // 2-1. JWT
            string tokenString = _usersTokenService.CreateJWT(entity.Id);
            // 2-2. Refresh Token
            string refreshToken = await _usersTokenService.CreateTokenAsync(entity.Id, TokenTypeEnum.refresh);

            return ServiceResult<LoginResponseDto>.Success(
                new LoginResponseDto { 
                    Token = tokenString, 
                    RefreshToken = refreshToken });
        }

        /// <summary>
        /// 登出，刪除資料庫RefreshToken
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns>ServiceResult</returns>
        public async Task<ServiceResult> LogoutDeleteTokenAsync(string refreshToken)
        {
            // 1. 檢查token是否存在 / 有效
            var checkResult = await _usersTokenService.CheckTokenAsync(refreshToken, TokenTypeEnum.refresh);

            // 2. token檢查失敗，直接回傳，不刪除refreshToken
            if (!checkResult.IsSuccess || checkResult.Data == null)
            {
                return ServiceResult.Fail(checkResult.ErrorCode, checkResult.Message);
            }

            // 3. 存在token，刪除users_token資料庫RefreshToken
            await _usersTokenService.DeleteTokenAsync(checkResult.Data.TokenId);

            return ServiceResult.Success();
        }

        /// <summary>
        /// 取得使用者資訊
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="baseUrl"></param>
        /// <returns>UserInfoDto</returns>
        public async Task<UserInfoDto?> GetUserInfoAsync(long userId, string baseUrl)
        {
            // 1. 資料庫取得使用者資訊
            User? entity = await _repo.SelectUserByIdAsync(userId);

            // 2. 不存在使用者
            if (entity == null) {
                return null;
            }

            // 3. 組裝回傳資料
            // 3-1. 處理回傳圖片
            string? pictureUrl = null;

            if (!string.IsNullOrEmpty(entity.Picture))
            {
                // C#內建的Uri類別來判斷這是不是絕對網址
                // UriKind.Absolute檢查 http://, https://, ftp://等所有標準協定
                bool isExternalUrl = Uri.TryCreate(entity.Picture, UriKind.Absolute, out Uri? result)
                                     && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);

                if (isExternalUrl) // 絕對網址: 原封不動
                {
                    pictureUrl = entity.Picture;
                }
                else
                {
                    // 本地上傳: 補上BaseUrl
                    pictureUrl = $"{baseUrl.TrimEnd('/')}/{entity.Picture.TrimStart('/')}";
                }
            }

            UserInfoDto dto = new UserInfoDto()
            {
                Id = userId,
                Name = entity.Name,
                Email = entity.Email,
                Provider = entity.Provider,
                CreateTime = entity.CreateTime,
                UpdateTime = entity.UpdateTime,
                Picture = pictureUrl,
                Role = entity.Role,
            };
            
            return dto;
        }

        /// <summary>
        /// 更新使用者資訊
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<ServiceResult<string>> EditUserAsync(UpdateUserDto dto, string baseUrl)
        {
            // 1. 獲得User AuthId
            User? entity = await _repo.SelectUserByIdAsync(dto.Id);
            // 2. 不存在使用者
            if (entity == null)
            {
                return ServiceResult<string>.Fail(ErrorCodeEnum.User_NotFound, "找不到使用者");
            }

            // 3. 處理上傳圖片，存進指定路徑與生成DB資料
            String? picturePath = null;
            String? pictureUrl = null;
            if (dto.Picture != null)
            {
                string folderName = _configuration["File.UploadFolder"] ?? "upload";
                picturePath = await _fileStorageService.SaveFileAsync(dto.Picture, folderName, entity.AuthId);

                // C#內建的Uri類別來判斷這是不是絕對網址
                // UriKind.Absolute檢查 http://, https://, ftp://等所有標準協定
                bool isExternalUrl = Uri.TryCreate(picturePath, UriKind.Absolute, out Uri? result)
                                     && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);

                if (isExternalUrl) // 絕對網址: 原封不動
                {
                    pictureUrl = picturePath;
                }
                else
                {
                    // 本地上傳: 補上BaseUrl
                    pictureUrl = $"{baseUrl.TrimEnd('/')}/{picturePath.TrimStart('/')}";
                }
            }

            // 4. 準備update資料庫
            DateTime updateTime = DateTime.UtcNow;
            int affectedRows = await _repo.UpdateAsync(dto.Id, dto.Name, picturePath, updateTime);

            if (affectedRows == 0) { 
                return ServiceResult<string>.Fail(ErrorCodeEnum.ResourceNotFound, "找不到該使用者，或已被刪除");
            }
            return ServiceResult<string>.Success(pictureUrl);
        }

        /// <summary>
        /// 重設密碼
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult> ResetPwdAsync(ResetPwdDto dto)
        {
            // 1. 檢查token是否存在 / 有效
            var checkResult = await _usersTokenService.CheckTokenAsync(dto.Token, TokenTypeEnum.forgotpwd);

            // 2. token檢查失敗，直接回傳，不要update
            if (!checkResult.IsSuccess || checkResult.Data == null)
            {
                return ServiceResult.Fail(checkResult.ErrorCode, checkResult.Message);
            }

            // 2. 有效，準備update token && password (transaction)
            // 2-1. 資料庫取得使用者資訊
            User? entity = await _repo.SelectUserByIdAsync(checkResult.Data.UserId);

            // 2-2. 不存在使用者 || 不存在使用者密碼(防呆)
            if (entity == null || entity.PasswordHash == null)
            {
                return ServiceResult.Fail(ErrorCodeEnum.User_NotFound, "找不到使用者");
            }

            // 2-3. 驗證使用者輸入密碼 vs 資料庫密碼
            string orgPasswordHash = entity.PasswordHash;

            // 2-3. 比對新密碼與舊密碼是否相符，若相符則回傳顯示不可與舊密碼相同
            // 相同，回傳
            if (_hashService.Verify(dto.Password, orgPasswordHash))
            {
                return ServiceResult.Fail(ErrorCodeEnum.Auth_PasswordReuse, "新密碼不能與舊密碼相同");
            }

            // 2-4. 雜湊密碼
            string passwordHash = _hashService.PasswordHashSalt(dto.Password);

            // 2-5. update token && password (transaction)
            using (var transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    // delete token
                    await _usersTokenService.DeleteTokenAsync(checkResult.Data.TokenId);

                    // update users的結果
                    bool isUserUpdate = await _repo.UpdatePwdAsync(checkResult.Data.UserId, passwordHash);

                    if (!isUserUpdate)
                    {
                        throw new Exception($"Register Transaction Failed. UserId: {checkResult.Data.UserId} 更新密碼失敗");
                    }

                    // 都沒報錯，正式提交Commit
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    // 發生任何錯誤，全部還原 (Rollback)
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            // 4. 成功更新
            return ServiceResult.Success();
        }

        /// <summary>
        /// 移除大頭貼
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>true / false</returns>
        public async Task<bool> RemoveAvatarAsync(long userId)
        {
            // 1. 準備update資料庫
            DateTime updateTime = DateTime.UtcNow;
            int affectedRows = await _repo.UpdatePicNullAsync(userId, updateTime);

            return affectedRows > 0;
        }

        // 寄送驗證信
        private async Task SendConfirmMailAsync(long userId, string callbackUrl, string name, string email, TokenTypeEnum tokenType)
        {
            // 1. 產生註冊驗證信的link
            string confirmLink = await GenerateConfirmLink(userId, callbackUrl, tokenType);

            // 2. 發送註冊驗證信
            // 2-1. Mail Server設定
            MailServerSetting mailServerSetting = new MailServerSetting()
            {
                SmtpHost = _configuration.GetValue<string>("MailServerSetting:SmtpHost") ?? "",
                SmtpPort = _configuration.GetValue<int>("MailServerSetting:SmtpPort", 0),
                UseSsl = _configuration.GetValue<bool>("MailServerSetting:UseSsl", true)
            };

            // 2-2. 組裝信件模板、主旨
            var htmlFileName = "";  // 信件模板
            var subject = "";
            switch (tokenType)
            {
                case TokenTypeEnum.register:
                    htmlFileName = "RegisterConfirmEmail.html";
                    subject = _configuration.GetValue<string>("MailSetting:Register:Subject") ?? "會員註冊驗證信";
                    break;
                case TokenTypeEnum.forgotpwd:
                    htmlFileName = "ForgotPwdEmail.html";
                    subject = _configuration.GetValue<string>("MailSetting:ForgotPwd:Subject") ?? "帳號安全通知：密碼重設";
                    break;
            }
            
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", htmlFileName);
            var templateContent = await File.ReadAllTextAsync(templatePath);
            var finalBody = templateContent
                            .Replace("{UserName}", name)
                            .Replace("{Link}", confirmLink);

            // 2-3. 組裝信件資訊
            MailSetting mailSetting = new MailSetting()
            {
                FromEmail = _configuration.GetValue<string>("MailServerSetting:FromEmail") ?? "",
                Password = _configuration.GetValue<string>("MailServerSetting:Password") ?? "",
                ToEmail = email,
                Subject = subject,
                Body = new Body()
                {
                    Html = finalBody
                }
            };

            // 3. 發送信件
            await _mailService.SendMail(new SendMailModel {
                MailServerSetting = mailServerSetting,
                MailSetting = mailSetting
            });
        }

        // 產生驗證信連結
        private async Task<string> GenerateConfirmLink(long userId, string frontendUrl, TokenTypeEnum tokenType)
        {
            // 1. 獲得token
            string token = await _usersTokenService.CreateTokenAsync(userId, tokenType);

            // 2. 產生連結
            string confirmLink = string.Empty;
            // 2-1. 註冊連結
            if (tokenType == TokenTypeEnum.register)
            {
                confirmLink = $"{frontendUrl}/verify-register?token={token}";
            }
            // 2-2. 忘記密碼連結
            else if (tokenType == TokenTypeEnum.forgotpwd)
            {
                confirmLink = $"{frontendUrl}/reset-pwd?token={token}";
            }
            else
            {
                throw new InvalidOperationException("不存在的token Type");
            }

            return confirmLink;
        }
    }
}
