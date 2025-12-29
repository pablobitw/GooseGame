using System;

namespace GameClient.Helpers
{
    public class UserSession
    {
        private static UserSession _instance;
        private static readonly object _lock = new object();

        public string Username { get; private set; }
        public bool IsGuest { get; private set; }
        public string Email { get; private set; }

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

        public void SetSession(string username, bool isGuest, string email = null)
        {
            Username = username;
            IsGuest = isGuest;
            Email = email ?? "Invitado";
        }

        public void Logout()
        {
            Username = null;
            IsGuest = false;
            Email = null;
        }
    }
}