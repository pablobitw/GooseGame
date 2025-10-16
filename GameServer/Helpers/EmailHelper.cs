using System;
using System.Configuration;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace GameServer.Helpers
{
    public static class EmailHelper
    {
        public static async Task<bool> EnviarCorreoDeVerificacion(string destinatarioEmail, string codigoVerificacion)
        {
            try
            {
                var apiKey = ConfigurationManager.AppSettings["SendGridApiKey"];
                var client = new SendGridClient(apiKey);

                var from = new EmailAddress("dagoosegame@gmail.com", "Goose Game");
                var to = new EmailAddress(destinatarioEmail);
                var subject = "Código de Verificación para Goose Game";
                var plainTextContent = $"Tu código de verificación es: {codigoVerificacion}";
                var htmlContent = $"<strong>¡Bienvenido a Goose Game!</strong><p>Tu código de verificación es: <strong>{codigoVerificacion}</strong></p>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

                // usamos 'await' en lugar de '.Result'
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Correo de verificación enviado a {destinatarioEmail}.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Error al enviar correo: SendGrid devolvió el código {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo: {ex.Message}");
                return false;
            }
        }
    }
}