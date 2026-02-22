using FluentAssertions;
using Google.Apis.Auth;
using modern_auth_api.Dtos;
using modern_auth_api.Entity;
using modern_auth_api.Enum;
using modern_auth_api.Interface;
using modern_auth_api.Repository;
using modern_auth_api.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ISupabaseClient = Supabase.Interfaces.ISupabaseClient<
    Supabase.Gotrue.User,
    Supabase.Gotrue.Session,
    Supabase.Realtime.RealtimeSocket,
    Supabase.Realtime.RealtimeChannel,
    Supabase.Storage.Bucket,
    Supabase.Storage.FileObject
>;

namespace modern_auth_api.Tests;

[TestClass]
public class UserServiceTests
{
    public Mock<IConfiguration> _mockConfiguration;
    private Mock<IUserRepository> _mockRepo;
    private Mock<IHashService> _mockHashService;
    private Mock<IFileStorageService> _mockFileStorageService;
    private Mock<IMailService> _mockMailService;
    private Mock<IUsersTokenService> _mockUsersTokenService;
    private Mock<PostgresContext> _mockDbContext;
    private Mock<ILogger<UserService>> _mockLogger;
    private Mock<ISupabaseClient> _mockClient;

    private UserService _service;

    [TestInitialize]
    public void Setup()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockRepo = new Mock<IUserRepository>();
        _mockHashService = new Mock<IHashService>();
        _mockFileStorageService = new Mock<IFileStorageService>();
        _mockMailService = new Mock<IMailService>();
        _mockUsersTokenService = new Mock<IUsersTokenService>();
        _mockDbContext = new Mock<PostgresContext>();
        _mockLogger = new Mock<ILogger<UserService>>();
        _mockClient = new Mock<ISupabaseClient>();

