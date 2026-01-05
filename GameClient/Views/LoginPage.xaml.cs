using GameClient.GameServiceReference;
using GameClient.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void Login(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid())
            {
                return;
            }

            string usernameOrEmail = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            var serviceClient = new GameServiceClient();

            try
            {
                var response = await serviceClient.LogInAsync(usernameOrEmail, password);

                if (response.IsSuccess)
                {
                    string serverLanguage = response.PreferredLanguage ?? "es-MX";
                    string currentLocalLanguage = GameClient.Properties.Settings.Default.LanguageCode;

                    if (string.IsNullOrEmpty(currentLocalLanguage))
                    {
                        currentLocalLanguage = "es-MX";
                    }

                    if (serverLanguage != currentLocalLanguage)
                    {
                        GameClient.Properties.Settings.Default.LanguageCode = serverLanguage;
                        GameClient.Properties.Settings.Default.Save();

                        try
                        {
                            await serviceClient.LogoutAsync(usernameOrEmail);
                        }
                        catch (CommunicationException)
                        {
                                    MessageBox.Show(
                           GameClient.Resources.Strings.ComunicationLabel,
                           GameClient.Resources.Strings.ErrorTitle);
                        }
                        catch (TimeoutException)
                        {
                            MessageBox.Show(
                    GameClient.Resources.Strings.TimeoutLabel,
                    GameClient.Resources.Strings.ErrorTitle);
                        }

                        Process.Start(Application.ResourceAssembly.Location);
                        Application.Current.Shutdown();
                        return;
                    }

                    ApplyLanguage(serverLanguage);

                    SessionManager.StartSession(usernameOrEmail, usernameOrEmail, false);

                    GameMainWindow gameMenu = new GameMainWindow(usernameOrEmail);
                    gameMenu.Show();
                    Window.GetWindow(this).Close();
                }
                else
                {
                    string errorMsg = response.Message ?? "Login failed.";
                    ShowError(UsernameBorder, errorMsg);
                    ShowError(PasswordBorder, errorMsg);
                }
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.EndpointNotFoundLabel,
                    GameClient.Resources.Strings.ErrorTitle);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.TimeoutLabel,
                    GameClient.Resources.Strings.ErrorTitle);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.ComunicationLabel,
                    GameClient.Resources.Strings.ErrorTitle);
            }
            finally
            {
                CloseClient(serviceClient);
            }
        }

        private static void ApplyLanguage(string cultureCode)
        {
                var culture = new CultureInfo(cultureCode);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;

                if (GameClient.Properties.Settings.Default.LanguageCode != cultureCode)
                {
                    GameClient.Properties.Settings.Default.LanguageCode = cultureCode;
                    GameClient.Properties.Settings.Default.Save();
                }
            
            
        }

        private static void CloseClient(GameServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
                else
                {
                    client.Abort();
                }
            }
            catch (CommunicationException)
            {
                client.Abort();
            }
            catch (TimeoutException)
            {
                client.Abort();
            }
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowError(UsernameBorder, GameClient.Resources.Strings.EmptyUsernameLabel);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowError(PasswordBorder, GameClient.Resources.Strings.EmptyPasswordLabel);
                isValid = false;
            }

            return isValid;
        }

        private void ShowError(Border field, string errorMessage)
        {
            field.BorderBrush = new SolidColorBrush(Colors.Red);
            field.ToolTip = new ToolTip { Content = errorMessage };
        }

        private void ClearAllErrors()
        {
            UsernameBorder.ClearValue(Border.BorderBrushProperty);
            UsernameBorder.ToolTip = null;
            PasswordBorder.ClearValue(Border.BorderBrushProperty);
            PasswordBorder.ToolTip = null;
        }

        private void ForgotPass(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new ForgotPassPage());
            }
        }

        private void OnUsernameTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            UsernameBorder.ClearValue(Border.BorderBrushProperty);
            UsernameBorder.ToolTip = null;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;

            PasswordBorder.ClearValue(Border.BorderBrushProperty);
            PasswordBorder.ToolTip = null;
        }

        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons();
            }
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(
                    "Por seguridad, el pegado está deshabilitado en este campo.",
                    "Acción bloqueada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnPasswordBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(
                    "Por seguridad, el pegado está deshabilitado en campos de contraseña.",
                    "Acción bloqueada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}