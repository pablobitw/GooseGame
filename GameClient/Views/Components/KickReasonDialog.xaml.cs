using System;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views.Dialogs
{
    public class KickEventArgs : EventArgs
    {
        public string TargetUsername { get; set; }
        public string Reason { get; set; }
    }

    public partial class KickReasonDialog : UserControl
    {
        public event EventHandler<KickEventArgs> KickConfirmed;
        public event EventHandler KickCancelled;

        private string _targetUsername;

        public KickReasonDialog()
        {
            InitializeComponent();
        }

        public void Show(string targetUsername)
        {
            try
            {
                _targetUsername = targetUsername;
                TargetLabel.Text = string.Format(GameClient.Resources.Strings.KickDialogTargetPrefix, targetUsername);
                if (KickReasonCombo.Items.Count > 0)
                {
                    KickReasonCombo.SelectedIndex = 0;
                }
                this.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                this.Visibility = Visibility.Collapsed;
            }
        }

        private void ConfirmKickButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string reason = GameClient.Resources.Strings.KickReasonNone;

                if (KickReasonCombo.SelectedItem is ComboBoxItem item && item.Content != null)
                {
                    reason = item.Content.ToString();
                }
                else if (KickReasonCombo.SelectedItem != null)
                {
                    reason = KickReasonCombo.SelectedItem.ToString();
                }

                this.Visibility = Visibility.Collapsed;

                KickConfirmed?.Invoke(this, new KickEventArgs
                {
                    TargetUsername = _targetUsername,
                    Reason = reason
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(GameClient.Resources.Strings.Error_UI_Generic + ": " + ex.Message,
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
                this.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelKickButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Visibility = Visibility.Collapsed;
                KickCancelled?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                this.Visibility = Visibility.Collapsed;
            }
        }
    }
}