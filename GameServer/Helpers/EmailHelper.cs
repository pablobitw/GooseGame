using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using log4net;

namespace GameServer.Helpers
{
    public static class EmailHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EmailHelper));

        public static async Task<bool> SendVerificationEmailAsync(string recipientEmail, string verificationCode)
        {
            string subject = "Goose Game - Código de Verificación";
            string body = $@"
                <div style='font-family: Arial, sans-serif; text-align: center;'>
                    <h2>¡Bienvenido a Goose Game!</h2>
                    <p>Tu código de verificación es:</p>
                    <h1 style='color: #4CAF50; letter-spacing: 5px;'>{verificationCode}</h1>
                    <p>Introduce este código en el juego para activar tu cuenta.</p>
                </div>";
            return await SendEmailInternalAsync(recipientEmail, subject, body);
        }

        public static async Task<bool> SendRecoveryEmailAsync(string recipientEmail, string verificationCode)
        {
            string subject = "Goose Game - Recuperación de Contraseña";
            string body = $@"
                <div style='font-family: Arial, sans-serif; text-align: center;'>
                    <h2>Recuperación de Contraseña</h2>
                    <p>Has solicitado restablecer tu contraseña. Usa este código:</p>
                    <h1 style='color: #F44336; letter-spacing: 5px;'>{verificationCode}</h1>
                    <p>Si no fuiste tú, ignora este mensaje.</p>
                </div>";
            return await SendEmailInternalAsync(recipientEmail, subject, body);
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
                Log.Error(string.Format("Error SMTP al enviar a {0}: {1}", toEmail, smtpEx.Message), smtpEx);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Error general al enviar correo a {0}: {1}", toEmail, ex.Message), ex);
                return false;
            }
        }
    }
}
