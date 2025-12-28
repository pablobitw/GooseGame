using System.Windows;
using System.Windows.Threading;

namespace GameClient
{
    public partial class App : Application
    {
        // Constructor para enganchar los eventos de error global
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // ESTO ES LO QUE TE DIRÁ POR QUÉ SE CIERRA
            string errorMessage = $"Error Crítico: {e.Exception.Message}\n\n" +
                                  $"Detalle: {e.Exception.InnerException?.Message}\n\n" +
                                  $"Stack Trace: {e.Exception.StackTrace}";

            MessageBox.Show(errorMessage, "Crash Report", MessageBoxButton.OK, MessageBoxImage.Error);
            
            e.Handled = true; // Esto evita que la app se cierre de golpe
        }
    }
}