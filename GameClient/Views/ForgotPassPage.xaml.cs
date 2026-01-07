using GameClient.AuthServiceReference;
using GameClient.Views;
using System;
using System.Net.Mail;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GameClient
{
    public partial class ForgotPassPage : Page
    {
        public ForgotPassPage()
        {
            InitializeComponent();
        }

        private async void OnSendButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid()) return;

            string email = EmailTextBox.Text;
            var client = new AuthServiceClient();
            bool requestSent = false;

            try
            {
                SendButton.IsEnabled = false;
                requestSent = await client.RequestPasswordResetAsync(email);

                if (requestSent)
                {
                    MessageBox.Show(GameClient.Resources.Strings.Forgot_Success_Msg,
                                    GameClient.Resources.Strings.Forgot_Success_Title,
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                    NavigationService.Navigate(new VerifyRecoveryCodePage(email));
                }
                else
                {
                    ShowError(EmailTextBox, GameClient.Resources.Strings.Forgot_Error_Process);
                }
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Auth_Error_ServerDown", "Auth_Title_Error", MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Auth_Error_Timeout", "Auth_Title_Error", MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Auth_Error_Communication", "Auth_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string generalError = GameClient.Resources.Strings.Auth_Error_General;
                MessageBox.Show($"{generalError}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseServiceClient(client);
                SendButton.IsEnabled = true;
            }
        }

        private void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, icon);
        }

        private static void CloseServiceClient(AuthServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened) client.Close();
                else client.Abort();
            }
            catch { client.Abort(); }
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new LoginPage());
        }

        private void OnGenericTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TextBlock placeholder)
            {
                placeholder.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            string email = EmailTextBox.Text;
            string repeatEmail = RepeatEmailTextBox.Text;

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError(EmailTextBox, GameClient.Resources.Strings.Forgot_Error_Empty);
                isValid = false;
            }
            else if (!IsValidEmail(email))
            {
                ShowError(EmailTextBox, GameClient.Resources.Strings.Forgot_Error_Format);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(repeatEmail))
            {
                ShowError(RepeatEmailTextBox, GameClient.Resources.Strings.Forgot_Error_Empty);
                isValid = false;
            }
            else if (email != repeatEmail)
            {
                ShowError(EmailTextBox, GameClient.Resources.Strings.Forgot_Error_Mismatch);
                ShowError(RepeatEmailTextBox, GameClient.Resources.Strings.Forgot_Error_Mismatch);
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
            EmailTextBox.ClearValue(Border.BorderBrushProperty);
            EmailTextBox.ToolTip = null;
            RepeatEmailTextBox.ClearValue(Border.BorderBrushProperty);
            RepeatEmailTextBox.ToolTip = null;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch { return false; }
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            MessageBox.Show(GameClient.Resources.Strings.Forgot_Paste_Blocked,
                            GameClient.Resources.Strings.Forgot_Title_Blocked,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}