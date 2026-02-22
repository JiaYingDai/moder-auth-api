using Google.Apis.Auth;
using Google.Apis.Http;
using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Interface;
using modern_auth_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Supabase.Gotrue;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace modern_auth_api.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IHashService _hashService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IUsersTokenService _usersTokenService;
        private readonly ILogger<AccountController> _logger;


        public AccountController(IConfiguration configuration, IUserService userService, IHashService hashService, IFileStorageService fileStorageService, IUsersTokenService usersTokenService, ILogger<AccountController> logger)
        {
            _configuration = configuration;
            _userService = userService;
            _hashService = hashService;
            _fileStorageService = fileStorageService;
            _usersTokenService = usersTokenService;
            _logger = logger;
        }

        /// <summary>
        /// 資料庫紀錄Google登入使用者
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("validateGoogle")]
        public async Task<IActionResult> ValidateGoogle([FromBody] GoogleLoginDto dto)
        {
            string email = "unknown";
            string googleSub = "unknown";

            try
            {
                // 1. 解析使用者
                var payload = await GoogleJsonWebSignature.ValidateAsync(dto.Credential);
                email = payload.Email;
                googleSub = payload.Subject;

                // 2. 驗證google token並儲存資料庫
                LoginResponseDto responseDto = await _userService.ValifyGoogleUserAsync(payload);

                // 3-1. 攜帶Http-Only的Refresh Token
                SetRefreshTokenInCookie(responseDto.RefreshToken);

                // 3-2. 回傳token在Body
                return Ok(responseDto.Token);
            }
            // 驗證失敗
            catch (InvalidJwtException ex)
            {
                string maskedToken = "null";
                if (!string.IsNullOrEmpty(dto.Credential) && dto.Credential.Length > 20)
                {
                    maskedToken = $"{dto.Credential.Substring(0, 10)}...{dto.Credential.Substring(dto.Credential.Length - 5)}";
                }
                _logger.LogError(ex, "Google Token 驗證失敗，Token: {MaskedToken}", maskedToken);
                return Unauthorized("Invalid Google Token");
            }
            // 其餘錯誤
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google登入發生錯誤，Email: {Email}, GoogleSub: {GoogleSub}", email, googleSub);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 註冊會員 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterDto dto)
        {
            try
            {
                // 處理註冊
                var result = await _userService.RegisterUserAsync(dto);

                if (!result.IsSuccess)
                {
                    switch (result.ErrorCode)
                    {
                        case ErrorCodeEnum.SystemError:   // 系統錯誤
                        case ErrorCodeEnum.System_EmailSendFailed:   // 註冊驗證信發送失敗
                            return StatusCode(500, result);
                        case ErrorCodeEnum.User_EmailAlreadyExists:   // 此Email已註冊
                            return Conflict(result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 註冊成功
                return Ok();
            }
            // 其餘錯誤
            catch (Exception ex) {
                _logger.LogError(ex, "註冊發生錯誤，Email: {Eamil}", dto.Email);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 註冊驗證token API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("registerVerify")]
        public async Task<IActionResult> RegisterVerify([FromBody] VerifyTokenDto dto)
        {
            try {
                // 驗證token是否有效
                var result = await _userService.RegisterVerifyAsync(dto);

                // 驗證不成功，回badrequest
                if (!result.IsSuccess) {
                    switch (result.ErrorCode)
                    {
                        case ErrorCodeEnum.Auth_TokenExpired:   // token已過期
                        case ErrorCodeEnum.Auth_InvalidToken:   // Token 無效
                            return StatusCode(401, result);
                        case ErrorCodeEnum.Auth_AlreadyVerified:   // 此帳號已完成驗證
                            return Conflict(result);
                        case ErrorCodeEnum.SystemError:   // 註冊驗證流程失敗
                            return StatusCode(500, result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 驗證成功
                return Ok(result);
            }
            catch (Exception ex) {
                string maskedToken = "null";
                if (!string.IsNullOrEmpty(dto.Token) && dto.Token.Length > 20)
                {
                    maskedToken = $"{dto.Token.Substring(0, 10)}...{dto.Token.Substring(dto.Token.Length - 5)}";
                }
                _logger.LogError(ex, "註冊驗證發生錯誤，Token: {MaskedToken}", maskedToken);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
         }

        /// <summary>
        /// 檢查token是否存在/有效 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("checkToken")]
        public async Task<IActionResult> CheckToken(CheckTokenDto dto)
        {
            try
            {
                // 驗證token是否存在
                var result = await _usersTokenService.CheckTokenAsync(dto.Token, dto.Type);

                // 驗證不成功，回badrequest
                if (!result.IsSuccess) {
                    switch (result.ErrorCode)
                    {
                        case ErrorCodeEnum.Auth_TokenExpired:   // token已過期
                        case ErrorCodeEnum.Auth_InvalidToken:   // Token 無效
                            return StatusCode(401, result);
                        case ErrorCodeEnum.Auth_AlreadyVerified:   // 此帳號已完成驗證
                            return Conflict(result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 驗證成功
                return Ok(result);
            }
            catch (Exception ex)
            {
                string maskedToken = "null";
                if (!string.IsNullOrEmpty(dto.Token) && dto.Token.Length > 20)
                {
                    maskedToken = $"{dto.Token.Substring(0, 10)}...{dto.Token.Substring(dto.Token.Length - 5)}";
                }
                _logger.LogError(ex, "token驗證發生錯誤，Token: {MaskedToken}", maskedToken);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 請求發送驗證信 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("requestVerificationMail")]
        public async Task<IActionResult> RequestVerificationMail(RequestVerificationMailDto dto)
        {
            try
            {
                // 1. 驗證email是否存在/已驗證，並重新傳送驗證信
                ServiceResult result = await _userService.RequestVerificationMailAsync(dto);

                // 2. 驗證沒過
                if (!result.IsSuccess)
                {
                    switch (result.ErrorCode)
                    {
                        case ErrorCodeEnum.User_NotFound:
                            return StatusCode(404, result);
                        case ErrorCodeEnum.Auth_AlreadyVerified:   // 此帳號已完成驗證
                            return Conflict(result);
                        case ErrorCodeEnum.System_EmailSendFailed:   // 寄送註冊驗證信失敗
                            return StatusCode(500, result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 3. 驗證通過，回傳結果
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送驗證信發生錯誤，Email: {Email}", dto.Email);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 登入 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginDto dto)
        {
            try {
                // 1. 驗證帳密，取得token
                ServiceResult<LoginResponseDto> result = await _userService.LoginCheckAsync(dto);

                // 2. 驗證沒過
                if (!result.IsSuccess || result.Data == null)
                {
                    switch (result.ErrorCode)
                    {
                        // 403: 帳號狀態問題 (未驗證、被停權)
                        case ErrorCodeEnum.User_AccountDisabled:    // 帳號被停權
                        case ErrorCodeEnum.User_EmailNotVerified:    // 帳號存在但未驗證Email
                            return StatusCode(403, result);

                        case ErrorCodeEnum.Auth_LoginFailed:    // 帳號或密碼錯誤
                            return StatusCode(401, result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 3-1. 驗證通過，攜帶Http-Only的Refresh Token
                SetRefreshTokenInCookie(result.Data.RefreshToken);

                // 3-2. 驗證通過，回傳token在Body
                return Ok(result.Data.Token);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "一般登入發生錯誤，Email: {Email}", dto.Email);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 前端登出，刪除Cookie + 資料庫refreshToken
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // 0. 從Cookie取得Refresh Token
            var refreshToken = Request.Cookies["refresh_token"];

            try
            {
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    // 1. 驗證token，存在則刪除，不存在該refreshToken，則不需動作
                    ServiceResult result = await _userService.LogoutDeleteTokenAsync(refreshToken);
                }

                // 2. 命令瀏覽器刪除Cookie
                DeleteRefreshTokenInCookie();

                // 3. 都回傳OK
                return Ok();
            }
            catch (Exception ex)
            {
                string maskedToken = "null";
                if (!string.IsNullOrEmpty(refreshToken) && refreshToken.Length > 20)
                {
                    maskedToken = $"{refreshToken.Substring(0, 10)}...{refreshToken.Substring(refreshToken.Length - 5)}";
                }
                _logger.LogError(ex, "Logout 刪除Refresh Token發生錯誤，Token: {MaskedToken}", maskedToken);
                // 3. 都回傳OK
                return Ok();
            }
        }


        /// <summary>
        /// 驗證Refresh Token，有效則換發新Token + RefreshToken
        /// </summary>
        /// <param></param>
        /// <returns></returns>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            // 0. 從Cookie取得Refresh Token
            var refreshToken = Request.Cookies["refresh_token"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("RefreshToken Not Found.");
            }

            try
            {
                // 1. 驗證帳密，換發token
                ServiceResult<LoginResponseDto> result = await _usersTokenService.RefreshTokenAsync(refreshToken);

                // 2. 驗證沒過
                if (!result.IsSuccess || result.Data == null)
                {
                    // 2-1. 丟掉失效的refreshToken
                    Response.Cookies.Delete("refresh_token");

                    switch (result.ErrorCode)
                    {
                        // 401: token已過期
                        case ErrorCodeEnum.Auth_TokenExpired:
                            return StatusCode(401, result);
                        case ErrorCodeEnum.Auth_InvalidToken:   // Token 無效
                            return StatusCode(401, result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 3. 驗證通過，攜帶Http-Only的Refresh Token
                SetRefreshTokenInCookie(result.Data.RefreshToken);

                return Ok(result.Data.Token);
            }
            catch (Exception ex)
            {
                string maskedToken = "null";
                if (!string.IsNullOrEmpty(refreshToken) && refreshToken.Length > 20)
                {
                    maskedToken = $"{refreshToken.Substring(0, 10)}...{refreshToken.Substring(refreshToken.Length - 5)}";
                }
                _logger.LogError(ex, "Refresh Token發生錯誤，Token: {MaskedToken}", maskedToken);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 重設密碼 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("resetPwd")]
        public async Task<IActionResult> ResetPwd([FromForm] ResetPwdDto dto)
        {
            try
            {
                // 驗證token是否有效，再update password
                var result = await _userService.ResetPwdAsync(dto);

                // 驗證不成功，回badrequest
                if (!result.IsSuccess) {
                    switch (result.ErrorCode)
                    {
                        case ErrorCodeEnum.Auth_TokenExpired:   // token已過期
                        case ErrorCodeEnum.Auth_InvalidToken:   // Token 無效
                            return StatusCode(401, result);
                        // 404: 找不到使用者
                        case ErrorCodeEnum.User_NotFound:
                            return StatusCode(404, result);
                        default:
                            return BadRequest(result);
                    }
                }

                // 驗證成功
                return Ok(result);
            }
            catch (Exception ex) {
                string maskedToken = "null";
                if (!string.IsNullOrEmpty(dto.Token) && dto.Token.Length > 20)
                {
                    maskedToken = $"{dto.Token.Substring(0, 10)}...{dto.Token.Substring(dto.Token.Length - 5)}";
                }
                _logger.LogError(ex, "重設密碼發生錯誤，Token: {MaskedToken}", maskedToken);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 取得使用者資訊 API
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpPost("getUserInfo")]
        public async Task<IActionResult> GetUserInfo()
        {
            // 進到此方法，代表token已被中介驗證通過
            // 1. 取得使用者資訊
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest("Invalid Token");
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}"; // 處理圖片回傳需要
            try
            {
                UserInfoDto? userInfoDto = await _userService.GetUserInfoAsync(Int32.Parse(userId), baseUrl);

                // 2. 不存在使用者
                if (userInfoDto == null)
                {
                    return NotFound(new { message = "使用者不存在" });
                }

                // 3. 存在使用者，回傳使用者資料
                return Ok(userInfoDto);
            }
            catch (InvalidJwtException ex) {
                _logger.LogError(ex, "取得使用者資訊Token驗證失敗，UserId: {UserId}", userId);
                return Unauthorized("Invalid Token");
            }
            // 其餘錯誤
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得使用者資訊發生錯誤，UserId: {UserId}", userId);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 異動使用者資訊 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("updateUser")]
        public async Task<IActionResult> UpdateUser([FromForm] UpdateUserDto dto)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}"; // 處理圖片回傳需要
                var result = await _userService.EditUserAsync(dto, baseUrl);

                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }

                // 更新成功
                return Ok(new { Picture = result.Data });
            }
            // 其餘錯誤
            catch (Exception ex)
            {
                _logger.LogError(ex, "異動使用者資訊發生錯誤，UserId: {UserId}", dto.Id);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        /// <summary>
        /// 異動使用者大頭貼 API
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("removeAvatar")]
        public async Task<IActionResult> RemoveAvatar([FromBody] UpdatePictureNullDto dto)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}"; // 處理圖片回傳需要
                bool IsSuccess = await _userService.RemoveAvatarAsync(dto.Id);

                if (!IsSuccess)
                {
                    return NotFound("找不到該使用者");
                }

                // 更新成功
                return Ok();
            }
            // 其餘錯誤
            catch (Exception ex)
            {
                _logger.LogError(ex, "異動使用者大頭貼發生錯誤，UserId: {UserId}", dto.Id);
                return StatusCode(500, "Internal Error: " + ex.Message);
            }
        }

        private void SetRefreshTokenInCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_configuration.GetValue<int>("TokenSetting:ExpireMins:Refresh")),
                Secure = _configuration.GetValue<bool>("TokenSetting:HttpOnlyCookieSecure"),
                SameSite = SameSiteMode.None // 跨網域: None
            };

            Response.Cookies.Append("refresh_token", refreshToken, cookieOptions);
        }

        private void DeleteRefreshTokenInCookie()
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = _configuration.GetValue<bool>("TokenSetting:HttpOnlyCookieSecure"),
                SameSite = SameSiteMode.None // 跨網域: None
            };

            Response.Cookies.Delete("refresh_token", cookieOptions);
        }
    }
}
