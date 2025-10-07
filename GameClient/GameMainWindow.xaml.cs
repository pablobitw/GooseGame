using GameClient.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
        public GameMainWindow()
        {
            InitializeComponent();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            PauseMenuGrid.Visibility = Visibility.Visible;
            MainMenuGrid.IsEnabled = false;
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            PauseMenuGrid.Visibility = Visibility.Collapsed;
            MainMenuGrid.IsEnabled = true;
        }

        private void OptionsButton_Click(Object sender, RoutedEventArgs e)
        {

        }

        private void QuitButton_Click(Object sender, RoutedEventArgs e)
        {

        }

        private void ProfileButton_Click(Object sender, RoutedEventArgs e)
        {
            PauseButton.Visibility = Visibility.Collapsed;
            ProfileButton.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new ModifyProfilePage());
        }

        public void ShowMainMenu()
        {
            MainFrame.Content = null;
            PauseButton.Visibility = Visibility.Visible;
            ProfileButton.Visibility = Visibility.Visible;
        }
    }
}
