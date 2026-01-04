using System.Windows;
using System.Windows.Controls;

namespace GameClient
{
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string message, params string[] buttonLabels)
        {
            InitializeComponent();
            MessageText.Text = message;

            if (buttonLabels == null || buttonLabels.Length == 0)
            {
                AddButton("Aceptar", true, true, "PrimaryButtonStyle", 220, 414);
            }
            else
            {
                for (int i = 0; i < buttonLabels.Length; i++)
                {
                    string label = buttonLabels[i];
                    bool isPositiveAction = (i == 0);
                    string styleKey = isPositiveAction ? "OrangeButtonStyle" : "PrimaryButtonStyle";

                    double left = 140 + (i * 160);
                    double top = 414;

                    AddButton(label, isPositiveAction, isPositiveAction, styleKey, left, top);
                }
            }
        }

        private void AddButton(string label, bool isDefault, bool result, string styleKey, double left, double top)
        {
            Style buttonStyle = (Style)Application.Current.FindResource(styleKey);

            var button = new Button
            {
                Content = label,
                IsDefault = isDefault,
                Style = buttonStyle,
                Width = 140,
                Height = 54,
                FontFamily = new System.Windows.Media.FontFamily("Fredoka Medium"),
                FontSize = 36
            };

            button.Click += (sender, e) =>
            {
                this.DialogResult = result;
                this.Close();
            };

            if (this.Content is Border border && border.Child is Canvas canvas)
            {
                canvas.Children.Add(button);
                Canvas.SetLeft(button, left);
                Canvas.SetTop(button, top);
            }
        }

        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

    }
}
