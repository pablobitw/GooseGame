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
            if (Window.GetWindow(this) is GameMainWindow mw)
            {
                if (IngameScreenModeCombo.SelectedIndex == 0)
                {
                    mw.WindowStyle = WindowStyle.None;
                    mw.WindowState = WindowState.Maximized;
                }
                else if (IngameScreenModeCombo.SelectedIndex == 1)
                {
                    mw.WindowStyle = WindowStyle.None;
                    mw.WindowState = WindowState.Normal;
                    mw.Width = 1280;
                    mw.Height = 720;
                    mw.CenterWindow();
                }
                else
                {
                    mw.WindowStyle = WindowStyle.SingleBorderWindow;
                    mw.WindowState = WindowState.Normal;
                }
            }
        }
    }
}