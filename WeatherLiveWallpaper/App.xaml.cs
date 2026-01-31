using System;
using System.Windows;

namespace WeatherLiveWallpaper
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configurar manejo de excepciones no controladas
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Error no controlado: {args.ExceptionObject}",
                    "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Error en la interfaz: {args.Exception.Message}",
                    "Error de UI", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}