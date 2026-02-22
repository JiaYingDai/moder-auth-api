using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using modern_auth_api.Controllers;
using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Interface;
using Moq;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static Google.Apis.Requests.RequestError;

namespace modern_auth_api.Tests
{
    [TestClass]
    public class AccountControllerTests
    {
        // 1. 定義所有需要的 Mock 物件
        private Mock<IConfiguration> _mockConfig;
        private Mock<IUserService> _mockUserService;
        private Mock<IHashService> _mockHashService;
        private Mock<IFileStorageService> _mockFileStorageService;
        private Mock<IUsersTokenService> _mockTokenService;
        private Mock<ILogger<AccountController>> _mockLogger;

        private AccountController _controller;

        // 2. 初始化 (每個測試執行前都會跑這段)
        [TestInitialize]
        public void Setup()
        {
            _mockConfig = new Mock<IConfiguration>();
            _mockUserService = new Mock<IUserService>();
            _mockHashService = new Mock<IHashService>();
            _mockFileStorageService = new Mock<IFileStorageService>();
            _mockTokenService = new Mock<IUsersTokenService>();
            _mockLogger = new Mock<ILogger<AccountController>>();

            // 實例化 Controller，注入所有的 Mock
            _controller = new AccountController(
                _mockConfig.Object,
                _mockUserService.Object,
                _mockHashService.Object,
                _mockFileStorageService.Object,
                _mockTokenService.Object,
                _mockLogger.Object
            );
        }

        #region Register 測試

        [TestMethod]
        public async Task Register_NewUser_ReturnsOk()
        {
            // Arrange
            var dto = new RegisterDto { 
                Name = "name",
                Email = "new@test.com",
                Password = "password",
                CallBackUrl = "test.com",
                Type = TokenTypeEnum.register
            };

            // 模擬 Service 回傳OK (代表註冊成功)
            var successResult = ServiceResult.Success();
            _mockUserService.Setup(s => s.RegisterUserAsync(dto))
                            .ReturnsAsync(successResult);

            // Act
            var result = await _controller.Register(dto);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        [TestMethod]
        public async Task Register_ExistingEmail_ReturnsConflict()
        {
            // Arrange
            var dto = new RegisterDto
            {
                Name = "name",
                Email = "new@test.com",
                Password = "password",
                CallBackUrl = "test.com",
                Type = TokenTypeEnum.register
            };

            // 模擬 Service 回傳 Fail (已存在的Email)
            var successResult = ServiceResult.Fail(ErrorCodeEnum.User_EmailAlreadyExists, "已註冊的Email");
            _mockUserService.Setup(s => s.RegisterUserAsync(dto))
                            .ReturnsAsync(successResult);

            // Act
            var result = await _controller.Register(dto);

            // Assert
            var conflictResult = result as ConflictObjectResult;
            Assert.IsNotNull(conflictResult);
            Assert.AreEqual(409, conflictResult.StatusCode);
        }

        #endregion

        #region Login 測試

        [TestMethod]
        public async Task Login_Success_ReturnsOk()
        {
            // Arrange
            // 準備假的設定檔資料(模擬 appsettings.json)
            var myAppSettings = new Dictionary<string, string>
            {
                {"TokenSetting:ExpireMins:Refresh", "10080"}, // 模擬 7 天
                {"TokenSetting:HttpOnlyCookieSecure", "true"}  // 模擬 true
            };

            // 建立Configuration物件
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(myAppSettings)
                .Build();
            var dto = new LoginDto { Email = "test@test.com", Password = "pwd" };
            var responseDto = new LoginResponseDto { Token = "jwt_token", RefreshToken = "refresh_token" };
            var successResult = ServiceResult<LoginResponseDto>.Success(responseDto);

            _mockUserService.Setup(s => s.LoginCheckAsync(dto))
                            .ReturnsAsync(successResult);

            _controller._configuration = configuration;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.Login(dto);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(responseDto.Token, okResult.Value);
        }

        [TestMethod]
        public async Task Login_EmailNotVerified_Returns403()
        {
            // Arrange
            var dto = new LoginDto { Email = "test@test.com", Password = "password" };
            var failResult = ServiceResult<LoginResponseDto>.Fail(ErrorCodeEnum.User_EmailNotVerified, "EmailNotVerified");

            _mockUserService.Setup(s => s.LoginCheckAsync(dto))
                            .ReturnsAsync(failResult);

            // Act
            var result = await _controller.Login(dto);

            // Assert
            var objectResult = result as ObjectResult; // 403 是 ObjectResult 不是 StatusCodeResult
            Assert.IsNotNull(objectResult);
            Assert.AreEqual(403, objectResult.StatusCode);
        }

        [TestMethod]
        public async Task Login_WrongPassword_Returns401()
        {
            // Arrange
            var dto = new LoginDto { Email = "test@test.com", Password = "password" };
            var failResult = ServiceResult<LoginResponseDto>.Fail(ErrorCodeEnum.Auth_LoginFailed, "WrongPassword");

            _mockUserService.Setup(s => s.LoginCheckAsync(dto))
                            .ReturnsAsync(failResult);

            // Act
            var result = await _controller.Login(dto);

            // Assert
            var objectResult = result as ObjectResult;
            Assert.IsNotNull(objectResult);
            Assert.AreEqual(401, objectResult.StatusCode);
        }

        #endregion

        #region GetUserInfo 測試 (含 Token/Claims 模擬)

        [TestMethod]
        public async Task GetUserInfo_ValidToken_ReturnsUserInfo()
        {
            // Arrange
            int userId = 10;
            // 模擬 HttpContext 和 User Claims (這是最關鍵的一步！)
            SetMockUser(userId.ToString());

            // 模擬 Request.Scheme 和 Request.Host (因為 Controller 有用到)
            _controller.ControllerContext.HttpContext.Request.Scheme = "http";
            _controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");

            var expectedInfo = new UserInfoDto { Name = "Test User" };

            // 注意：Controller 傳進去的是 baseUrl，我們用 It.IsAny<string>() 來忽略字串比對細節
            _mockUserService.Setup(s => s.GetUserInfoAsync(userId, It.IsAny<string>()))
                            .ReturnsAsync(expectedInfo);

            // Act
            var result = await _controller.GetUserInfo();

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(expectedInfo, okResult.Value);
        }

        [TestMethod]
        public async Task GetUserInfo_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            int userId = 99;
            SetMockUser(userId.ToString());

            // 模擬 Request (避免 NullReferenceException)
            _controller.ControllerContext.HttpContext.Request.Scheme = "http";
            _controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");

            // Service 回傳 null
            _mockUserService.Setup(s => s.GetUserInfoAsync(userId, It.IsAny<string>()))
                            .ReturnsAsync((UserInfoDto)null);

            // Act
            var result = await _controller.GetUserInfo();

            // Assert
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult);
            Assert.AreEqual(404, notFoundResult.StatusCode);
        }

        #endregion

        // --- Helper Method ---
        // 用來模擬登入後的 User Claims
        private void SetMockUser(string userId)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            // 建立 Mock HttpContext
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);

            // 模擬 Request 物件 (讓 Request.Scheme/Host 不會噴錯)
            var mockRequest = new Mock<HttpRequest>();
            mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);

            // 塞進 Controller Context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = mockHttpContext.Object
            };
        }
    }
}