        _service = new UserService(
            _mockConfiguration.Object, 
            _mockRepo.Object, 
            _mockHashService.Object, 
            _mockFileStorageService.Object,
            _mockMailService.Object,
            _mockUsersTokenService.Object,
            _mockDbContext.Object,
            _mockLogger.Object,
            _mockClient.Object);
    }

    [TestMethod]
    public async Task ValifyGoogleUserAsync_Success_ReturnLoginResponse()
    {
        // Arrange
        var payload = new GoogleJsonWebSignature.Payload
        {
            Email = "test@test.com",
            Name = "Test",
            Picture = "TestPicture",
            Subject = "Test"
        };

        User? oldUserEntity = new User()
        {
            Id = 1,
            AuthId = "authId",
            Name = "Test",
            Email = "test@test.com",
            Picture = "TestPicture",
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = "Test",
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            Active = true
        };

        _mockRepo.Setup(r => r.SelectUserByEmailAsync(payload.Email, ProviderEnum.Google.ToString()))
                                        .ReturnsAsync(oldUserEntity);

        User newUserEntity = new User()
        {
            AuthId = "authId",
            Name = payload.Name,
            Email = payload.Email,
            Picture = payload.Picture,
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = payload.Subject,
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            Active = true
        };

        var userId = 1;
        _mockRepo.Setup(r => r.AddAsync(newUserEntity))
                .ReturnsAsync(userId);

        string tokenString = "tokenString";
        _mockUsersTokenService.Setup(s => s.CreateJWT(userId))
                               .Returns(tokenString);

        string refreshToken = "refreshToken";
        _mockUsersTokenService.Setup(s => s.CreateTokenAsync(userId, TokenTypeEnum.refresh))
                               .ReturnsAsync(refreshToken);

        LoginResponseDto expectedResult = new LoginResponseDto
        {
            Token = tokenString,
            RefreshToken = refreshToken
        };

        // Act
        LoginResponseDto result = await _service.ValifyGoogleUserAsync(payload);

        // Assert
        Assert.IsNotNull(result);
        //Assert.AreEqual(result.Token, expectedResult.Token);
        //Assert.AreEqual(result.RefreshToken, expectedResult.RefreshToken);
        result.Should().BeEquivalentTo(expectedResult); // 使用FluentAssertions可直接自動比較物件內的所有屬性值
    }

    [TestMethod]
    public async Task LoginCheckAsync_Success_ReturnSuccess()
    {
        // Arrange
        LoginDto dto = new LoginDto { Email = "test@test.com", Password = "password" };
        User entity = new User()
        {
            Id = 1,
            AuthId = "authId",
            Name = "Test",
            Email = "test@test.com",
            Picture = "TestPicture",
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = "Test",
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            PasswordHash = "passwordHash",
            Active = true,
            IsEmailVerified = true
        };

        // 獲取資料庫該使用者密碼雜湊
        _mockRepo.Setup(r => r.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString()))
                    .ReturnsAsync(entity);

        string orgPasswordHash = entity.PasswordHash;

        // 驗證使用者輸入密碼 vs 資料庫密碼
        _mockHashService.Setup(s => s.Verify(dto.Password, orgPasswordHash))
                        .Returns(true);

        // 組裝JWT
        string tokenString = "tokenString";
        _mockUsersTokenService.Setup(s => s.CreateJWT(entity.Id))
                               .Returns(tokenString);

        // 組裝Refresh Token
        string refreshToken = "refreshToken";
        _mockUsersTokenService.Setup(s => s.CreateTokenAsync(entity.Id, TokenTypeEnum.refresh))
                               .ReturnsAsync(refreshToken);

        var expectedResult = ServiceResult<LoginResponseDto>
            .Success(new LoginResponseDto { Token = tokenString, RefreshToken = refreshToken });

        // Act
        var result = await _service.LoginCheckAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        result.Should().BeEquivalentTo(expectedResult);
    }



    [TestMethod]
    public async Task LoginCheckAsync_entityNull_ReturnLoginFailed()
    {
        // Arrange
        LoginDto dto = new LoginDto { Email = "test@test.com", Password = "password" };
        User? entity = null;

        // 獲取資料庫該使用者密碼雜湊
        _mockRepo.Setup(r => r.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString()))
                    .ReturnsAsync(entity);

        var expectedResult = ServiceResult<LoginResponseDto>
            .Fail(ErrorCodeEnum.Auth_LoginFailed, "帳號或密碼錯誤");

        // Act
        var result = await _service.LoginCheckAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task LoginCheckAsync_PasswordHashNull_ReturnLoginFailed()
    {
        // Arrange
        LoginDto dto = new LoginDto { Email = "test@test.com", Password = "password" };
        User entity = new User()
        {
            Id = 1,
            AuthId = "authId",
            Name = "Test",
            Email = "test@test.com",
            Picture = "TestPicture",
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = "Test",
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            PasswordHash = null,    // PasswordHash為null
            Active = true,
            IsEmailVerified = true
        };

        // 獲取資料庫該使用者密碼雜湊
        _mockRepo.Setup(r => r.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString()))
                    .ReturnsAsync(entity);

        var expectedResult = ServiceResult<LoginResponseDto>
            .Fail(ErrorCodeEnum.Auth_LoginFailed, "帳號或密碼錯誤");

        // Act
        var result = await _service.LoginCheckAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task LoginCheckAsync_VerifyPasswordHashFailed_ReturnLoginFailed()
    {
        // Arrange
        LoginDto dto = new LoginDto { Email = "test@test.com", Password = "password" };
        User entity = new User()
        {
            Id = 1,
            AuthId = "authId",
            Name = "Test",
            Email = "test@test.com",
            Picture = "TestPicture",
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = "Test",
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            PasswordHash = "wrongPasswordHash",
            Active = true,
            IsEmailVerified = true
        };

        // 獲取資料庫該使用者密碼雜湊
        _mockRepo.Setup(r => r.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString()))
                    .ReturnsAsync(entity);

        string orgPasswordHash = entity.PasswordHash;

        // 驗證使用者輸入密碼 vs 資料庫密碼
        _mockHashService.Setup(s => s.Verify(dto.Password, orgPasswordHash))
                        .Returns(false);

        var expectedResult = ServiceResult<LoginResponseDto>
            .Fail(ErrorCodeEnum.Auth_LoginFailed, "帳號或密碼錯誤");

        // Act
        var result = await _service.LoginCheckAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task LoginCheckAsync_EmailNotVerified_ReturnEmailNotVerified()
    {
        // Arrange
        LoginDto dto = new LoginDto { Email = "test@test.com", Password = "password" };
        User entity = new User()
        {
            Id = 1,
            AuthId = "authId",
            Name = "Test",
            Email = "test@test.com",
            Picture = "TestPicture",
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = "Test",
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            PasswordHash = "PasswordHash",
            Active = true,
            IsEmailVerified = false // Email未驗證
        };

        // 獲取資料庫該使用者密碼雜湊
        _mockRepo.Setup(r => r.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString()))
                    .ReturnsAsync(entity);

        string orgPasswordHash = entity.PasswordHash;

        // 驗證使用者輸入密碼 vs 資料庫密碼
        _mockHashService.Setup(s => s.Verify(dto.Password, orgPasswordHash))
                        .Returns(true);

        var expectedResult = ServiceResult<LoginResponseDto>
            .Fail(ErrorCodeEnum.User_EmailNotVerified, "帳號存在但未驗證Email");

        // Act
        var result = await _service.LoginCheckAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task LoginCheckAsync_UserNotActive_ReturnAccountDisabled()
    {
        // Arrange
        LoginDto dto = new LoginDto { Email = "test@test.com", Password = "password" };
        User entity = new User()
        {
            Id = 1,
            AuthId = "authId",
            Name = "Test",
            Email = "test@test.com",
            Picture = "TestPicture",
            Provider = ProviderEnum.Google.ToString(),
            ProviderKey = "Test",
            Role = RoleEnum.user.ToString(),
            CreateTime = DateTime.UtcNow,
            PasswordHash = "PasswordHash",
            Active = false, // Active = false
            IsEmailVerified = true
        };

        // 獲取資料庫該使用者密碼雜湊
        _mockRepo.Setup(r => r.SelectUserByEmailAsync(dto.Email, ProviderEnum.Local.ToString()))
                    .ReturnsAsync(entity);

        string orgPasswordHash = entity.PasswordHash;

        // 驗證使用者輸入密碼 vs 資料庫密碼
        _mockHashService.Setup(s => s.Verify(dto.Password, orgPasswordHash))
                        .Returns(true);

        var expectedResult = ServiceResult<LoginResponseDto>
            .Fail(ErrorCodeEnum.User_AccountDisabled, "帳號被停權");

        // Act
        var result = await _service.LoginCheckAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        result.Should().BeEquivalentTo(expectedResult);
    }
}
