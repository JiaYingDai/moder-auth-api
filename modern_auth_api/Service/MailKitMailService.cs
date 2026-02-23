using modern_auth_api.Interface;
using modern_auth_api.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace modern_auth_api.Service
{
    public class MailKitMailService : IMailService
    {
        public async Task SendMail(SendMailModel sendMailModel)
        {
            // 1. 建立郵件訊息
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("寄件人", sendMailModel.MailSetting.FromEmail));
            message.To.Add(new MailboxAddress("收件人", sendMailModel.MailSetting.ToEmail));
            message.Subject = sendMailModel.MailSetting.Subject;

            // 2. 建立郵件正文
            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = sendMailModel.MailSetting.Body?.Html; // html內容
            bodyBuilder.TextBody = sendMailModel.MailSetting.Body?.Text; // 純文字內容

            message.Body = bodyBuilder.ToMessageBody();

            // 3. 發送郵件
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(sendMailModel.MailServerSetting.SmtpHost, sendMailModel.MailServerSetting.SmtpPort,
                    sendMailModel.MailServerSetting.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                await client.AuthenticateAsync(sendMailModel.MailSetting.FromEmail, sendMailModel.MailSetting.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}
