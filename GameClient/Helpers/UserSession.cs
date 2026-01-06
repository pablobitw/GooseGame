using GameClient.Views;
using System.Linq;
using System.Windows;
using System;

namespace GameClient.Helpers
{
    public class UserSession
    {
        private static UserSession _instance;
        private static readonly object _lock = new object();

        public string Username { get; private set; }
        public string Email { get; private set; }
        public bool IsGuest { get; private set; }

        private UserSession() { }

        public static UserSession GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new UserSession();
                    }
                }
            }
            return _instance;
        }

        public void SetSession(string username, string email, bool isGuest)
        {
            Username = username;
            Email = email;
            IsGuest = isGuest;
        }

        public void Logout()
        {
            Username = null;
            Email = null;
            IsGuest = false;
        }

   
        public void HandleCatastrophicError(string customMessage = null)
        {
            if (Application.Current == null) return;

            string finalMessage = customMessage ?? GameClient.Resources.Strings.ErrorConnectionLost;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Logout();

                try
                {
                    AudioManager.StopMusic();
                    if (LobbyServiceManager.Instance != null) LobbyServiceManager.Instance.Dispose();
                    if (GameplayServiceManager.Instance != null) GameplayServiceManager.Instance.Dispose();
                    if (FriendshipServiceManager.Instance != null) FriendshipServiceManager.Instance.Disconnect();
                }
                catch
                {
                    //
                }

                AuthWindow loginWindow = new AuthWindow();
                loginWindow.Show();

                var openWindows = Application.Current.Windows.Cast<Window>()
                                     .Where(w => w != loginWindow)
                                     .ToList();

                foreach (Window window in openWindows)
                {
                    window.Close();
                }

                MessageBox.Show(finalMessage,
                                GameClient.Resources.Strings.SessionTerminatedTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            });
        }
    }
}