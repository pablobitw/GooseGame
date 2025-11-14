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

namespace GameClient.Views
{

    public partial class CreateOrJoinMatchPage : Page
    {
        private string _username;

        public CreateOrJoinMatchPage(string username)
        {
            InitializeComponent();
            _username = username;
        }

        private void CreateMatchButton_Click(object sender, RoutedEventArgs e)
        {
            // CAMBIO: Pasa el _username a la siguiente página
            NavigationService.Navigate(new LobbyPage(_username));
        }

        private void JoinMatchButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ViewMatchesButton_Click(object sender, RoutedEventArgs e)
        {

        }


        private void BackButton_Click(object sender, RoutedEventArgs e)
        {

            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}