using System;
using System.IO;
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

        //public App()
        //{
        //    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        //}

        //private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        //{
        //    Console.WriteLine($"{e}");
        //    Console.WriteLine($"{e.ExceptionObject}");
        //}

        public App()
        {
            //new DirectoryInfo("C:\\Users\\buh\\AppData\\Roaming\\buh\\Watcher\\Settings.json").Parent.Create();
            //Console.WriteLine($"{new DirectoryInfo("C:\\Users\\buh\\AppData\\Roaming\\buh\\Watcher\\Settings.json").Parent.Exists}");
            //Environment.Exit(-1);

            LogManager.Configuration = Helper.DefaultLogConfig();
            Exit += App_Exit;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            _logger.Info($"------ {Helper.AppNameWithVersion} ------");

            UpdateApp();
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
            MessageBox.Show($"{e.Exception.Message}\r\n\r\n\r\n{e.Exception}", $"Ошибка :: {Helper.AppNameWithVersion}", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            _logger.Info($"ApplicationExitCode: {e.ApplicationExitCode}");
        }
    }
}
