using System;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views.Components
{
    public partial class GameAlertBox : UserControl
    {
        public event EventHandler AlertClosed;

        public GameAlertBox()
        {
            InitializeComponent();
            this.Visibility = Visibility.Collapsed;
        }

  
        public void Show(string title, string message)
        {
            TitleText.Text = title;
            MessageText.Text = message;
            this.Visibility = Visibility.Visible;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;

            AlertClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}