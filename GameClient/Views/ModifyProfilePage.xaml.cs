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
    public partial class ModifyProfilePage : Page
    {
        public ModifyProfilePage()
        {
            InitializeComponent();
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelButton_Click(Object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow gameMenu)
            {
                gameMenu.ShowMainMenu();
            }
        }
    }
}
