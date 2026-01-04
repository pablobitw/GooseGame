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
            HintText.Text = "Selecciona una red social.";
        }

        private void SocialTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SocialTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string prefix = selectedItem.Tag.ToString();
                PrefixTextBlock.Text = prefix;

                UrlTextBox.Focus();
                HintText.Text = "Máximo 70 caracteres. Pegado deshabilitado.";
            }
        }

        private void UrlTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("Por seguridad, el pegado está deshabilitado.",
                                "Acción bloqueada",
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
                MessageBox.Show("Selecciona una red social.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Escribe tu nombre de usuario.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (username.Length > 70)
            {
                MessageBox.Show("El nombre de usuario es demasiado largo.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fullUrl = PrefixTextBlock.Text + username;

            LinkAdded?.Invoke(this, fullUrl);
        }
    }
}