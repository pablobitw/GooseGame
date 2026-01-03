using GameClient.Views; 
using System.Linq;
using System.Windows;

namespace GameClient.Helpers
{
    public static class SessionManager
    {
        public static string CurrentUsername { get; private set; }
        public static string CurrentEmail { get; private set; }
        public static bool IsGuest { get; private set; }

        public static void StartSession(string username, string email, bool isGuest)
        {
            CurrentUsername = username;
            CurrentEmail = email;
            IsGuest = isGuest;
        }

        public static void ClearSession()
        {
            CurrentUsername = null;
            CurrentEmail = null;
            IsGuest = false;
        }

        public static void ForceLogout(string message = "Se ha perdido la conexión con el servidor.")
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ClearSession();

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