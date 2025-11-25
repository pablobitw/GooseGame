using System.Windows;

namespace GameClient.Helpers
{
    public static class DialogHelper
    {
        public static void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowInfo(string message)
        {
            MessageBox.Show(message, "Información", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string message)
        {
            MessageBox.Show(message, "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static bool ShowConfirmation(string message, string title = "Confirmación")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
}