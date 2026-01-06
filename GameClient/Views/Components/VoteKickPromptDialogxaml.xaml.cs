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
            try
            {

                string questionFormat = GameClient.Resources.Strings.VoteKickQuestion ?? "¿Votar para expulsar a {0}?";
                string reasonFormat = GameClient.Resources.Strings.VoteKickReasonPrefix ?? "Motivo: {0}";

                VoteKickTargetText.Text = string.Format(questionFormat, targetUsername);
                VoteReasonText.Text = string.Format(reasonFormat, reason);

                this.Visibility = Visibility.Visible;
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"[VoteKickDialog] Format Error: {ex.Message}");
                VoteKickTargetText.Text = $"Kick {targetUsername}?";
                VoteReasonText.Text = reason;
                this.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VoteKickDialog] UI Error: {ex.Message}");
                this.Visibility = Visibility.Collapsed;
            }
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