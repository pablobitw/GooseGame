using GameClient.AuthServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class ResetPasswordPage : Page
    {
        private string _username;
        private Action _onConfirmAction;

        public ResetPasswordPage(string username)
        {
            InitializeComponent();
            this._username = username;
        }

        public ResetPasswordPage() : this(string.Empty)
        {
        }

        private void ShowCustomDialog(string title, string message, FontAwesome.WPF.FontAwesomeIcon icon, bool isConfirmation = false, Action onConfirm = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Icon = icon;
            CancelBtn.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
            CancelColumn.Width = isConfirmation ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            ConfirmBtn.Content = isConfirmation ? GameClient.Resources.Strings.DialogConfirmBtn : GameClient.Resources.Strings.DialogOkBtn;
            CancelBtn.Content = GameClient.Resources.Strings.DialogCancelBtn;
            _onConfirmAction = onConfirm;
            DialogOverlay.Visibility = Visibility.Visible;
        }


        private void ShowTranslatedError(string messageKey, string titleKey, FontAwesome.WPF.FontAwesomeIcon icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            ShowCustomDialog(title ?? titleKey, message ?? messageKey, icon);
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            _onConfirmAction?.Invoke();
            _onConfirmAction = null;
        }

        private async void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid())
            {
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedError("ResetPass_Error_NoInternet", "ResetPass_Title_Error", FontAwesome.WPF.FontAwesomeIcon.Wifi);
                return;
            }

            string currentPassword = CurrentPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;

            var client = new AuthServiceClient();

            try
            {
                bool updateSuccess = await client.ChangeUserPasswordAsync(_username, currentPassword, newPassword);

                if (updateSuccess)
                {
                    string successMsg = GameClient.Resources.Strings.ResetPass_Success_Msg;
                    string successTitle = GameClient.Resources.Strings.ResetPass_Title_Success;

                    ShowCustomDialog(successTitle, successMsg, FontAwesome.WPF.FontAwesomeIcon.CheckCircle, false, () =>
                    {
                        if (NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    });
                }
                else
                {
                    ShowError(CurrentPasswordBox, GameClient.Resources.Strings.ResetPass_Error_CurrentIncorrect);
                }
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedError("ResetPass_Error_ServerDown", "ResetPass_Title_Error", FontAwesome.WPF.FontAwesomeIcon.ExclamationTriangle);
            }
            catch (TimeoutException)
            {
                ShowTranslatedError("ResetPass_Error_Timeout", "ResetPass_Title_Error", FontAwesome.WPF.FontAwesomeIcon.ClockOutline);
            }
            catch (FaultException)
            {
                ShowTranslatedError("ResetPass_Error_Database", "ResetPass_Title_Error", FontAwesome.WPF.FontAwesomeIcon.Database);
            }
            catch (CommunicationException)
            {
                ShowTranslatedError("ResetPass_Error_Communication", "ResetPass_Title_Error", FontAwesome.WPF.FontAwesomeIcon.Wifi);
            }
            catch (Exception ex)
            {
                string generalError = GameClient.Resources.Strings.ResetPass_Error_General;
                string title = GameClient.Resources.Strings.ResetPass_Title_Error;
                ShowCustomDialog(title, $"{generalError}\n{ex.Message}", FontAwesome.WPF.FontAwesomeIcon.TimesCircle);
            }
            finally
            {
                CloseClientSafely(client);
            }
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
            catch (Exception)
            {
                client.Abort();
            }
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private static bool IsPasswordStrong(string password)
        {
            var regex = new Regex(@"^(?=.*[0-9])(?=.*[!@#$%^&*()_+={}\[\]:;<>,.?/~`|-]).{8,}$");
            return regex.IsMatch(password);
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            string currentPass = CurrentPasswordBox.Password;
            string newPass = NewPasswordBox.Password;
            string repeatPass = RepeatNewPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPass))
            {
                ShowError(CurrentPasswordBox, GameClient.Resources.Strings.ErrorCurrentPassRequired);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(newPass))
            {
                ShowError(NewPasswordBox, GameClient.Resources.Strings.ErrorNewPassEmpty);
                isValid = false;
            }
            else if (!IsPasswordStrong(newPass))
            {
                ShowError(NewPasswordBox, GameClient.Resources.Strings.ErrorPassTooWeak);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(repeatPass))
            {
                ShowError(RepeatNewPasswordBox, GameClient.Resources.Strings.ErrorCurrentPassRequired);
                isValid = false;
            }
            else if (newPass != repeatPass)
            {
                ShowError(NewPasswordBox, GameClient.Resources.Strings.ErrorPassMismatch);
                ShowError(RepeatNewPasswordBox, GameClient.Resources.Strings.ErrorPassMismatch);
                isValid = false;
            }

            if (isValid && currentPass == newPass)
            {
                ShowError(NewPasswordBox, GameClient.Resources.Strings.ErrorSamePass);
                isValid = false;
            }

            return isValid;
        }

        private void ShowError(Control field, string errorMessage)
        {
            field.BorderBrush = new SolidColorBrush(Colors.Red);
            field.BorderThickness = new Thickness(2);
            field.ToolTip = new ToolTip { Content = errorMessage };
        }

        private void ClearAllErrors()
        {
            CurrentPasswordBox.ClearValue(Border.BorderBrushProperty);
            CurrentPasswordBox.ToolTip = null;

            NewPasswordBox.ClearValue(Border.BorderBrushProperty);
            NewPasswordBox.ToolTip = null;

            RepeatNewPasswordBox.ClearValue(Border.BorderBrushProperty);
            RepeatNewPasswordBox.ToolTip = null;
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

        private void OnPasswordBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            ShowCustomDialog(GameClient.Resources.Strings.DialogActionBlockedTitle,
                             GameClient.Resources.Strings.ErrorPastingDisabled,
                             FontAwesome.WPF.FontAwesomeIcon.Lock);
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
        }
    }
}