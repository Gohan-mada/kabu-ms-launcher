using System;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace KabuMSLauncher.Services
{
    public class AppSettings
    {
        public string LoginId { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public string MarketSpeedExePath { get; set; } = string.Empty;
        // v1.1.0: 通信切断の画面でOKを押し、Excelの「未接続」も押す
        public bool AutoReconnect { get; set; } = true;
        // v1.1.0: 再接続のあと「発注不可」も押すか（注文が出る状態に戻るので既定は押さない）
        public bool AutoEnableOrder { get; set; } = false;
        // v1.1.0: メール通知（確認コードで登録したアドレスと、受け口が発行したトークン〈DPAPIで暗号化〉）
        public string NotifyEmail { get; set; } = string.Empty;
        public string EncryptedNotifyToken { get; set; } = string.Empty;
        public bool NotifyOnSuccess { get; set; } = true;

        private static string SettingsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KabuMSLauncher");

        private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var ser = new JavaScriptSerializer();
                    var s = ser.Deserialize<AppSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { /* fall through */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);
                var ser = new JavaScriptSerializer();
                File.WriteAllText(SettingsPath, ser.Serialize(this));
            }
            catch { /* non-critical */ }
        }
    }

    public static class AutoStartRegistry
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "KabuMSLauncher";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void Enable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                var exe = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(exe))
                {
                    key.SetValue(ValueName, "\"" + exe + "\"");
                }
            }
        }

        public static void Disable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName);
            }
        }
    }
}
