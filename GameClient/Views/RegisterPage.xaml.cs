using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using GameClient.AuthServiceReference;
using System.ServiceModel;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Threading;

namespace GameClient.Views
{
    public partial class RegisterPage : Page
    {
        private const int MinPasswordLength = 8;
        private const int MaxPasswordLength = 50;
        private const string DefaultLanguage = "es-MX";

        public RegisterPage()
        {
            InitializeComponent();
        }

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnGenericTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var placeholder = textBox.Tag as TextBlock;
            if (placeholder != null)
            {
                placeholder.Visibility = string.IsNullOrEmpty(textBox.Text)
                  ? Visibility.Visible
                  : Visibility.Collapsed;
            }
        }

        private void OnGenericPasswordFocus(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;
            if (placeholder != null)
            {
                placeholder.Visibility = Visibility.Collapsed;
            }
        }

        private void OnGenericPasswordLost(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;
            if (placeholder != null && string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                placeholder.Visibility = Visibility.Visible;
            }
        }

        private void OnGenericPasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;
            if (placeholder != null)
            {
                placeholder.Visibility = string.IsNullOrEmpty(passwordBox.Password)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            passwordBox.ClearValue(Border.BorderBrushProperty);
            passwordBox.ToolTip = null;

            RepeatBox.ClearValue(Border.BorderBrushProperty);
            RepeatBox.ToolTip = null;
        }

