using modern_auth_api.Interface;
using modern_auth_api.Models;
using MimeKit;
using Resend;

namespace modern_auth_api.Service
{
    public class ResendAPIMailService : IMailService
    {
        private readonly IResend _resend;
        public ResendAPIMailService(IResend resend)
        {
            _resend = resend;
        }

        public async Task SendMail(MailSeverSetting mailServerSetting, MailSetting mailSetting)
        {
            var message = new EmailMessage();
            message.From = mailSetting.FromEmail;  // 寄件人
            message.To.Add(mailSetting.ToEmail); // 收件人
            message.Subject = mailSetting.Subject ?? string.Empty;    // 主旨
            message.HtmlBody = mailSetting.Body?.Html; // html內容
            message.TextBody = mailSetting.Body?.Text; // 純文字內容

            await _resend.EmailSendAsync(message);
        }
    }
}
