using System;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using Squirrel;
using Tools;

namespace Watcher
{
    public partial class App
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public App()
        {
            LogManager.Configuration = Helper.DefaultLogConfig()
#if !DEBUG
                    .SetFileNamePrefix("..\\")
#endif
                ;
            Exit += App_Exit;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            _logger.HelloWorld();
#if !DEBUG
            UpdateApp();
#endif
        }

        private async Task UpdateApp()
        {
            _logger.Info($"Start");

            try
            {
                using (var mgr = new UpdateManager("K:\\updates\\Watcher"))
                {
                    _logger.Info($"UpdateApp()");
                    await mgr.UpdateApp();
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private void Current_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Error(e.Exception);
            MessageBox.Show($"{e.Exception.Message}\r\n\r\n\r\n{e.Exception}", $"Ошибка :: {Helper.AppNameWithVersion}",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            _logger.Info($"ApplicationExitCode: {e.ApplicationExitCode}");
        }
    }
}