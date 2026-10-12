using System;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KabuMSLauncher.Services;

namespace KabuMSLauncher
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly ReconnectWatcher _watcher;
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private bool _exitRequested;
        private bool _trayHintShown;
        private bool _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            _watcher = new ReconnectWatcher(() => _settings.AutoEnableOrder);
            _watcher.StatusChanged += (msg, isError) => Dispatcher.BeginInvoke(new Action(() => SetStatus(msg, isError)));
            _watcher.SequenceFinished += (ok, summary) =>
            {
                Dispatcher.BeginInvoke(new Action(() => ShowTrayBalloon(
                    ok ? "再接続しました" : "再接続を確認してください",
                    ok ? "MarketspeedⅡとExcelの再接続を行いました。" : "自動の再接続が途中で止まりました。画面を確認してください。",
                    !ok)));
                SendMailNotice(ok, summary);
            };
            ApplySettingsToUi();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AutoStartCheck.IsChecked = AutoStartRegistry.IsEnabled();
            AutoReconnectCheck.IsChecked = _settings.AutoReconnect;
            AutoEnableOrderCheck.IsChecked = _settings.AutoEnableOrder;
            AutoEnableOrderCheck.IsEnabled = _settings.AutoReconnect;
            UpdateMailState();
            _uiReady = true;
            SetupTrayIcon();
            if (_settings.AutoReconnect) _watcher.Start();
        }

        private void SendMailNotice(bool ok, string summary)
        {
            if (string.IsNullOrEmpty(_settings.EncryptedNotifyToken)) return;
            if (ok && !_settings.NotifyOnSuccess) return;
            string token;
            try { token = CredentialStore.Decrypt(_settings.EncryptedNotifyToken); }
            catch { return; }
            MailNotifier.SendInBackground(token, ok ? "succeeded" : "failed", summary,
                msg => Dispatcher.BeginInvoke(new Action(() => SetStatus(msg, msg.Contains("送れません")))));
        }

        private void UpdateMailState()
        {
            MailStateText.Text = string.IsNullOrEmpty(_settings.EncryptedNotifyToken)
                ? "メール通知：未設定"
                : "メール通知：" + _settings.NotifyEmail;
            MailSettingsButton.Content = string.IsNullOrEmpty(_settings.EncryptedNotifyToken) ? "設定する" : "変更する";
        }

        private void MailSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            new MailSettingsWindow(_settings) { Owner = this }.ShowDialog();
            UpdateMailState();
        }

        private void ReconnectSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            _settings.AutoReconnect = AutoReconnectCheck.IsChecked == true;
            _settings.AutoEnableOrder = AutoEnableOrderCheck.IsChecked == true;
            AutoEnableOrderCheck.IsEnabled = _settings.AutoReconnect;
            _settings.Save();
            if (_settings.AutoReconnect) _watcher.Start();
            else _watcher.Stop();
        }

        // 見守り中は、×で閉じてもタスクトレイに残る（閉じると再接続できないため）
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_exitRequested && _watcher.IsRunning)
            {
                e.Cancel = true;
                Hide();
                if (!_trayHintShown)
                {
                    ShowTrayBalloon("かぶ自動LOGIN", "通信切断の見守りを続けています。終了するときは右下のアイコンを右クリックしてください。", false);
                    _trayHintShown = true;
                }
                return;
            }
            _watcher.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }

        private void SetupTrayIcon()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("開く", null, (s, a) => RestoreFromTray());
            menu.Items.Add("再接続の記録を見る", null, (s, a) => OpenUrl(ReconnectWatcher.LogPath));
            menu.Items.Add("終了", null, (s, a) => { _exitRequested = true; Close(); });

            System.Drawing.Icon icon = null;
            try
            {
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
                if (res != null) icon = new System.Drawing.Icon(res.Stream);
            }
            catch { }

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = icon ?? System.Drawing.SystemIcons.Application,
                Text = "かぶ自動LOGIN",
                ContextMenuStrip = menu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, a) => RestoreFromTray();
        }

        public void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ShowTrayBalloon(string title, string text, bool isError)
        {
            if (_trayIcon == null) return;
            try
            {
                _trayIcon.ShowBalloonTip(8000, title, text,
                    isError ? System.Windows.Forms.ToolTipIcon.Warning : System.Windows.Forms.ToolTipIcon.Info);
            }
            catch { }
        }

        private void ApplySettingsToUi()
        {
            if (!string.IsNullOrEmpty(_settings.LoginId))
            {
                LoginIdBox.Text = _settings.LoginId;
            }

            if (!string.IsNullOrEmpty(_settings.EncryptedPassword))
            {
                try
                {
                    var pw = CredentialStore.Decrypt(_settings.EncryptedPassword);
                    PasswordBox.Password = pw;
                    SetStatus("保存済みの認証情報を読み込みました。", false);
                }
                catch
                {
                    SetStatus("保存済みパスワードを復号できませんでした。再入力してください。", true);
                }
            }
        }

        private void ShowPasswordCheck_Toggled(object sender, RoutedEventArgs e)
        {
            if (ShowPasswordCheck.IsChecked == true)
            {
                PasswordPlainBox.Text = PasswordBox.Password;
                PasswordPlainBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                PasswordBox.Password = PasswordPlainBox.Text;
                PasswordPlainBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
            }
        }

        private string GetCurrentPassword()
        {
            return ShowPasswordCheck.IsChecked == true ? PasswordPlainBox.Text : PasswordBox.Password;
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var id = LoginIdBox.Text?.Trim() ?? string.Empty;
            var pw = GetCurrentPassword() ?? string.Empty;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                SetStatus("ログインIDとパスワードを入力してください。", true);
                return;
            }

            PersistSettings(id, pw);

            LaunchButton.IsEnabled = false;
            SetStatus("MarketspeedⅡ を起動しています...", false);

            try
            {
                var launcher = new MarketSpeedLauncher();
                var report = await Task.Run(() => launcher.LaunchAndLogin(id, pw, status => Dispatcher.Invoke(() => SetStatus(status, false))));

                if (report.Success)
                {
                    SetStatus("ログイン処理を完了しました。", false);
                }
                else
                {
                    SetStatus("自動入力に失敗しました：" + report.Message + "\n手動で入力してください。", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus("起動エラー：" + ex.Message, true);
            }
            finally
            {
                LaunchButton.IsEnabled = true;
            }
        }

        private void PersistSettings(string id, string pw)
        {
            if (SaveCredentialsCheck.IsChecked == true)
            {
                _settings.LoginId = id;
                _settings.EncryptedPassword = CredentialStore.Encrypt(pw);
            }
            else
            {
                _settings.LoginId = string.Empty;
                _settings.EncryptedPassword = string.Empty;
            }
            _settings.Save();

            try
            {
                if (AutoStartCheck.IsChecked == true) AutoStartRegistry.Enable();
                else AutoStartRegistry.Disable();
            }
            catch { /* non-critical */ }
        }

        private void SetStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = isError
                ? System.Windows.Media.Brushes.Crimson
                : (System.Windows.Media.Brush)FindResource("TextMuted");
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InfoDialog
            {
                Owner = this,
                Title = "使い方",
                HeaderText = "かぶ自動LOGIN の使い方",
                BodyText =
                    "■ 使い方\n" +
                    "1. 楽天証券のログインID・パスワードを入力\n" +
                    "2. 「保存する」にチェック（次回から入力不要）\n" +
                    "3. 「MarketspeedⅡ を起動する」をクリック\n\n" +
                    "■ セキュリティについて\n" +
                    "・パスワードは Windows DPAPI で暗号化保存されます\n" +
                    "・同じPC・同じユーザーアカウントでのみ復号可能\n" +
                    "・ファイル単体を他PCに移しても復号できません\n\n" +
                    "■ 通信が切断されたときの自動再接続\n" +
                    "・「サーバとの通信が切断されました」の画面が出たら、インターネットが戻るのを待ってOKを押します\n" +
                    "・再接続のあと、Excelの楽天RSS「未接続」を押します（「発注不可」は設定で選んだときだけ）\n" +
                    "・Excelのボタンは、クイックアクセスツールバーに置いておくと確実に押せます\n" +
                    "・見守り中は×で閉じても右下のアイコンに残ります。終了は右クリック→「終了」\n" +
                    "・再接続できなかったときは、もう一度は試しません\n\n" +
                    "■ うまく動かないとき\n" +
                    "・MarketspeedⅡ のアップデート直後はUI構造が変わり、自動入力が失敗する場合があります\n" +
                    "・その場合は手動でログインしてください\n" +
                    "・継続して失敗する場合はサポートサイトをご確認ください",
                LinkText = "詳しい使い方・サポート（cabu-labo.co.jp）",
                // 商品ページへ直接飛ばす（使い方3ステップとFAQがここにある）。
                // utm は、アプリから来た人をGA4で数えるための印。
                LinkUrl = "https://cabu-labo.co.jp/kabu_auto_login/?utm_source=app&utm_medium=help-button"
            };
            dlg.ShowDialog();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InfoDialog
            {
                Owner = this,
                Title = "バージョン情報",
                HeaderText = "かぶ自動LOGIN",
                BodyText =
                    "MarketspeedⅡ 自動起動アプリ\n" +
                    "Version 1.1.0\n" +
                    "提供：株式会社ボストーク / かぶ自動化LAB\n\n" +
                    "■ 免責事項\n" +
                    "本ツールは MarketspeedⅡ のログインを補助するローカルツールであり、" +
                    "楽天証券株式会社とは無関係の独立した第三者開発ツールです。\n" +
                    "本ツールの利用により生じたいかなる損害についても開発元は責任を負いません。\n" +
                    "ご利用は自己責任にてお願いします。",
                LinkText = "かぶ自動化LAB 公式サイト（cabu-labo.co.jp）",
                LinkUrl = "https://cabu-labo.co.jp/"
            };
            dlg.ShowDialog();
        }

        private void LabCtaButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://cabu-labo.co.jp/");
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { /* best-effort */ }
        }
    }
}
