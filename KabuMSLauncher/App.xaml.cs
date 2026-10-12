using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace KabuMSLauncher
{
    public partial class App : Application
    {
        // 2つ同時に動くと、切断の画面やExcelのボタンを2回押してしまう（押した直後の「接続中」を切る）。
        // 2つ目の起動では、動いている方に「画面を出して」と合図して終わる。
        private const string MutexName = @"Local\KabuMSLauncher.SingleInstance";
        private const string ShowEventName = @"Local\KabuMSLauncher.ShowWindow";
        private static Mutex _mutex;
        private static EventWaitHandle _showEvent;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
                Environment.Exit(0);
                return;
            }

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            var listener = new Thread(() =>
            {
                while (_showEvent.WaitOne())
                {
                    Dispatcher.BeginInvoke(new Action(() => (MainWindow as KabuMSLauncher.MainWindow)?.RestoreFromTray()));
                }
            }) { IsBackground = true, Name = "ShowWindowListener" };
            listener.Start();

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
