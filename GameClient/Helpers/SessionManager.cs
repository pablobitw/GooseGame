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

        public static void ForceLogout(string message = null)
        {
            if (Application.Current == null) return;

            string finalMessage = message ?? GameClient.Resources.Strings.ErrorConnectionLost;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ClearSession();

                MessageBox.Show(finalMessage,
                                GameClient.Resources.Strings.SessionTerminatedTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                AuthWindow loginWindow = new AuthWindow();
                loginWindow.Show();

                var openWindows = Application.Current.Windows.Cast<Window>().Where(w => w != loginWindow).ToList();
                foreach (Window window in openWindows)
                {
                    window.Close();
                }
            });
        }
    }
}
