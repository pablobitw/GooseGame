using GameClient.Helpers;
using GameClient.UserProfileServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Views
{
    public partial class ChangeUsernameWindow : Window
    {
        private readonly string _userEmail;

        public ChangeUsernameWindow(string email)
        {
            InitializeComponent();
            _userEmail = email;

            Step1_VerifyCode.Visibility = Visibility.Visible;
            Step2_ChangeName.Visibility = Visibility.Collapsed;
            VerifyCodeButton.Visibility = Visibility.Visible;

            this.Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await SendVerificationCode();
        }

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, icon);
        }

        private async Task SendVerificationCode()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("ChangeUser_Error_NoInternet", "ChangeUser_Title_Error", MessageBoxImage.Error);
                return;
            }

            var client = new UserProfileServiceClient();

            try
            {
                bool sent = await client.SendUsernameChangeCodeAsync(_userEmail);

                if (!sent)
                {
                    MessageBox.Show(GameClient.Resources.Strings.ErrorTitle,
                                    GameClient.Resources.Strings.DialogWarningTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_Timeout", "ChangeUser_Title_Error", MessageBoxImage.Warning);
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_ServerDown", "ChangeUser_Title_Error", MessageBoxImage.Error);
            }
            catch (FaultException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_Database", "ChangeUser_Title_Error", MessageBoxImage.Error);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_Communication", "ChangeUser_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string generalError = GameClient.Resources.Strings.ChangeUser_Error_General;
                string title = GameClient.Resources.Strings.ChangeUser_Title_Error;
                MessageBox.Show($"{generalError}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseClient(client);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();
            string newName = NewUsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                ShowError(CodeBorder, CodeErrorLabel, GameClient.Resources.Strings.CodeLengthError);
                return;
            }

            if (string.IsNullOrEmpty(newName) || newName.Length < 3)
            {
                ShowError(UsernameBorder, UsernameErrorLabel, GameClient.Resources.Strings.UsernameMinLengthError);
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("ChangeUser_Error_NoInternet", "ChangeUser_Title_Error", MessageBoxImage.Error);
                return;
            }

            SaveButton.IsEnabled = false;
            var client = new UserProfileServiceClient();

            try
            {
                var result = await client.ChangeUsernameAsync(_userEmail, newName, code);
                HandleChangeResult(result);
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_Timeout", "ChangeUser_Title_Error", MessageBoxImage.Warning);
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_ServerDown", "ChangeUser_Title_Error", MessageBoxImage.Error);
            }
            catch (FaultException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_Database", "ChangeUser_Title_Error", MessageBoxImage.Error);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("ChangeUser_Error_Communication", "ChangeUser_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string generalError = GameClient.Resources.Strings.ChangeUser_Error_General;
                string title = GameClient.Resources.Strings.ChangeUser_Title_Error;
                MessageBox.Show($"{generalError}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseClient(client);
                SaveButton.IsEnabled = true;
            }
        }

        private void HandleChangeResult(UsernameChangeResult result)
        {
            switch (result)
            {
                case UsernameChangeResult.Success:
                    ShowTranslatedMessageBox("ChangeUser_Success_Msg", "ChangeUser_Title_Success", MessageBoxImage.Information);
                    this.Close();
                    break;

                case UsernameChangeResult.UsernameAlreadyExists:
                    ShowError(UsernameBorder, UsernameErrorLabel, GameClient.Resources.Strings.ChangeUser_Error_Exists);
                    break;

                case UsernameChangeResult.LimitReached:
                    ShowError(UsernameBorder, UsernameErrorLabel, GameClient.Resources.Strings.ChangeUser_Error_Limit);
                    break;

                case UsernameChangeResult.UserNotFound:
                    SessionManager.ForceLogout("Usuario no encontrado. Sesión inválida.");
                    break;

                case UsernameChangeResult.FatalError:
                    ShowError(CodeBorder, CodeErrorLabel, GameClient.Resources.Strings.ChangeUser_Error_Code);
                    Step2_ChangeName.Visibility = Visibility.Collapsed;
                    Step1_VerifyCode.Visibility = Visibility.Visible;
                    break;

                default:
                    ShowError(UsernameBorder, UsernameErrorLabel, GameClient.Resources.Strings.ChangeUser_Error_General);
                    break;
            }
        }

        private async void ResendCode_Click(object sender, RoutedEventArgs e)
        {
            await SendVerificationCode();
            ShowTranslatedMessageBox("ChangeUser_Info_CodeSent", "DialogInfoTitle", MessageBoxImage.Information);
        }

        private void VerifyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();
            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                ShowError(CodeBorder, CodeErrorLabel, GameClient.Resources.Strings.CodeLengthError);
                return;
            }

            Step1_VerifyCode.Visibility = Visibility.Collapsed;
            Step2_ChangeName.Visibility = Visibility.Visible;
        }

        private static void CloseClient(UserProfileServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened)
                    client.Close();
                else
                    client.Abort();
            }
            catch (Exception)
            {
                client.Abort();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private static void ShowError(Border border, TextBlock label, string message)
        {
            border.BorderBrush = new SolidColorBrush(Colors.Red);
            border.BorderThickness = new Thickness(2);
            label.Text = message;
            label.Visibility = Visibility.Visible;
        }

        private static void ClearError(Border border, TextBlock label)
        {
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            border.BorderThickness = new Thickness(1);
            if (label != null) label.Visibility = Visibility.Collapsed;
        }

        private void OnCodeTextChanged(object sender, TextChangedEventArgs e)
        {
            if (CodePlaceholder != null)
            {
                CodePlaceholder.Visibility = string.IsNullOrWhiteSpace(CodeTextBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            ClearError(CodeBorder, CodeErrorLabel);
        }

        private void OnUsernameTextChanged(object sender, TextChangedEventArgs e)
        {
            if (UsernamePlaceholder != null)
            {
                UsernamePlaceholder.Visibility = string.IsNullOrWhiteSpace(NewUsernameTextBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            ClearError(UsernameBorder, UsernameErrorLabel);
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorPastingDisabled,
                                GameClient.Resources.Strings.DialogActionBlockedTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}