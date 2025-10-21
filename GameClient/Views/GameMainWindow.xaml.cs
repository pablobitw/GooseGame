using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.Views; 

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
 
        private string _username;

        
        private ChatWindow _chatWindowInstance;

        
        public GameMainWindow(string loggedInUsername)
        {
            InitializeComponent();

            _username = loggedInUsername;
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

        private void ChatButton_Click(object sender, RoutedEventArgs e)
        {
          
            if (_chatWindowInstance != null && _chatWindowInstance.IsVisible)
            {
            
                _chatWindowInstance.Activate();
            }
            else
            {
                
                _chatWindowInstance = new ChatWindow(_username);
                _chatWindowInstance.Show();
            }
        }

        public void ShowMainMenu()
        {
            MainFrame.Content = null;
            MainMenuGrid.Visibility = Visibility.Visible; 
        }
    }
}