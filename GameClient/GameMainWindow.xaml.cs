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