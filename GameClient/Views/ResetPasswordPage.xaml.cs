﻿using GameClient.GameServiceReference;
using GameClient.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class ResetPasswordPage : Page
    {
        private string _userEmail;

        public ResetPasswordPage(string email)
        {
            InitializeComponent();
            _userEmail = email;
        }

        public ResetPasswordPage() : this(string.Empty) { }

        private async void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid())
            {
                return;
            }

          
            string newPassword = NewPasswordBox.Password;

            var client = new GameServiceClient();
            bool updateSuccess = false;
            try
            {
                updateSuccess = await client.UpdatePasswordAsync(_userEmail, newPassword);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error conectando al servidor: {ex.Message}", "Error");
                return;
            }

            if (updateSuccess)
            {
                MessageBox.Show("¡Contraseña actualizada con éxito! Ya puedes iniciar sesión.", "Éxito");
                NavigationService.Navigate(new LoginPage());
            }
            else
            {
               
                ShowError(NewPasswordBox, "Error: No puedes usar tu contraseña anterior.");
                ShowError(RepeatNewPasswordBox, "Error: No puedes usar tu contraseña anterior.");
            }
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new LoginPage());
            }
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            
            string newPass = NewPasswordBox.Password;
            string repeatPass = RepeatNewPasswordBox.Password; 

            
            if (string.IsNullOrWhiteSpace(newPass))
            {
                ShowError(NewPasswordBox, "La contraseña no puede estar vacía.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(repeatPass))
            {
                ShowError(RepeatNewPasswordBox, "Debes repetir la contraseña.");
                isValid = false;
            }
            else if (newPass != repeatPass)
            {
                ShowError(NewPasswordBox, "Las contraseñas no coinciden.");
                ShowError(RepeatNewPasswordBox, "Las contraseñas no coinciden.");
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
            if (placeholder != null)
            {
                if (string.IsNullOrWhiteSpace(passwordBox.Password))
                {
                    placeholder.Visibility = Visibility.Visible;
                }
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
        }
    }
}