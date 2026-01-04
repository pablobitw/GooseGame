using System.Windows;
using System.Windows.Controls;
using GameClient.Helpers;

namespace GameClient.Views.Dialogs
{
    public partial class FriendRequestDialog : UserControl
    {
        private string _requesterName;

        public FriendRequestDialog()
        {
            InitializeComponent();
        }

        public void ShowRequest(string senderName)
        {
            _requesterName = senderName;
            SenderNameText.Text = senderName;
            this.Visibility = Visibility.Visible;
        }

        private async void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            if (FriendshipServiceManager.Instance != null)
            {
                await FriendshipServiceManager.Instance.RespondToFriendRequestAsync(_requesterName, true);
            }
        }

        private async void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            if (FriendshipServiceManager.Instance != null)
            {
                await FriendshipServiceManager.Instance.RespondToFriendRequestAsync(_requesterName, false);
            }
        }
    }
}