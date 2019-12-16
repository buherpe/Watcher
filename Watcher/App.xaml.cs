using System;
using System.Linq;
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
                var urlOrPath = $"http://buherpet.tk:9999/updates/{Helper.AppName}";
                _logger.Info($"urlOrPath: {urlOrPath}");
                using (var mgr = new UpdateManager(urlOrPath))
                {
                    var updateInfo = await mgr.CheckForUpdate();
                    _logger.Info($"ReleasesToApply.Count: {updateInfo.ReleasesToApply.Count}");
                    if (updateInfo.ReleasesToApply.Any())
                    {
                        _logger.Info($"UpdateApp()");
                        await mgr.UpdateApp(Progress);
                        _logger.Info($"UpdateHelper.Updated?.Invoke()");
                        UpdateHelper.Updated?.Invoke();
                    }
                    
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private void Progress(int obj)
        {
            _logger.Info($"{obj}");
            //if (obj == 99)
            //{
            //    //MessageBox.Show($"Update ready, restart to apply");
            //    UpdateHelper.Updated?.Invoke();
            //}
        }

        private void Current_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Error(e.Exception);
            MessageBox.Show($"{e.Exception.Message}\r\n\r\n\r\n{e.Exception}", $"Error :: {Helper.AppNameWithVersion}",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            _logger.Info($"ApplicationExitCode: {e.ApplicationExitCode}");
        }
    }

    public static class UpdateHelper
    {
        public static Action Updated { get; set; }
    }
}