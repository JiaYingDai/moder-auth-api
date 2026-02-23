
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading.Channels;

namespace modern_auth_api.Service
{
    public abstract class BasicConsumerService : BackgroundService
    {
        private readonly RabbitMQ _rabbitMQ;
        protected readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider; // 解決 Scopped 注入 使用 IServiceProvide
        private IChannel? _channel;

        public BasicConsumerService(RabbitMQ rabbitMQ, ILogger logger, IServiceProvider serviceProvider)
        {
            _rabbitMQ = rabbitMQ;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // 強迫子類別提供 QueueName
        protected abstract string QueueName { get; }

        // 強迫子類別實作 處理邏輯
        protected abstract Task ProcessMessageAsync(String message, IServiceProvider serviceProvider, CancellationToken stoppingToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("正在啟動RabbitMQ背景服務...");

            try
            {
                // 1. 建立Channel
                _channel = _rabbitMQ.Channel;

                // 2. 宣告 Queue，確定 Queue 存在
                await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false);

                // 3. 開始消費
                await StartConsuming(QueueName, stoppingToken);

                // 讓 BackgroundService 保持活著，直到系統關閉
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"啟動RabbitMQ背景服務時發生錯誤，原因為:{ex.Message}");
            }
        }

        public async Task StartConsuming(string queueName, CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            // 定義Callback
            consumer.ReceivedAsync += async (sender, args) =>
            {
                try
                {
                    var body = args.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // 建立新的 scope，此時才能拿到 Scoped 的 Service
                    using var scope = _serviceProvider.CreateScope();

                    // 子類別實作邏輯
                    await ProcessMessageAsync(message, scope.ServiceProvider, stoppingToken);

                    // 成功處理，手動確認 (Ack)
                    await _channel.BasicAckAsync(deliveryTag: args.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"處理 Queue [{QueueName}] 訊息時發生錯誤: {ex.Message}");
                    // 處理失敗，將訊息退回 Queue (Nack)，或者送去 Dead Letter Queue
                    // requeue: true 代表重新排隊 (如果一直失敗可能會無限迴圈，實務上通常搭配重試機制)
                    await _channel.BasicNackAsync(deliveryTag: args.DeliveryTag, multiple: false, requeue: false);
                }
            };

            // 開始監聽 (autoAck: false)
            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false, // true: 自動確認，RabbitMQ 消費者中途出錯，訊息會遺失，不會再重傳 / false: 手動確認，消費者需明確告訴RabbitMQ訊息處理完成才能刪除訊息，如果消費者中途出錯，RabbitMQ會把這個訊息重新放回佇列
                consumerTag: "", // 唯一標識消費者的標籤
                noLocal: true, // true: 消費者不接收同一連接生產者發送的消息 / false: 消費者可接收所有消息，包括來自同一連接內生產者
                exclusive: false, // 確保隊列或消費者的唯一性
                arguments: null,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation($"RabbitMQ Consumer 已啟動，正在監聽 Queue: {QueueName}...");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null)
            {
                await _channel.CloseAsync(cancellationToken);
                _channel.Dispose();
            }
            await base.StopAsync(cancellationToken);
        }
    }
}
