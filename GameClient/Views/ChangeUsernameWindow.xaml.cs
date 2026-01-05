using GameClient.Helpers;
using GameClient.UserProfileServiceReference;
using System;
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

        private async Task SendVerificationCode()
        {
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
                SessionManager.ForceLogout("El servidor tardó demasiado en enviar el código.");
            }
            catch (EndpointNotFoundException)
            {
                SessionManager.ForceLogout("No se pudo conectar con el servidor.");
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Error de comunicación al solicitar el código.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                MessageBox.Show("Ocurrió un error inesperado al enviar el código.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            SaveButton.IsEnabled = false;
            var client = new UserProfileServiceClient();

            try
            {
                var result = await client.ChangeUsernameAsync(_userEmail, newName, code);

                switch (result)
                {
                    case UsernameChangeResult.Success:
                        MessageBox.Show(GameClient.Resources.Strings.UsernameSuccess,
                                        GameClient.Resources.Strings.DialogSuccessTitle,
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close();
                        break;

                    case UsernameChangeResult.UsernameAlreadyExists:
                        ShowError(UsernameBorder, UsernameErrorLabel, GameClient.Resources.Strings.UsernameExistsError);
                        break;

                    case UsernameChangeResult.LimitReached:
                        ShowError(UsernameBorder, UsernameErrorLabel, GameClient.Resources.Strings.UsernameLimitError);
                        break;

                    case UsernameChangeResult.UserNotFound:
                        SessionManager.ForceLogout("Usuario no encontrado. Sesión inválida.");
                        break;

                    case UsernameChangeResult.FatalError:
                        ShowError(CodeBorder, CodeErrorLabel, GameClient.Resources.Strings.CodeIncorrectError);
                        Step2_ChangeName.Visibility = Visibility.Collapsed;
                        Step1_VerifyCode.Visibility = Visibility.Visible;
                        break;

                    default:
                        ShowError(UsernameBorder, UsernameErrorLabel, "Error al actualizar. Intenta más tarde.");
                        break;
                }
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
            }
            catch (EndpointNotFoundException)
            {
                SessionManager.ForceLogout("No se pudo conectar con el servidor.");
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Se perdió la conexión al intentar guardar.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ShowError(UsernameBorder, UsernameErrorLabel, "Ocurrió un error inesperado.");
            }
            finally
            {
                CloseClient(client);
                SaveButton.IsEnabled = true;
            }
        }

        private async void ResendCode_Click(object sender, RoutedEventArgs e)
        {
            await SendVerificationCode();
            MessageBox.Show(GameClient.Resources.Strings.CodeSentInfo,
                            GameClient.Resources.Strings.DialogInfoTitle,
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // CORRECCIÓN: Se eliminó 'async' porque solo realiza operaciones síncronas de UI
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

        private void CloseClient(UserProfileServiceClient client)
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