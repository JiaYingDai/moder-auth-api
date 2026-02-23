using RabbitMQ.Client;
using System.Text;

namespace modern_auth_api.Service
{
    public class RabbitMQ : IDisposable
    {
        private readonly ILogger<RabbitMQ> _logger;
        private IConnection? _connection;
        private IChannel? _channel;
        private bool _disposed = false;

        public RabbitMQ(ILogger<RabbitMQ> logger)
        {
            _logger = logger;
        }

        public IChannel Channel => _channel ?? throw new InvalidOperationException($"RabbitMq connection not initialized.");

        public async Task ConnectAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Rabbitmq connectionString must be provided via configuration (Rabbitmq:ConnectionString).", nameof(connectionString));
            }

            try
            {
                var factory = new ConnectionFactory()
                {
                    Uri = new Uri(connectionString)
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                _logger.LogInformation("RabbitMQ connected successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ.");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // 釋放託管資源
                try
                {
                    _connection?.CloseAsync(); // 先關閉
                    _connection?.Dispose(); // 再釋放
                    _logger.LogInformation("RabbitMQ connection disposed.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during RabbitMQ disposal.");
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// RabbitMQ 最基本的訊息傳遞模式，下一個 Producer 傳輸到 Queue 內，Consumer 再接收訊息並處理
        /// </summary>
        /// <param name="message">傳送的訊息</param>
        /// <param name="exchange">訊息中心名稱</param>
        /// <param name="routingKey">識別證</param>
        public async Task Simple(string message, string? exchange, string? routingKey, string queueName)
        {
            // 1. 如果有指定 Exchange，必須先宣告它 (確保它存在)
            if (!string.IsNullOrEmpty(exchange))
            {
                // 宣告一個 Direct 類型的交換機
                await Channel.ExchangeDeclareAsync(exchange: exchange, type: "direct", durable: true, autoDelete: false);
            }
            // Declares a queue within the channel
            // queue: name of queue
            // durable: false - 佇列無法在 broker 重新啟動時存活
            // exclusive: false - 該佇列可由其他連線使用
            // autoDelete: false - 當最後一個消費者取消訂閱時，佇列不會被刪除。
            // arguments：附加的佇列參數(在此處設定為 null) 。
            await Channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            if (!string.IsNullOrEmpty(exchange))
            {
                // 綁訂 routingKey 與 Queue
                await Channel.QueueBindAsync(queue: queueName, exchange: exchange, routingKey: routingKey ?? string.Empty);
            }

            // Prepares the message to be sent
            var body = Encoding.UTF8.GetBytes(message);

            // Publishes the message to the specified exchange and routing key
            // exchange: string.Empty(默認交換中心) or 指定交換中心
            // routingKey: 識別證
            // body: 實際的訊息
            await Channel.BasicPublishAsync(exchange: exchange ?? string.Empty, routingKey: routingKey ?? string.Empty, mandatory: false, basicProperties: new BasicProperties(), body: body);
        }
    }
}
