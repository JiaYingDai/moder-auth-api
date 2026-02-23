using modern_auth_api.Models;

namespace modern_auth_api.Interface
{
    public interface IMailService
    {
        Task SendMail(SendMailModel sendMailModel);
    }
}
