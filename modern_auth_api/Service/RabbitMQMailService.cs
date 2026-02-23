using modern_auth_api.Interface;
using modern_auth_api.Models;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace modern_auth_api.Service
{
    public class RabbitMQMailService : IMailService
    {
        private readonly RabbitMQ _rabbitMQ;
        private readonly string _exchange = "notification";
        private readonly string _routingKey = "mail_send";
        private readonly string _queueName = "mail_queue";

        public RabbitMQMailService(RabbitMQ rabbitMQ)
        {
            _rabbitMQ = rabbitMQ;
        }

        public async Task SendMail(SendMailModel sendMailModel)
        {
            // 1. 準備傳送資料
            // 1-1. 將 mailServerSetting, mailSetting 包成物件 轉為 JSON
            string mailJson = JsonConvert.SerializeObject(sendMailModel);
            // 1-2. 發送訊息
            await _rabbitMQ.Simple(mailJson, _exchange, _routingKey, _queueName);
        }
    }
}
