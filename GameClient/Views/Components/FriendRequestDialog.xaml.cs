using System;
using System.ServiceModel; 
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
            await ProcessResponseAsync(true);
        }

        private async void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessResponseAsync(false);
        }

        private async System.Threading.Tasks.Task ProcessResponseAsync(bool accept)
        {
            this.Visibility = Visibility.Collapsed;

            try
            {
                if (FriendshipServiceManager.Instance != null)
                {
                    await FriendshipServiceManager.Instance.RespondToFriendRequestAsync(_requesterName, accept);
                }
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.FriendConnError,
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.SafeZone_ServerTimeout,
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error respondiendo solicitud: {ex.Message}");
            }
        }
    }
}