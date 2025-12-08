using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GameClient
{
    /// <summary>
    /// Lógica de interacción para App.xaml
    /// </summary>
    public partial class App : Application
    {
    }
}

//PASAR A INGLES
/*using System.Globalization;
using System.Threading;
using System.Windows;

namespace GameClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // --- ESTA LÍNEA FUERZA EL INGLÉS ---
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");
            // -----------------------------------

            // Si quieres probar español de nuevo, comenta la línea de arriba o cámbiala a "es"

            base.OnStartup(e);
        }
    }
}
*/