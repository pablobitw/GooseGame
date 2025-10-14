using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.Views; 

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
        public GameMainWindow()
        {
            InitializeComponent();
        }


        private void PlayButtonClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lógica para buscar partida aquí...", "Función no implementada");
        }

        private void OptionsButtonClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Aquí se abriría la página de opciones.", "Función no implementada");
        }

        private void QuitButtonClick(object sender, RoutedEventArgs e)
        {
            string confirmationMessage = GameClient.Resources.Strings.ConfirmExitLabel;
            string yesButtonText = GameClient.Resources.Strings.YesLabel;
            string noButtonText = GameClient.Resources.Strings.NoLabel;

            var confirmationDialog = new CustomMessageBox(confirmationMessage, yesButtonText, noButtonText);

            bool? result = confirmationDialog.ShowDialog();
            if (result == true)
            {
                Application.Current.Shutdown();

            }
        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new ModifyProfilePage());
        }


        private void PauseButtonClick(object sender, RoutedEventArgs e)
        {
            PauseMenuGrid.Visibility = Visibility.Visible;
            MainMenuGrid.IsEnabled = false;
        }

        private void ResumeButtonClick(object sender, RoutedEventArgs e)
        {
            PauseMenuGrid.Visibility = Visibility.Collapsed;
            MainMenuGrid.IsEnabled = true;
        }


        public void ShowMainMenu()
        {
            MainFrame.Content = null; // Limpia el Frame.
            MainMenuGrid.Visibility = Visibility.Visible; // Muestra el menú principal de nuevo.
        }
    }
}