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

        public MainWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            ApplySettingsToUi();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AutoStartCheck.IsChecked = AutoStartRegistry.IsEnabled();
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
                    "■ うまく動かないとき\n" +
                    "・MarketspeedⅡ のアップデート直後はUI構造が変わり、自動入力が失敗する場合があります\n" +
                    "・その場合は手動でログインしてください\n" +
                    "・継続して失敗する場合はサポートサイトをご確認ください",
                LinkText = "詳しい使い方・サポート（cabu.info）",
                LinkUrl = "https://cabu.info/"
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
                    "Version 1.0.0\n" +
                    "提供：株式会社ボストーク / かぶ自動化LAB\n\n" +
                    "■ 免責事項\n" +
                    "本ツールは MarketspeedⅡ のログインを補助するローカルツールであり、" +
                    "楽天証券株式会社とは無関係の独立した第三者開発ツールです。\n" +
                    "本ツールの利用により生じたいかなる損害についても開発元は責任を負いません。\n" +
                    "ご利用は自己責任にてお願いします。",
                LinkText = "かぶ自動化LAB 公式サイト（cabu.info）",
                LinkUrl = "https://cabu.info/"
            };
            dlg.ShowDialog();
        }

        private void LabCtaButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://cabu.info/");
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
