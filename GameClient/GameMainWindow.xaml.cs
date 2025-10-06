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
    /// <summary>
    /// Lógica de interacción para GameMainWindow.xaml
    /// </summary>
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
    }
}
