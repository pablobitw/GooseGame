using System;
using System.Windows;
using System.Windows.Controls;
using GameClient;
using GameClient.Helpers; 

namespace GameClient.Views
{
    public partial class OptionsPage : Page
    {
        public OptionsPage()
        {
            InitializeComponent();
            this.Loaded += OptionsPage_Loaded;
        }

        private void OptionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                if (mainWindow.WindowStyle == WindowStyle.None && mainWindow.WindowState == WindowState.Maximized)
                {
                    ScreenModeComboBox.SelectedIndex = 0;
                }
                else if (mainWindow.WindowStyle == WindowStyle.None && mainWindow.WindowState == WindowState.Normal)
                {
                    ScreenModeComboBox.SelectedIndex = 1;
                }
                else
                {
                    ScreenModeComboBox.SelectedIndex = 2;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyVideoSettings();

            double musicVol = MusicSlider.Value;
            double sfxVol = SfxSlider.Value;
            string language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            MessageBox.Show("¡Configuración aplicada correctamente!", "Opciones", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyVideoSettings()
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow == null) return;

            int selectedMode = ScreenModeComboBox.SelectedIndex;

            switch (selectedMode)
            {
                case 0:
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Maximized;
                    mainWindow.ResizeMode = ResizeMode.NoResize;
                    break;

                case 1: 
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.NoResize;
                    mainWindow.Width = 1280;
                    mainWindow.Height = 720;
                    mainWindow.CenterWindow(); 
                    break;

                case 2: 
                    mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.CanResize;
                    break;
            }
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