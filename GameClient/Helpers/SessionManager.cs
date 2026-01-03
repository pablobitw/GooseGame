using GameClient.Views; // Asegúrate de que aquí esté tu AuthWindow
using System;
using System.Linq;
using System.Windows;

namespace GameClient.Helpers
{

    public static class SessionManager
    {
        public static void ForceLogout(string message = "Se ha perdido la conexión con el servidor.")
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Sesión Terminada", MessageBoxButton.OK, MessageBoxImage.Error);

                AuthWindow loginWindow = new AuthWindow();
                loginWindow.Show();

                
                var openWindows = Application.Current.Windows.Cast<Window>().ToList();

                foreach (Window window in openWindows)
                {
                    if (window != loginWindow)
                    {
                        window.Close();
                    }
                }

               
            });
        }
    }
}