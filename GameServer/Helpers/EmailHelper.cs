using System;
using System.Configuration;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using log4net;
using System.Net.Http;

namespace GameServer.Helpers
{
    public static class EmailHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EmailHelper));

        public static async Task<bool> SendVerificationEmailAsync(string recipientEmail, string verificationCode)
        {
            bool isSuccess = false;

            try
            {
                var apiKey = ConfigurationManager.AppSettings["SendGridApiKey"];
                var client = new SendGridClient(apiKey);

                var from = new EmailAddress("dagoosegame@gmail.com", "Goose Game");
                var to = new EmailAddress(recipientEmail);
                var subject = "Goose Game Verification Code";
                var plainTextContent = $"Your verification code is: {verificationCode}";
                var htmlContent = $"<strong>Welcome to Goose Game!</strong><p>Your verification code is: <strong>{verificationCode}</strong></p>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    Log.Info($"Verification email sent to {recipientEmail}.");
                    isSuccess = true;
                }
                else
                {
                    Log.Warn($"Failed to send email: SendGrid returned code {response.StatusCode} for {recipientEmail}.");
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"Network error sending email (HttpRequestException) to {recipientEmail}: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"General error sending email to {recipientEmail}: {ex.Message}", ex);
            }

            return isSuccess;
        }

        public static async Task<bool> SendRecoveryEmailAsync(string recipientEmail, string verificationCode)
        {
            bool isSuccess = false;

            try
            {
                var apiKey = ConfigurationManager.AppSettings["SendGridApiKey"];
                var client = new SendGridClient(apiKey);

                var from = new EmailAddress("dagoosegame@gmail.com", "Goose Game");
                var to = new EmailAddress(recipientEmail);
                var subject = "Goose Game Recovery Code";
                var plainTextContent = $"Your recovery code is: {verificationCode}";
                var htmlContent = $"<strong>You are about to change your password!</strong><p>Your recovery code is: <strong>{verificationCode}</strong></p>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    Log.Info($"Recovery email sent to {recipientEmail}.");
                    isSuccess = true;
                }
                else
                {
                    Log.Warn($"Failed to send email: SendGrid returned code {response.StatusCode} for {recipientEmail}.");
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"Network error sending recovery email (HttpRequestException) to {recipientEmail}: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"General error sending recovery email to {recipientEmail}: {ex.Message}", ex);
            }

            return isSuccess;
        }
    }
}