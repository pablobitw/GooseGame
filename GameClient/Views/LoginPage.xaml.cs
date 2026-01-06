using GameClient.GameServiceReference;
using GameClient.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void Login(object sender, RoutedEventArgs e)
        {
            if (IsFormValid())
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    string usernameOrEmail = UsernameTextBox.Text;
                    string password = PasswordBox.Password;
                    var serviceClient = new GameServiceClient();

                    try
                    {
                        var response = await serviceClient.LogInAsync(usernameOrEmail, password);

                        if (response.IsSuccess)
                        {
                            await HandleSuccessfulLogin(response, usernameOrEmail, serviceClient);
                        }
                        else
                        {
                            HandleLoginError(response.Message);
                        }
                    }
                    catch (EndpointNotFoundException)
                    {
                        ShowTranslatedMessageBox("Login_Error_ServerDown", "Login_Title_Error");
                    }
                    catch (TimeoutException)
                    {
                        ShowTranslatedMessageBox("Login_Error_Timeout", "Login_Title_Error");
                    }
                    catch (FaultException)
                    {
                        ShowTranslatedMessageBox("Login_Error_Database", "Login_Title_Error");
                    }
                    catch (CommunicationException)
                    {
                        ShowTranslatedMessageBox("Login_Error_Communication", "Login_Title_Error");
                    }
                    catch (Exception ex)
                    {
                        string generalError = GameClient.Resources.Strings.Login_Error_General;
                        string title = GameClient.Resources.Strings.Login_Title_Error;
                        MessageBox.Show($"{generalError}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        CloseClient(serviceClient);
                    }
                }
                else
                {
                    ShowTranslatedMessageBox("Login_Error_NoInternet", "Login_Title_Error");
                }
            }
        }

        private void HandleLoginError(string messageCode)
        {
            switch (messageCode)
            {
                case "DbError":
                    ShowTranslatedMessageBox("Login_Error_Database", "Login_Title_Error");
                    break;
                case "UserBanned":
                    ShowTranslatedMessageBox("Login_Error_Banned", "Login_Title_Error");
                    break;
                case "AccountInactive":
                    ShowTranslatedMessageBox("Login_Error_Inactive", "Login_Title_Error");
                    break;
                case "UserAlreadyOnline":
                    ShowTranslatedMessageBox("Login_Error_AlreadyOnline", "Login_Title_Error");
                    break;
                case "InvalidCredentials":
                default:
                    string errorMsg = GameClient.Resources.Strings.Login_Error_Credentials;
                    ShowError(UsernameBorder, errorMsg);
                    ShowError(PasswordBorder, errorMsg);
                    break;
            }
        }

        private async Task HandleSuccessfulLogin(LoginResponseDto response, string username, GameServiceClient client)
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
                    await client.LogoutAsync(username);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error no crítico al cerrar sesión antes de reiniciar: {ex.Message}");
                }

                Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
                return;
            }

            ApplyLanguage(serverLanguage);
            UserSession.GetInstance().SetSession(username, username, false);
            GameMainWindow gameMenu = new GameMainWindow(username);
            gameMenu.Show();
            Window.GetWindow(this).Close();
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
            catch (Exception)
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
                ShowError(UsernameBorder, GameClient.Resources.Strings.Login_Val_EmptyField);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowError(PasswordBorder, GameClient.Resources.Strings.Login_Val_EmptyField);
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
            }), DispatcherPriority.Background);
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
            }), DispatcherPriority.Background);
        }
    }
}