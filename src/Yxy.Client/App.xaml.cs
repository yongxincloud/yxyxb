using System.Windows;
using System.Windows.Threading;
using Yxy.Client.Dialogs;
using Yxy.Cloud;
using Yxy.Core;
using Yxy.Core.Runtime;
using Yxy.Core.Store;

namespace Yxy.Client
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            CloudModule.Register();
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            OpenMain();
        }

        private void OpenMain()
        {
            var boot = HostBootstrap.Start();
            var settings = SettingsStore.Get(boot.Db);
            ThemeService.Apply(settings.Theme);
            if (!string.IsNullOrEmpty(settings.LockPassword))
            {
                var gate = new LockWindow(settings.LockPassword, settings.SoftwareId);
                var passed = gate.ShowDialog() == true;
                if (!passed)
                {
                    boot.Db.Dispose();
                    Shutdown();
                    return;
                }
            }

            var main = new MainWindow(boot);
            MainWindow = main;
            main.Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, AppPaths.ProductDisplayName);
            e.Handled = true;
        }
    }
}
