using System;
using System.Threading.Tasks;
using System.Windows;
using KabuMSLauncher.Services;

namespace KabuMSLauncher
{
    public partial class MailSettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private string _pendingEmail = string.Empty;
        private bool _ready;

        public MailSettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            Loaded += (s, e) =>
            {
                NotifyOnSuccessCheck.IsChecked = _settings.NotifyOnSuccess;
                ShowState();
                _ready = true;
            };
        }

        private bool IsRegistered => !string.IsNullOrEmpty(_settings.EncryptedNotifyToken);

        private void ShowState()
        {
            RegisterPanel.Visibility = IsRegistered ? Visibility.Collapsed : Visibility.Visible;
            RegisteredPanel.Visibility = IsRegistered ? Visibility.Visible : Visibility.Collapsed;
            RegisteredEmailText.Text = _settings.NotifyEmail;
        }

        private async void SendCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var email = (EmailBox.Text ?? string.Empty).Trim();
            if (email.Length == 0) { SetStatus("メールアドレスを入力してください。", true); return; }

            SendCodeButton.IsEnabled = false;
            SetStatus("確認コードを送っています...", false);
            var r = await Task.Run(() => MailNotifier.Register(email));
            SendCodeButton.IsEnabled = true;
            if (!r.Ok) { SetStatus(MailNotifier.Describe(r.Error), true); return; }

            _pendingEmail = email;
            CodePanel.Visibility = Visibility.Visible;
            CodeBox.Focus();
            SetStatus(email + " に確認コードを送りました。届かないときは迷惑メールのフォルダもご確認ください。", false);
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            var code = (CodeBox.Text ?? string.Empty).Trim();
            VerifyButton.IsEnabled = false;
            SetStatus("確認しています...", false);
            var email = _pendingEmail;
            var r = await Task.Run(() => MailNotifier.Verify(email, code));
            VerifyButton.IsEnabled = true;
            if (!r.Ok) { SetStatus(MailNotifier.Describe(r.Error), true); return; }

            _settings.NotifyEmail = email.ToLowerInvariant();
            _settings.EncryptedNotifyToken = CredentialStore.Encrypt(r.Token);
            _settings.Save();
            CodeBox.Text = string.Empty;
            CodePanel.Visibility = Visibility.Collapsed;
            ShowState();
            SetStatus("登録しました。「試しに送る」で届くかを確かめられます。", false);
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var token = SafeToken();
            if (token == null) return;
            TestButton.IsEnabled = false;
            SetStatus("送っています...", false);
            var r = await Task.Run(() => MailNotifier.Send(token, "test", string.Empty));
            TestButton.IsEnabled = true;
            SetStatus(r.Ok ? "送りました。メールが届いたかご確認ください。" : MailNotifier.Describe(r.Error), !r.Ok);
        }

        private async void UnregisterButton_Click(object sender, RoutedEventArgs e)
        {
            var token = SafeToken();
            UnregisterButton.IsEnabled = false;
            if (token != null) await Task.Run(() => MailNotifier.Unregister(token));
            UnregisterButton.IsEnabled = true;
            // サーバーに届かなくても、このPCからは送らなくなる
            _settings.NotifyEmail = string.Empty;
            _settings.EncryptedNotifyToken = string.Empty;
            _settings.Save();
            ShowState();
            SetStatus("登録を解除しました。", false);
        }

        private void NotifyOnSuccessCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            _settings.NotifyOnSuccess = NotifyOnSuccessCheck.IsChecked == true;
            _settings.Save();
        }

        private string SafeToken()
        {
            try { return CredentialStore.Decrypt(_settings.EncryptedNotifyToken); }
            catch
            {
                SetStatus("登録の情報を読めませんでした。登録を解除して、もう一度登録してください。", true);
                return null;
            }
        }

        private void SetStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = isError
                ? System.Windows.Media.Brushes.Crimson
                : (System.Windows.Media.Brush)FindResource("TextMuted");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
