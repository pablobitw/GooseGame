using System;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views.Components
{
    public partial class AddSocialLinkDialog : UserControl
    {
        public event EventHandler DialogClosed;
        public event EventHandler<string> LinkAdded;

        public AddSocialLinkDialog()
        {
            InitializeComponent();
        }

        public void Reset()
        {
            UrlTextBox.Text = string.Empty;
            SocialTypeComboBox.SelectedIndex = -1;
            PrefixTextBlock.Text = "https://...";
            HintText.Text = GameClient.Resources.Strings.SocialHintDefault;
        }

        private void SocialTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SocialTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string prefix = selectedItem.Tag.ToString();
                PrefixTextBlock.Text = prefix;

                UrlTextBox.Focus();
                HintText.Text = GameClient.Resources.Strings.SocialHintActive;
            }
        }

        private void UrlTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorPastingDisabled,
                                GameClient.Resources.Strings.DialogActionBlockedTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }));
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogClosed?.Invoke(this, EventArgs.Empty);

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UrlTextBox.Text.Trim();

            if (SocialTypeComboBox.SelectedIndex == -1)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorSelectPlatform,
                                GameClient.Resources.Strings.DialogWarningTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorEmptyUsername,
                                GameClient.Resources.Strings.DialogWarningTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (username.Length > 70)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorUsernameTooLong,
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fullUrl = PrefixTextBlock.Text + username;

            LinkAdded?.Invoke(this, fullUrl);
        }
    }
}