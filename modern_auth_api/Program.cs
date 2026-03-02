using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using modern_auth_api.Entity;
using modern_auth_api.Interface;
using modern_auth_api.Repository;
using modern_auth_api.Service;
using Resend;
using Serilog;
using Supabase;
using System.Text;
using ISupabaseClient = Supabase.Interfaces.ISupabaseClient<
    Supabase.Gotrue.User,
    Supabase.Gotrue.Session,
    Supabase.Realtime.RealtimeSocket,
    Supabase.Realtime.RealtimeChannel,
    Supabase.Storage.Bucket,
    Supabase.Storage.FileObject
>;
Serilog.Debugging.SelfLog.Enable(Console.Error);
// 設定啟動時的基本 Logger (先建臨時 Logger)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() //將Log輸出到終端機
    .CreateBootstrapLogger();

try
{
    // 開啟Dapper底線轉換功能
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

    var builder = WebApplication.CreateBuilder(args);
    Log.Information($"Current Environment: {builder.Environment.EnvironmentName}");

    // Use Serilog (讀入組態設定 appsettings.json)
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)  //從設定檔中讀取
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services to the container.
    builder.Services.AddControllers();

    // 註冊 DbContext(EF Core)
    builder.Services.AddDbContext<PostgresContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 註冊Supabase Storage
    builder.Services.AddScoped<ISupabaseClient>(_ =>
        new Supabase.Client(
            builder.Configuration["Supabase:Url"],
            builder.Configuration["Supabase:ApiKey"],
            new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            }
        )
    );

    // 註冊Resend API
    builder.Services.AddOptions();
    builder.Services.AddHttpClient<ResendClient>();
    builder.Services.Configure<ResendClientOptions>(o =>
    {
        o.ApiToken = builder.Configuration["ResendApiKey"];
    });
    builder.Services.AddTransient<IResend, ResendClient>();

    // Register Redis singleton
    builder.Services.AddSingleton<Redis>();

    // Register RabbitMQ singleton
    builder.Services.AddSingleton<modern_auth_api.Service.RabbitMQ>();

    // 註冊Repository
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IUsersTokenRepository, RedisUsersTokenRepository>();


    // 註冊Service
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IHashService, Argon2HashService>();
    builder.Services.AddScoped<IFileStorageService, FileStorageSupabaseService>();
    builder.Services.AddKeyedScoped<IMailService, RabbitMQMailService>("producer"); // 根據標籤決定注入何者。producer使用的mailService
    builder.Services.AddKeyedScoped<IMailService, ResendAPIMailService>("consumer"); // 根據標籤決定注入何者。producer使用的mailService
    builder.Services.AddScoped<IUsersTokenService, UsersTokenService>();

    // 註冊背景處理
    builder.Services.AddHostedService<EmailConsumerService>();

    // Authentication
    var securityKey = builder.Configuration["SecurityKey"];
    builder.Services.AddAuthentication(
        options =>
        {
            // 設定預設驗證方案為 JWT
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }
    ).AddJwtBearer(
        options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                // 驗證簽名
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey)),

                // 驗證發行者 (Issuer) & 接收者 (Audience)
                ValidateIssuer = false,
                ValidateAudience = false,

                // 驗證過期時間
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
        }
    );

    // Cors
    var allowedOrigins = builder.Configuration["AllowedOrigins"];
    // 預設值
    if (string.IsNullOrEmpty(allowedOrigins))
    {
        allowedOrigins = "http://localhost:5173";
    }
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(o => o.Trim())
                                             .ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();  // 允許帶Cookie
        });
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    //builder.Services.AddEndpointsApiExplorer();
    //builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Serilog RequestLogging
    app.UseSerilogRequestLogging();

    //// Configure the HTTP request pipeline.
    //if (app.Environment.IsDevelopment())
    //{
    //    app.UseSwagger();
    //    app.UseSwaggerUI();
    //}

    using (var scope = app.Services.CreateScope())
    {
        // Initialize Redis connection
        var redis = scope.ServiceProvider.GetRequiredService<Redis>();
        var redisConnStr = builder.Configuration["Redis:ConnectionString"];
        if (!string.IsNullOrEmpty(redisConnStr)) await redis.ConnectAsync(redisConnStr);
        else Log.Warning("Redis ConnectionString is missing!");

        // Initialize RabbitMQ connection
        var rabbitMq = scope.ServiceProvider.GetRequiredService<modern_auth_api.Service.RabbitMQ>();
        var rabbitConnStr = builder.Configuration["RabbitMq:ConnectionString"];
        if (!string.IsNullOrEmpty(rabbitConnStr)) await rabbitMq.ConnectAsync(rabbitConnStr);
        else Log.Warning("RabbitMQ ConnectionString is missing!");
    }

    app.UseHttpsRedirection();

    // Cors
    app.UseCors();

    // 靜態檔案映射
    // 1.上傳檔案位址
    var rootPath = builder.Configuration.GetValue<string>("File:Root") ?? Directory.GetCurrentDirectory();
    var uploadFolder = builder.Configuration.GetValue<string>("File:UploadFolder") ?? "uploads";
    var uploadPath = Path.Combine(rootPath, uploadFolder);

    // 2.確保資料夾存在
    if (!Directory.Exists(uploadPath))
    {
        Directory.CreateDirectory(uploadPath);
    }
    // 3. 加入靜態檔案映射中介，別名/upload
    app.UseStaticFiles(new StaticFileOptions
    {
        RequestPath = "/upload",
        FileProvider = new PhysicalFileProvider(uploadPath)
    });

    // 驗證
    app.UseAuthentication();

    // 授權
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    // 將最後剩餘的 Log 寫入到 Sinks 去！
    Log.CloseAndFlush();
}


