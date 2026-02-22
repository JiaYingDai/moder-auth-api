namespace modern_auth_api.Models
{
    public class MailSeverSetting
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set;} = 0;
        public bool UseSsl { get; set; } = true;
    }
}
