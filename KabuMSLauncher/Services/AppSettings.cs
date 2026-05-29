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
