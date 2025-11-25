using System.Windows.Media;

namespace GameClient.Models
{
    public class FriendDisplayModel
    {
        public string Username { get; set; }
        public bool IsOnline { get; set; }

        public string StatusText => IsOnline ? "Online" : "Offline";

        public Brush StatusColorBrush => IsOnline
            ? new SolidColorBrush(Colors.LimeGreen)
            : new SolidColorBrush(Colors.Gray);
    }
}