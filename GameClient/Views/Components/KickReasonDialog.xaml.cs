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
            _targetUsername = targetUsername;
            TargetLabel.Text = string.Format(GameClient.Resources.Strings.KickDialogTargetPrefix, targetUsername);
            KickReasonCombo.SelectedIndex = 0;
            this.Visibility = Visibility.Visible;
        }

        private void ConfirmKickButton_Click(object sender, RoutedEventArgs e)
        {
            string reason = (KickReasonCombo.SelectedItem as ComboBoxItem)?.Content.ToString()
                            ?? GameClient.Resources.Strings.KickReasonNone;

            this.Visibility = Visibility.Collapsed;

            KickConfirmed?.Invoke(this, new KickEventArgs
            {
                TargetUsername = _targetUsername,
                Reason = reason
            });
        }

        private void CancelKickButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            KickCancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}