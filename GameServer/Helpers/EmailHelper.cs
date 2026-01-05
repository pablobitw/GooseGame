using System;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using GameServer.Resources;

namespace GameServer.Helpers
{
    public static class EmailHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EmailHelper));

        public static async Task<bool> SendVerificationEmailAsync(string recipientEmail, string verificationCode, string languageCode = "es-MX")
        {
            SetCulture(languageCode);
            string subject = ServerStrings.Email_VerificationSubject;
            string bodyFormat = ServerStrings.Email_VerificationBody;

            string body = $@"
                <div style='font-family: Arial, sans-serif; text-align: center;'>
                    <h2>Goose Game</h2>
                    <p>{string.Format(bodyFormat, verificationCode)}</p>
                </div>";

            return await SendEmailInternalAsync(recipientEmail, subject, body);
        }

        public static async Task<bool> SendRecoveryEmailAsync(string recipientEmail, string verificationCode, string languageCode = "es-MX")
        {
            SetCulture(languageCode);
            string subject = ServerStrings.Email_RecoverySubject;
            string bodyFormat = ServerStrings.Email_RecoveryBody;

            string body = $@"
                <div style='font-family: Arial, sans-serif; text-align: center;'>
                    <h2>Goose Game - Recovery</h2>
                    <p>{string.Format(bodyFormat, verificationCode)}</p>
                </div>";

            return await SendEmailInternalAsync(recipientEmail, subject, body);
        }

        public static async Task SendLoginNotificationAsync(string recipientEmail, string username, string languageCode = "es-MX")
        {
            SetCulture(languageCode);
            string subject = ServerStrings.Email_LoginAlertSubject;
            string bodyFormat = ServerStrings.Email_LoginAlertBody;
            string date = DateTime.Now.ToString("g");

            string body = $@"
                <div style='font-family: Arial, sans-serif; color: #333;'>
                    <h2>Goose Game - Security</h2>
                    <p>{string.Format(bodyFormat, username)}</p>
                    <p>Date: {date}</p>
                </div>";

            await SendEmailInternalAsync(recipientEmail, subject, body);
        }

        public static async Task SendPasswordChangedNotificationAsync(string recipientEmail, string username, string languageCode = "es-MX")
        {
            SetCulture(languageCode);
            string subject = "Goose Game - Password Updated";
            string date = DateTime.Now.ToString("g");

            string body = $@"
                <div style='font-family: Arial, sans-serif; color: #333;'>
                    <h2>Security Alert</h2>
                    <p>Hello {username}, your password was changed successfully.</p>
                    <p>Date: {date}</p>
                </div>";

            await SendEmailInternalAsync(recipientEmail, subject, body);
        }

        public static async Task SendUsernameChangedNotificationAsync(string recipientEmail, string oldUsername, string newUsername, string languageCode = "es-MX")
        {
            SetCulture(languageCode);
            string subject = ServerStrings.Email_UsernameChangedSubject;
            string bodyFormat = ServerStrings.Email_UsernameChangedBody;
            string date = DateTime.Now.ToString("g");

            string body = $@"
                <div style='font-family: Arial, sans-serif; color: #333;'>
                    <h2>Identity Update</h2>
                    <p>{string.Format(bodyFormat, oldUsername, newUsername)}</p>
                    <p>Date: {date}</p>
                    <hr>
                </div>";

            await SendEmailInternalAsync(recipientEmail, subject, body);
        }

        private static void SetCulture(string languageCode)
        {
            try
            {
                var culture = new CultureInfo(languageCode);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
            }
            catch
            {
                var culture = new CultureInfo("es-MX");
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        private static async Task<bool> SendEmailInternalAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                string gmailUser = ConfigurationManager.AppSettings["GmailUser"];
                string gmailPass = ConfigurationManager.AppSettings["GmailPass"];

                if (string.IsNullOrEmpty(gmailUser) || string.IsNullOrEmpty(gmailPass))
                {
                    Log.Error("Faltan las credenciales de Gmail en App.config/Web.config");
                    return false;
                }

                using (var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(gmailUser, gmailPass),
                    EnableSsl = true,
                })
                using (var mailMessage = new MailMessage
                {
                    From = new MailAddress(gmailUser, "Goose Game Support"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                })
                {
                    mailMessage.To.Add(toEmail);
                    await smtpClient.SendMailAsync(mailMessage);
                    Log.InfoFormat("Correo enviado exitosamente a {0}", toEmail);
                    return true;
                }
            }
            catch (SmtpException smtpEx)
            {
                Log.Error($"Error SMTP al enviar a {toEmail}: {smtpEx.Message}", smtpEx);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Error general al enviar correo a {toEmail}: {ex.Message}", ex);
                return false;
            }
        }
    }
}