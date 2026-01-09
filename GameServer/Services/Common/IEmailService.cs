using System.Threading.Tasks;

namespace GameServer.Helpers
{
    public interface IEmailService
    {
        Task<bool> SendVerificationEmailAsync(string email, string code, string lang);
        Task SendLoginNotificationAsync(string email, string username, string lang);
        Task SendPasswordChangedNotificationAsync(string email, string username, string lang);
        Task<bool> SendRecoveryEmailAsync(string email, string code, string lang);

        Task SendUsernameChangedNotificationAsync(string email, string oldUsername, string newUsername, string lang);
    }

    public class EmailService : IEmailService
    {
        public Task<bool> SendVerificationEmailAsync(string e, string c, string l) => EmailHelper.SendVerificationEmailAsync(e, c, l);
        public Task SendLoginNotificationAsync(string e, string u, string l) => EmailHelper.SendLoginNotificationAsync(e, u, l);
        public Task SendPasswordChangedNotificationAsync(string e, string u, string l) => EmailHelper.SendPasswordChangedNotificationAsync(e, u, l);
        public Task<bool> SendRecoveryEmailAsync(string e, string c, string l) => EmailHelper.SendRecoveryEmailAsync(e, c, l);

        public Task SendUsernameChangedNotificationAsync(string e, string o, string n, string l) => EmailHelper.SendUsernameChangedNotificationAsync(e, o, n, l);
    }
}