using GameClient.UserProfileServiceReference;
using System;
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
                MessageBox.Show("Por favor ingresa tu contraseña en ambos campos.", "Campos requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pass1 != pass2)
            {
                MessageBox.Show("Las contraseñas no coinciden.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("La contraseña es incorrecta o no se pudo desactivar la cuenta.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ConfirmDeactivateButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ConfirmDeactivateButton.IsEnabled = true;
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
                else client.Abort();
            }
        }

        private void PasswordBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("Por seguridad, el pegado está deshabilitado en campos de contraseña.",
                                "Acción bloqueada",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
