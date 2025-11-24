using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.UserProfileServiceReference;
using GameClient; // Necesario para ver GameMainWindow

namespace GameClient.Views
{
    public partial class UserProfilePage : Page
    {
        private readonly string userEmail;
        private string currentUsername;

        public UserProfilePage(string email)
        {
            InitializeComponent();
            userEmail = email;
            LoadUserProfile();
        }

        public UserProfilePage() : this("dev@test.com")
        {
        }

        private void LoadUserProfile()
        {
            if (EmailTextBox != null)
            {
                EmailTextBox.Text = userEmail;
            }
        }

        private async void SaveUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            string newUsername = UsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(newUsername))
            {
                MessageBox.Show("El nombre de usuario no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newUsername.Length < 3)
            {
                MessageBox.Show("El nombre es muy corto (mínimo 3 caracteres).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var client = new UserProfileServiceClient();
            bool connectionError = false;
            string errorMessage = string.Empty;

            try
            {
                var result = await client.ChangeUsernameAsync(userEmail, newUsername);

                switch (result)
                {
                    case UsernameChangeResult.Success:
                        MessageBox.Show("¡Nombre de usuario actualizado correctamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        currentUsername = newUsername;
                        break;

                    case UsernameChangeResult.UsernameAlreadyExists:
                        MessageBox.Show($"El nombre '{newUsername}' ya está en uso. Elige otro.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;

                    case UsernameChangeResult.LimitReached:
                        MessageBox.Show("Has alcanzado el límite de cambios de nombre permitidos (3).", "Límite Alcanzado", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;

                    case UsernameChangeResult.CooldownActive:
                        MessageBox.Show("No puedes cambiar tu nombre aún. Intenta más tarde.", "Espera", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;

                    case UsernameChangeResult.UserNotFound:
                        MessageBox.Show("Error crítico: Usuario no encontrado.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case UsernameChangeResult.FatalError:
                        MessageBox.Show("Ocurrió un error interno en el servidor.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (EndpointNotFoundException)
            {
                errorMessage = "No se pudo conectar al servidor. Verifica tu conexión.";
                connectionError = true;
            }
            catch (TimeoutException)
            {
                errorMessage = "El servidor tardó demasiado en responder.";
                connectionError = true;
            }
            catch (CommunicationException)
            {
                errorMessage = "Error de comunicación con el servidor.";
                connectionError = true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error inesperado: {ex.Message}";
                connectionError = true;
            }
            finally
            {
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
            }

            if (connectionError)
            {
                MessageBox.Show(errorMessage, "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funcionalidad de Selección de Avatar pendiente de implementación.", "Info");
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            // NavigationService.Navigate(new ChangePasswordPage(userEmail));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                mainWindow.ShowMainMenu();
            }
        }
    }
}