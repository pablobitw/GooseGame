using System;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; 
using System.Windows.Navigation;
using GameClient.GameServiceReference; 
using GameClient.Views;

namespace GameClient.Views
{
  
    public partial class VerifyRecoveryCodePage : Page
    {
        private readonly string _userEmail; // Almacenará el email que se pasó desde la página anterior

        // Constructor para recibir el email
        public VerifyRecoveryCodePage(string email)
        {
            InitializeComponent();
            _userEmail = email; // guardamos el email para usarlo en las llamadas al servicio

        
        }

        // constructor por defecto (necesario para la navegación sin parametros, aunque no lo usaremos directamente)
        public VerifyRecoveryCodePage() : this(string.Empty) 
        {
         
        }

   
        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            // vuelve a la página anterior (ForgotPassPage)
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                NavigationService.Navigate(new LoginPage());
            }
        }

      
        private async void OnVerifyButtonClick(object sender, RoutedEventArgs e)
        {
            ClearAllErrors(); 

            string code = CodeTextBox.Text;

            
            if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !int.TryParse(code, out _))
            {
                ShowError(CodeTextBox, "El código debe ser de 6 dígitos numéricos.");
                return;
            }

            // llamar al servicio para verificar el código
            var client = new GameServiceClient(); 
            bool isCodeValid = false;
            try
            {
                isCodeValid = await client.VerifyRecoveryCodeAsync(_userEmail, code);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar con el servidor: {ex.Message}", "Error de Conexión");
                return;
            }

            if (isCodeValid)
            {
                MessageBox.Show("Código verificado exitosamente. Ahora puedes establecer una nueva contraseña.", "Éxito");
               
                NavigationService.Navigate(new ResetPasswordPage(_userEmail));
            }
            else
            {
                ShowError(CodeTextBox, "Código incorrecto o expirado. Por favor, inténtalo de nuevo.");
            }
        }

      
        private async void OnResendCodeButtonClick(object sender, RoutedEventArgs e)
        {
            ClearAllErrors();

            if (string.IsNullOrEmpty(_userEmail))
            {
                MessageBox.Show("No se encontró el email para reenviar el código.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var client = new GameServiceClient();
            bool requestSent = false;
            try
            {
                requestSent = await client.RequestPasswordResetAsync(_userEmail);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar con el servidor para reenviar el código: {ex.Message}", "Error de Conexión");
                return;
            }

            if (requestSent)
            {
                MessageBox.Show("Se ha enviado un nuevo código a tu correo electrónico.", "Código Reenviado");
            }
            else
            {
                // Esto podría significar que el email ya no existe o un error en el servidor.
                MessageBox.Show("No se pudo reenviar el código. Intenta solicitar la recuperación de nuevo.", "Error al Reenviar");
            }
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
            textBox.ClearValue(Border.BorderBrushProperty);
            textBox.ToolTip = null;
        }

        private void ShowError(Control field, string errorMessage)
        {
            field.BorderBrush = new SolidColorBrush(Colors.Red);
            field.ToolTip = new ToolTip { Content = errorMessage };
        }

        private void ClearAllErrors()
        {
            CodeTextBox.ClearValue(Border.BorderBrushProperty);
            CodeTextBox.ToolTip = null;
        }
    }
}