using GameClient.Helpers;
using GameClient.UserProfileServiceReference;
using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views
{
    public partial class OptionsPage : Page
    {
        private string _initialLanguage;

        public OptionsPage()
        {
            InitializeComponent();
            this.Loaded += OptionsPage_Loaded;
        }

        private void OptionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
            LoadCurrentLanguage();
        }

        private void AboutUsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new CreditsPage());
        }

        private void LoadCurrentSettings()
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                if (mainWindow.WindowStyle == WindowStyle.None && mainWindow.WindowState == WindowState.Maximized)
                {
                    ScreenModeComboBox.SelectedIndex = 0;
                }
                else if (mainWindow.WindowStyle == WindowStyle.None && mainWindow.WindowState == WindowState.Normal)
                {
                    ScreenModeComboBox.SelectedIndex = 1;
                }
                else
                {
                    ScreenModeComboBox.SelectedIndex = 2;
                }
            }
        }

        private void LoadCurrentLanguage()
        {
            string currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
            _initialLanguage = currentCulture;

            if (currentCulture.StartsWith("es"))
            {
                LanguageComboBox.SelectedIndex = 0;
            }
            else if (currentCulture.StartsWith("en"))
            {
                LanguageComboBox.SelectedIndex = 1;
            }
            else if (currentCulture.StartsWith("fr"))
            {
                LanguageComboBox.SelectedIndex = 2;
            }
            else
            {
                LanguageComboBox.SelectedIndex = 0;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyVideoSettings();

            string selectedLanguage = GetSelectedLanguageCode();
            bool languageChanged = selectedLanguage != _initialLanguage;

            if (languageChanged)
            {
                // VALIDACIÓN CRÍTICA: Si no hay usuario, no podemos guardar en BD
                if (string.IsNullOrEmpty(SessionManager.CurrentUsername))
                {
                    MessageBox.Show("No se detectó una sesión activa. Por favor, vuelve a iniciar sesión.", "Error de Sesión", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveButton.IsEnabled = false;
                var client = new UserProfileServiceClient();

                try
                {
                    // 1. Intentar guardar en el servidor
                    bool success = await client.UpdateLanguageAsync(SessionManager.CurrentUsername, selectedLanguage);

                    if (success)
                    {
                        // 2. Guardar configuración LOCALMENTE (Para que al reiniciar arranque en el idioma correcto)
                        GameClient.Properties.Settings.Default.LanguageCode = selectedLanguage;
                        GameClient.Properties.Settings.Default.Save();

                        var result = MessageBox.Show(
                             "El idioma ha cambiado. La aplicación se reiniciará para aplicar los cambios.",
                             "Cambio Exitoso",
                             MessageBoxButton.OKCancel,
                             MessageBoxImage.Information);

                        if (result == MessageBoxResult.OK)
                        {
                            // 3. Reiniciar la aplicación
                            Process.Start(Application.ResourceAssembly.Location);
                            Application.Current.Shutdown();
                        }
                    }
                    else
                    {
                        MessageBox.Show("El servidor no pudo actualizar tu preferencia de idioma. Inténtalo más tarde.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error de conexión al guardar idioma: " + ex.Message, "Error");
                }
                finally
                {
                    CloseClient(client);
                    SaveButton.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.ConfigSavedMessage,
                    GameClient.Resources.Strings.ConfigSavedTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private string GetSelectedLanguageCode()
        {
            switch (LanguageComboBox.SelectedIndex)
            {
                case 0: return "es-MX";
                case 1: return "en-US";
                case 2: return "fr-FR";
                default: return "es-MX";
            }
        }

        private void ApplyVideoSettings()
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow == null) return;

            int selectedMode = ScreenModeComboBox.SelectedIndex;

            switch (selectedMode)
            {
                case 0:
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Maximized;
                    mainWindow.ResizeMode = ResizeMode.NoResize;
                    break;

                case 1:
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.NoResize;
                    mainWindow.Width = 1280;
                    mainWindow.Height = 720;
                    mainWindow.CenterWindow();
                    break;

                case 2:
                    mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.CanResize;
                    break;
            }
        }

        private void CloseClient(UserProfileServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened) client.Close();
                else client.Abort();
            }
            catch
            {
                client.Abort();
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                await mainWindow.ShowMainMenu();
            }
        }
    }
}