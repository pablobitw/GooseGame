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
                string questionFormat = GameClient.Resources.Strings.VoteKickQuestion
                                        ?? GameClient.Resources.Strings.VoteKick_Fallback_Question;

                string reasonFormat = GameClient.Resources.Strings.VoteKickReasonPrefix
                                      ?? GameClient.Resources.Strings.VoteKick_Fallback_Reason;

                VoteKickTargetText.Text = string.Format(questionFormat, targetUsername);
                VoteReasonText.Text = string.Format(reasonFormat, reason);

                this.Visibility = Visibility.Visible;
            }
            catch (FormatException)
            {
                string safeQuestion = GameClient.Resources.Strings.VoteKick_Fallback_Question ?? "Kick {0}?";
                string safeReason = GameClient.Resources.Strings.VoteKick_Fallback_Reason ?? "Reason: {0}";

                VoteKickTargetText.Text = safeQuestion.Replace("{0}", targetUsername);
                VoteReasonText.Text = safeReason.Replace("{0}", reason);

                this.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                this.Visibility = Visibility.Collapsed;
                MessageBox.Show(GameClient.Resources.Strings.Error_UI_Generic + ": " + ex.Message,
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
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
            try
            {
                this.Visibility = Visibility.Collapsed;
                VoteSubmitted?.Invoke(this, accept);
            }
            catch (Exception)
            {
                this.Visibility = Visibility.Collapsed;
            }
        }
    }
}