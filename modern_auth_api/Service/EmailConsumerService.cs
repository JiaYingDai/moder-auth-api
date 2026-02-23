
using modern_auth_api.Interface;
using modern_auth_api.Models;
using Newtonsoft.Json;

namespace modern_auth_api.Service
{
    public class EmailConsumerService : BasicConsumerService
    {
        public EmailConsumerService(RabbitMQ rabbitMQ, ILogger<EmailConsumerService> logger, IServiceProvider serviceProvider) : base(rabbitMQ, logger, serviceProvider)
        {
        }

        protected override string QueueName => "mail_queue";

        protected override async Task ProcessMessageAsync(string message, IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {
            // 1. 解析 RabbitMQ 的 mail message (JSON)
            SendMailModel? mailModel = JsonConvert.DeserializeObject<SendMailModel>(message);
            if (mailModel != null) {
                _logger.LogInformation($"準備發送 Email 給 {mailModel.MailSetting.ToEmail}");

                // 2. 從剛建立的 Scope 拿出 IMailService (指定標籤為 consumer 的 mailService)
                var mailService = serviceProvider.GetRequiredKeyedService<IMailService>("consumer");

                // 3. 呼叫 mailService 原來的寄信邏輯
                await mailService.SendMail(mailModel);

                _logger.LogInformation($"Email 發送成功: {mailModel.MailSetting.ToEmail}");
            }
        }
    }
}
