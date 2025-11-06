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
   
    public partial class LobbyPage : Page
    {
        public LobbyPage()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void DecreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void IncreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void VisibilityPublicButton_Click(object sender, RoutedEventArgs e)
        {
      
        }

        private void VisibilityPrivateButton_Click(object sender, RoutedEventArgs e)
        {
         
        }

        private void SendChatMessageButton_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void StartMatchButton_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}