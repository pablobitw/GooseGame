using GameClient.UserProfileServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views.Components
{
    public partial class DeactivateAccountDialog : UserControl
    {
        public event EventHandler AccountDeactivated;
        public event EventHandler DialogClosed;

        public string CurrentUserEmail { get; set; }

        public DeactivateAccountDialog()
        {
            InitializeComponent();
        }

        public void ResetFields()
        {
            PbDeactivate1.Password = string.Empty;
            PbDeactivate2.Password = string.Empty;
            ConfirmDeactivateButton.IsEnabled = true;

            PbDeactivate1.ClearValue(Border.BorderBrushProperty);
            PbDeactivate2.ClearValue(Border.BorderBrushProperty);
        }

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, icon);
        }

        private void CancelDeactivate_Click(object sender, RoutedEventArgs e)
        {
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        private async void ConfirmDeactivate_Click(object sender, RoutedEventArgs e)
        {
            string pass1 = PbDeactivate1.Password;
            string pass2 = PbDeactivate2.Password;

            if (string.IsNullOrWhiteSpace(pass1) || string.IsNullOrWhiteSpace(pass2))
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorPasswordsEmpty,
                                GameClient.Resources.Strings.DialogWarningTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pass1 != pass2)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorPasswordsMismatch,
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Deactivate_Error_NoInternet", "DialogErrorTitle", MessageBoxImage.Error);
                return;
            }

            ConfirmDeactivateButton.IsEnabled = false;
            var client = new UserProfileServiceClient();

            try
            {
                var request = new DeactivateAccountRequest
                {
                    Username = CurrentUserEmail,
                    Password = pass1
                };

                bool success = await client.DeactivateAccountAsync(request);

                if (success)
                {
                    AccountDeactivated?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    MessageBox.Show(GameClient.Resources.Strings.ErrorDeactivationFailed,
                                    GameClient.Resources.Strings.DialogErrorTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    ConfirmDeactivateButton.IsEnabled = true;
                }
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Deactivate_Error_Timeout", "DialogErrorTitle", MessageBoxImage.Warning);
                ConfirmDeactivateButton.IsEnabled = true;
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Deactivate_Error_ServerDown", "DialogErrorTitle", MessageBoxImage.Error);
                ConfirmDeactivateButton.IsEnabled = true;
            }
            catch (FaultException)
            {
                ShowTranslatedMessageBox("Deactivate_Error_Database", "DialogErrorTitle", MessageBoxImage.Error);
                ConfirmDeactivateButton.IsEnabled = true;
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Deactivate_Error_Communication", "DialogErrorTitle", MessageBoxImage.Error);
                ConfirmDeactivateButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                string general = GameClient.Resources.Strings.Deactivate_Error_General;
                MessageBox.Show($"{general}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ConfirmDeactivateButton.IsEnabled = true;
            }
            finally
            {
                CloseClientSafely(client);
            }
        }

        private static void CloseClientSafely(UserProfileServiceClient client)
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
            catch (Exception)
            {
                client.Abort();
            }
        }

        private void PasswordBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorPastingDisabled,
                                GameClient.Resources.Strings.DialogWarningTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}