using System.Windows;
using NLog;
using Tools;

namespace Watcher
{
    public partial class App
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        
        public App()
        {
            Helper.MagickInitMethod($"http://buherpet.tk:9999/updates/{Helper.AppName}");

            Exit += App_Exit;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception);
            MessageBox.Show($"{e.Exception.Message}\r\n\r\n\r\n{e.Exception}", $"Error :: {Helper.AppNameWithVersion}", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            Log.Info($"ApplicationExitCode: {e.ApplicationExitCode}");
        }
    }
}