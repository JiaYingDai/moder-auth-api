using modern_auth_api.Models;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;
using IDatabase = StackExchange.Redis.IDatabase;

namespace modern_auth_api.Service
{
    public class Redis : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private ConnectionMultiplexer? _connection;
        private IDatabase? _db;
        private bool _disposed = false;
        private readonly ILogger<Redis> _logger;

        public Redis(ILoggerFactory loggerFactory, ILogger<Redis> logger)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public IDatabase Db => _db ?? throw new InvalidOperationException($"Redis connection not initialized. Call {nameof(ConnectAsync)}() first.");

        public async Task ConnectAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Redis ConnectionString must be provided via configuration (Redis:ConnectionString).", nameof(connectionString));
            }

            var options = ConfigurationOptions.Parse(connectionString);
            options.LoggerFactory = _loggerFactory;
            options.AbortOnConnectFail = false;  // 避免連線失敗程式崩潰

            //await options.ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential());
            _connection = await ConnectionMultiplexer.ConnectAsync(options);
            _db = _connection.GetDatabase();
        }

        /// <summary>
        /// 讀取redis value
        /// </summary>
        /// <param name="key"></param>
        /// <returns>string?</returns>
        public async Task<string?> GetValue(string key)
        {
            // Read current value from Redis
            var value = await Db.StringGetAsync(key);

            return value;
        }

        /// <summary>
        /// 設Redis key, value
        /// </summary>
        /// <param name="redisModel"></param>
        public async Task SetValue(RedisModel redisModel)
        {
            // 確保有key傳入
            if (string.IsNullOrEmpty(redisModel.Key)) throw new ArgumentNullException(nameof(redisModel.Key));

            // Update value in Redis
            if (redisModel.ExpireTimeSpan.HasValue)
            {
                await Db.StringSetAsync(redisModel.Key, redisModel.Value, redisModel.ExpireTimeSpan.Value);
            }
            else
            {
                await Db.StringSetAsync(redisModel.Key, redisModel.Value);
            }
        }

        /// <summary>
        /// 刪除Redis value
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task DeleteValue(string key)
        {
            // Delete value in Redis
            await Db.KeyDeleteAsync(key);
        }

        /// <summary>
        /// 取得Redis配的自動tokenId
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetIncrementTokenId()
        {
            long tokenId = await Db.StringIncrementAsync("sys:token_id_seq");
            return tokenId;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
            _db = null;
            GC.SuppressFinalize(this);
        }
    }
}
