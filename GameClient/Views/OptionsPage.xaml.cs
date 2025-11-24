using System;
using System.Windows;
using System.Windows.Controls;
using GameClient; 

namespace GameClient.Views
{
    public partial class OptionsPage : Page
    {
        public OptionsPage()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // TODO: Cargar configuración guardada (UserPreferences o archivo local)
            // Ejemplo:
            // MusicSlider.Value = Settings.Default.MusicVolume;
            // LanguageComboBox.SelectedIndex = ...
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            double musicVol = MusicSlider.Value;
            double sfxVol = SfxSlider.Value;
            string screenMode = (ScreenModeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();


            MessageBox.Show("¡Configuración guardada correctamente!", "Opciones", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                mainWindow.ShowMainMenu();
            }
        }
    }
}