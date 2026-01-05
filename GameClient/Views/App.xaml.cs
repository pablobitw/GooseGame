using System;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace GameClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string languageCode = GameClient.Properties.Settings.Default.LanguageCode;

            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "es-MX";
            }

            try
            {
                CultureInfo culture = new CultureInfo(languageCode);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error al establecer cultura: {ex.Message}");
            }

            base.OnStartup(e);
        }
    }
}
