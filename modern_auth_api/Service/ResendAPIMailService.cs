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

        public async Task SendMail(SendMailModel sendMailModel)
        {
            var message = new EmailMessage();
            message.From = sendMailModel.MailSetting.FromEmail;  // 寄件人
            message.To.Add(sendMailModel.MailSetting.ToEmail); // 收件人
            message.Subject = sendMailModel.MailSetting.Subject ?? string.Empty;    // 主旨
            message.HtmlBody = sendMailModel.MailSetting.Body?.Html; // html內容
            message.TextBody = sendMailModel.MailSetting.Body?.Text; // 純文字內容

            await _resend.EmailSendAsync(message);
        }
    }
}