        private async void CreateAccount(object sender, RoutedEventArgs e)
        {
            if (IsFormValid())
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    string email = EmailBox.Text;
                    string username = UserBox.Text;
                    string password = PasswordBox.Password;
                    string selectedLanguage = GetSelectedLanguage();

                    var serviceClient = new AuthServiceClient();

                    try
                    {
                        var request = new RegisterUserRequest
                        {
                            Username = username,
                            Email = email,
                            Password = password,
                            PreferredLanguage = selectedLanguage
                        };

                        RegistrationResult result = await serviceClient.RegisterUserAsync(request);
                        HandleRegistrationResult(result, email);
                    }
                    catch (EndpointNotFoundException)
                    {
                        ShowTranslatedMessageBox("Register_Error_ServerDown", "Register_Title_Error");
                    }
                    catch (TimeoutException)
                    {
                        ShowTranslatedMessageBox("Register_Error_Timeout", "Register_Title_Error");
                    }
                    catch (FaultException)
                    {
                        ShowTranslatedMessageBox("Register_Error_Database", "Register_Title_Error");
                    }
                    catch (CommunicationException)
                    {
                        ShowTranslatedMessageBox("Register_Error_Communication", "Register_Title_Error");
                    }
                    catch (Exception ex)
                    {
                        string generalError = GameClient.Resources.Strings.Register_Error_General;
                        string errorTitle = GameClient.Resources.Strings.Register_Title_Error;
                        MessageBox.Show($"{generalError}\n{ex.Message}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        CloseServiceClient(serviceClient);
                    }
                }
                else
                {
                    ShowTranslatedMessageBox("Register_Error_NoInternet", "Register_Title_Error");
                }
            }
        }

        private string GetSelectedLanguage()
        {
            string language = DefaultLanguage;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                language = item.Tag.ToString();
            }
            return language;
        }

        private void HandleRegistrationResult(RegistrationResult result, string email)
        {
            switch (result)
            {
                case RegistrationResult.Success:
                    ShowTranslatedMessageBox("Register_SuccessDesc", "Register_Title_Success");
                    NavigationService.Navigate(new VerifyAccountPage(email));
                    break;

                case RegistrationResult.EmailPendingVerification:
                    ShowTranslatedMessageBox("Register_PendingDesc", "Register_PendingTitle");
                    NavigationService.Navigate(new VerifyAccountPage(email));
                    break;

                case RegistrationResult.UsernameAlreadyExists:
                    ShowTranslatedMessageBox("Register_UserExistsDesc", "Register_UserExistsTitle");
                    ShowError(UserBox, GameClient.Resources.Strings.Register_UserExistsDesc);
                    break;

                case RegistrationResult.EmailAlreadyExists:
                    ShowTranslatedMessageBox("Register_EmailExistsDesc", "Register_EmailExistsTitle");
                    ShowError(EmailBox, GameClient.Resources.Strings.Register_EmailExistsDesc);
                    break;

                case RegistrationResult.FatalError:
                    ShowTranslatedMessageBox("Register_Error_Database", "Register_Title_Error");
                    break;

                default:
                    ShowTranslatedMessageBox("Register_Error_General", "Register_Title_Error");
                    break;
            }
        }

        private static void CloseServiceClient(AuthServiceClient serviceClient)
        {
            if (serviceClient.State == CommunicationState.Opened)
            {
                serviceClient.Close();
            }
            else
            {
                serviceClient.Abort();
            }
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            if (!ValidateEmail()) isValid = false;
            if (!ValidateUsername()) isValid = false;

            bool isPasswordStrong = ValidatePasswordStrength();
            bool isRepeatPopulated = ValidateRepeatPasswordNotEmpty();

            if (isPasswordStrong && isRepeatPopulated)
            {
                if (!CheckPasswordsMatch()) isValid = false;
            }
            else
            {
                if (!isPasswordStrong || !isRepeatPopulated) isValid = false;
            }

            return isValid;
        }

        private bool ValidateEmail()
        {
            bool isValid = true;
            string email = EmailBox.Text;
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError(EmailBox, GameClient.Resources.Strings.Register_Val_EmptyField);
                isValid = false;
            }
            else if (!IsValidEmail(email))
            {
                ShowError(EmailBox, GameClient.Resources.Strings.Register_Val_InvalidEmail);
                isValid = false;
            }
            return isValid;
        }

        private bool ValidateUsername()
        {
            bool isValid = true;
            if (string.IsNullOrWhiteSpace(UserBox.Text))
            {
                ShowError(UserBox, GameClient.Resources.Strings.Register_Val_EmptyField);
                isValid = false;
            }
            return isValid;
        }

        private bool ValidatePasswordStrength()
        {
            bool isValid = true;
            string password = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError(PasswordBox, GameClient.Resources.Strings.Register_Val_EmptyField);
                isValid = false;
            }
            else if (password.Length < MinPasswordLength ||
                password.Length > MaxPasswordLength ||
                !password.Any(char.IsUpper) ||
                !password.Any(c => !char.IsLetterOrDigit(c)))
            {
                ShowError(PasswordBox, GameClient.Resources.Strings.Register_Val_PassStrength);
                isValid = false;
            }
            return isValid;
        }

        private bool ValidateRepeatPasswordNotEmpty()
        {
            bool isValid = true;
            if (string.IsNullOrWhiteSpace(RepeatBox.Password))
            {
                ShowError(RepeatBox, GameClient.Resources.Strings.Register_Val_EmptyField);
                isValid = false;
            }
            return isValid;
        }

        private bool CheckPasswordsMatch()
        {
            bool isValid = true;
            if (PasswordBox.Password != RepeatBox.Password)
            {
                string msg = GameClient.Resources.Strings.Register_Val_PassMismatch;
                ShowError(PasswordBox, msg);
                ShowError(RepeatBox, msg);
                isValid = false;
            }
            return isValid;
        }

        private void ShowError(Control field, string errorMessage)
        {
            field.BorderBrush = new SolidColorBrush(Colors.Red);
            field.ToolTip = new ToolTip { Content = errorMessage };
        }

        private void ClearAllErrors()
        {
            EmailBox.ClearValue(Border.BorderBrushProperty);
            EmailBox.ToolTip = null;
            UserBox.ClearValue(Border.BorderBrushProperty);
            UserBox.ToolTip = null;
            PasswordBox.ClearValue(Border.BorderBrushProperty);
            PasswordBox.ToolTip = null;
            RepeatBox.ClearValue(Border.BorderBrushProperty);
            RepeatBox.ToolTip = null;
        }

        private static bool IsValidEmail(string email)
        {
            bool isValid = false;
            try
            {
                var addr = new MailAddress(email);
                isValid = addr.Address == email;
            }
            catch
            {
                isValid = false;
            }
            return isValid;
        }

        private void GoToLogin(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new LoginPage());
        }

        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons();
            }
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
        }
    }
}