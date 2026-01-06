using System;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views.Components
{
    public partial class AddSocialLinkDialog : UserControl
    {
        private const int MaxUsernameLength = 70;
        private const string DefaultPrefix = "https://...";

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
            PrefixTextBlock.Text = DefaultPrefix;
            HintText.Text = GameClient.Resources.Strings.SocialHintDefault;

            UrlTextBox.ClearValue(Border.BorderBrushProperty);
        }

        private void SocialTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SocialTypeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UrlTextBox.Text.Trim();

            if (SocialTypeComboBox.SelectedIndex == -1)
            {
                ShowWarning(GameClient.Resources.Strings.Social_Error_SelectPlatform);
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                ShowWarning(GameClient.Resources.Strings.Social_Error_EmptyUser);
                return;
            }

            if (username.Length > MaxUsernameLength)
            {
                ShowWarning(GameClient.Resources.Strings.Social_Error_TooLong);
                return;
            }

            string fullUrl = PrefixTextBlock.Text + username;

            LinkAdded?.Invoke(this, fullUrl);
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message,
                            GameClient.Resources.Strings.DialogWarningTitle,
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
        }
    }
}