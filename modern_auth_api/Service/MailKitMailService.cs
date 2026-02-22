using modern_auth_api.Interface;
using modern_auth_api.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace modern_auth_api.Service
{
    public class MailKitMailService : IMailService
    {
        public async Task SendMail(MailSeverSetting mailServerSetting, MailSetting mailSetting)
        {
            // 1. 建立郵件訊息
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("寄件人", mailSetting.FromEmail));
            message.To.Add(new MailboxAddress("收件人", mailSetting.ToEmail));
            message.Subject = mailSetting.Subject;

            // 2. 建立郵件正文
            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = mailSetting.Body?.Html; // html內容
            bodyBuilder.TextBody = mailSetting.Body?.Text; // 純文字內容

            message.Body = bodyBuilder.ToMessageBody();

            // 3. 發送郵件
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(mailServerSetting.SmtpHost, mailServerSetting.SmtpPort,
                    mailServerSetting.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                await client.AuthenticateAsync(mailSetting.FromEmail, mailSetting.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}
