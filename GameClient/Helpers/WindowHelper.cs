using System.Windows;

namespace GameClient.Helpers
{
    public static class WindowHelper
    {
        public static void CenterWindow(this Window window)
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            window.Left = (screenWidth - window.Width) / 2;
            window.Top = (screenHeight - window.Height) / 2;
        }
    }
}