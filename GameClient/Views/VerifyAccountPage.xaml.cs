using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.AuthServiceReference;
using System.ServiceModel;
using GameClient.Views;
using System.Windows.Threading;
using System.Net.NetworkInformation;

namespace GameClient
{
    public partial class VerifyAccountPage : Page
    {
        private string userEmail;
        private const int CodeLength = 6;

        public VerifyAccountPage(string email)
        {
            InitializeComponent();
            userEmail = email;
        }

        public VerifyAccountPage()
        {
            InitializeComponent();
        }

        private async void VerifyButton(object sender, RoutedEventArgs e)
        {
            string codeTyped = CodeTextBox.Text.Trim();

            if (!IsCodeValid(codeTyped))
            {
                ShowTranslatedMessageBox("Verify_Error_Format", "Verify_Title_Error", MessageBoxImage.Warning);
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Verify_Error_NoInternet", "Verify_Title_Error", MessageBoxImage.Warning);
                return;
            }

            var client = new AuthServiceClient();
            bool verificationResult = false;

            try
            {
                verificationResult = await client.VerifyAccountAsync(userEmail, codeTyped);

                if (verificationResult)
                {
                    ShowTranslatedMessageBox("Verify_Success_Msg", "Verify_Success_Title", MessageBoxImage.Information);
                    NavigationService.Navigate(new LoginPage());
                }
                else
                {
                    ShowTranslatedMessageBox("Verify_Error_Invalid", "Verify_Title_Failed", MessageBoxImage.Warning);
                }
            }
            catch (FaultException<ServiceFault> fault)
            {
                var resManager = GameClient.Resources.Strings.ResourceManager;

                string contextMsg = resManager.GetString("Verify_Context_Error") ?? "Error durante la verificación.";
                string title = resManager.GetString("Verify_Title_Error") ?? "Error";

                string technicalReason = resManager.GetString(fault.Detail.Code);

                if (string.IsNullOrEmpty(technicalReason))
                {
                    technicalReason = fault.Detail.Message ?? "Error del servidor.";
                }

                MessageBox.Show($"{contextMsg}\n\nDetalle: {technicalReason}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Global_Error_ServerDown", "Verify_Title_Error", MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Global_Error_Timeout", "Verify_Title_Error", MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Global_Error_Communication", "Verify_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string generalError = GameClient.Resources.Strings.Global_Error_Unknown;
                string title = GameClient.Resources.Strings.Verify_Title_Error;
                MessageBox.Show($"{generalError}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseClientSafely(client);
            }
        }

        private static bool IsCodeValid(string code)
        {
            return !string.IsNullOrEmpty(code) && code.Length == CodeLength && int.TryParse(code, out _);
        }

        private static void CloseClientSafely(AuthServiceClient client)
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
            catch
            {
                client.Abort();
            }
        }

        private async void ResendCodeButton(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Verify_Error_NoInternet", "Verify_Title_Error", MessageBoxImage.Warning);
                if (button != null) button.IsEnabled = true;
                return;
            }

            var client = new AuthServiceClient();
            bool requestSent = false;

            try
            {
                requestSent = await client.ResendVerificationCodeAsync(userEmail);

                if (requestSent)
                {
                    string msgFormat = GameClient.Resources.Strings.Resend_Success_Msg;
                    string msg = string.Format(msgFormat ?? "Código reenviado a {0}", userEmail);
                    string title = GameClient.Resources.Strings.Resend_Success_Title;

                    MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ShowTranslatedMessageBox("Resend_Error_Failed", "Verify_Title_Error", MessageBoxImage.Warning);
                }
            }
            catch (FaultException<ServiceFault> fault)
            {
                var resManager = GameClient.Resources.Strings.ResourceManager;
                string contextMsg = resManager.GetString("Resend_Context_Error") ?? "Error al reenviar código.";
                string title = resManager.GetString("Verify_Title_Error") ?? "Error";
                string technicalReason = resManager.GetString(fault.Detail.Code);

                if (string.IsNullOrEmpty(technicalReason))
                {
                    technicalReason = fault.Detail.Message ?? "Error del servidor.";
                }

                MessageBox.Show($"{contextMsg} {technicalReason}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Global_Error_ServerDown", "Verify_Title_Error", MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Global_Error_Timeout", "Verify_Title_Error", MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Global_Error_Communication", "Verify_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string generalError = GameClient.Resources.Strings.Global_Error_Unknown;
                string title = GameClient.Resources.Strings.Verify_Title_Error;
                MessageBox.Show($"{generalError}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseClientSafely(client);
                if (button != null)
                {
                    button.IsEnabled = true;
                }
            }
        }

        private void BackButton(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new LoginPage());
        }

        private void OnCodeTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                var raw = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
                e.CancelCommand();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var sanitized = new string(raw.Where(char.IsDigit).ToArray());
                    if (sanitized.Length > CodeTextBox.MaxLength)
                    {
                        sanitized = sanitized.Substring(0, CodeTextBox.MaxLength);
                    }

                    CodeTextBox.Text = sanitized;
                    CodeTextBox.CaretIndex = CodeTextBox.Text.Length;
                }), DispatcherPriority.Background);
            }
            else
            {
                e.CancelCommand();
            }
        }

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, icon);
        }
    }
}