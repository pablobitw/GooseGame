using System;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views.Dialogs
{
    public partial class VoteKickPromptDialog : UserControl
    {
        public event EventHandler<bool> VoteSubmitted;

        public VoteKickPromptDialog()
        {
            InitializeComponent();
        }

        public void ShowVote(string targetUsername, string reason)
        {
            VoteKickTargetText.Text = $"¿Expulsar a '{targetUsername}'?";
            VoteReasonText.Text = $"Motivo: {reason}";
            this.Visibility = Visibility.Visible;
        }

        private void VoteYes_Click(object sender, RoutedEventArgs e)
        {
            SubmitVote(true);
        }

        private void VoteNo_Click(object sender, RoutedEventArgs e)
        {
            SubmitVote(false);
        }

        public void SubmitVote(bool accept)
        {
            this.Visibility = Visibility.Collapsed;
            VoteSubmitted?.Invoke(this, accept);
        }
    }
}