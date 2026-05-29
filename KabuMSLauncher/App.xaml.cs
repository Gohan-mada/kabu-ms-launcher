using System;
using System.Windows;
using System.Windows.Threading;

namespace KabuMSLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnUnhandledException;
            base.OnStartup(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "予期せぬエラーが発生しました。\n\n" + e.Exception.Message,
                "かぶ自動LOGIN",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
