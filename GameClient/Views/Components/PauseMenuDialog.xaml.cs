using GameClient.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameClient.Views.Dialogs
{
    public partial class PauseMenuDialog : UserControl
    {
        public event EventHandler ResumeRequested;
        public event EventHandler QuitRequested;

        public PauseMenuDialog()
        {
            InitializeComponent();
            try
            {
                IngameMusicSlider.Value = AudioManager.GetVolume() * 100;
            }
            catch (Exception)
            {
                IngameMusicSlider.Value = 50;
            }
            IngameMusicSlider.ValueChanged += IngameMusicSlider_ValueChanged;
        }

        private void IngameMusicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                AudioManager.SetVolume(e.NewValue / 100.0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PauseMenu] Audio Error: {ex.Message}");
            }
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            QuitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Content_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void IngameScreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Window.GetWindow(this) is GameMainWindow mw)
                {
                    ApplyScreenSettings(mw);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PauseMenu] Screen Error: {ex.Message}");
            }
        }

        private void ApplyScreenSettings(GameMainWindow mw)
        {
            int index = IngameScreenModeCombo.SelectedIndex;

            if (mw == null) return;

            if (index == 0) 
            {
                mw.WindowStyle = WindowStyle.None;
                mw.WindowState = WindowState.Maximized;
            }
            else if (index == 1) 
            {
                mw.WindowStyle = WindowStyle.None;
                mw.WindowState = WindowState.Normal;
                mw.Width = 1280;
                mw.Height = 720;

                try { mw.CenterWindow(); } catch { /* */ }
            }
            else 
            {
                mw.WindowStyle = WindowStyle.SingleBorderWindow;
                mw.WindowState = WindowState.Normal;
            }
        }
    }
}