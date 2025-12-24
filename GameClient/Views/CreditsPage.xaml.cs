using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class CreditsPage : Page
    {
        public CreditsPage()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Intentar obtener la ventana principal para mostrar el menú
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }
            else
            {
                // Fallback por si acaso
                NavigationService.GoBack();
            }
        }
    }
}