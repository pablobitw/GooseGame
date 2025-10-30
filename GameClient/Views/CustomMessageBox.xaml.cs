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
                AddButton("Aceptar", isDefault: true, result: true, styleKey: "PrimaryButtonStyle");
            }
            else
            {
                for (int i = 0; i < buttonLabels.Length; i++)
                {
                    string label = buttonLabels[i];
                    bool isPositiveAction = (i == 0); 


                    string styleKey = isPositiveAction ? "OrangeButtonStyle" : "PrimaryButtonStyle";

                    AddButton(label, isDefault: isPositiveAction, result: isPositiveAction, styleKey: styleKey);
                }
            }
        }

        private void AddButton(string label, bool isDefault, bool result, string styleKey)
        {
            Style buttonStyle = (Style)Application.Current.FindResource(styleKey);

            var button = new Button
            {
                Content = label,
                IsDefault = isDefault,
                Style = buttonStyle, 
                Width = 120,
                Height = 50,
                Margin = new Thickness(10, 0, 10, 0)
            };

            button.Click += (sender, e) =>
            {
                this.DialogResult = result;
                this.Close();
            };

            ButtonsPanel.Children.Add(button);
        }
    }
}






