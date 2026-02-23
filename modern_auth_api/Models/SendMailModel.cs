namespace modern_auth_api.Models
{
    public class SendMailModel
    {
        public MailServerSetting? MailServerSetting { get; set; }
        public required MailSetting MailSetting { get; set; }
    }

    public class MailServerSetting
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 0;
        public bool UseSsl { get; set; } = true;
    }

    public class MailSetting
    {
        public string FromEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public Body? Body { get; set; }
    }

    public class Body
    {
        public string? Html { get; set; }
        public string? Text { get; set; }
    }
}
