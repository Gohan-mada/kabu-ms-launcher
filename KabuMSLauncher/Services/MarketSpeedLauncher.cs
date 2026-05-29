using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace KabuMSLauncher.Services
{
    public class LaunchReport
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MarketSpeedLauncher
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private const int SW_RESTORE = 9;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        // Probe locations (LocalAppData ClickOnce-style install is the standard MSⅡ deployment)
        private static readonly string[] CandidatePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"MarketSpeed2\Bin\MarketSpeed2.exe"),
            @"C:\Program Files\MarketSpeed2\MarketSpeed2.exe",
            @"C:\Program Files (x86)\MarketSpeed2\MarketSpeed2.exe",
        };

        public LaunchReport LaunchAndLogin(string loginId, string password, Action<string> progress)
        {
            var report = new LaunchReport();

            var exe = ResolveExePath();
            if (exe == null)
            {
                report.Success = false;
                report.Message = "MarketSpeed2.exe が見つかりません。インストール済みかご確認ください。";
                return report;
            }

            // Only start a new process if no login window is already on screen.
            var preLogin = FindLoginWindow();
            if (preLogin == null)
            {
                progress?.Invoke("MarketspeedⅡ を起動中...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
                    UseShellExecute = true,
                };
                try { Process.Start(startInfo); }
                catch (Exception ex)
                {
                    report.Success = false;
                    report.Message = "起動に失敗：" + ex.Message;
                    return report;
                }
            }
            else
            {
                progress?.Invoke("既存のログインウィンドウを使用します...");
            }

            progress?.Invoke("ログインウィンドウを待機中...");
            var loginWindow = WaitForLoginWindow(timeoutSec: 60);
            if (loginWindow == null)
            {
                report.Success = false;
                report.Message = "ログインウィンドウが見つかりませんでした（タイムアウト60秒）。\n既にログイン済みの可能性があります。";
                return report;
            }

            // Give the window a moment to fully render before interacting.
            Thread.Sleep(700);
            BringWindowForeground(loginWindow);
            Thread.Sleep(250);

            progress?.Invoke("認証情報を入力中...");
            string error;
            var filled = FillCredentialsAndSubmit(loginWindow, loginId, password, progress, out error);
            if (!filled)
            {
                report.Success = false;
                report.Message = string.IsNullOrEmpty(error) ? "ID/PW自動入力に失敗しました。" : error;
                return report;
            }

            report.Success = true;
            report.Message = "自動ログインを実行しました。";
            return report;
        }

        private static void BringWindowForeground(AutomationElement element)
        {
            try
            {
                var hwnd = new IntPtr(element.Current.NativeWindowHandle);
                if (hwnd == IntPtr.Zero) return;

                ShowWindow(hwnd, SW_RESTORE);

                // AttachThreadInput trick: focus stealing prevention bypass when this process is foreground.
                uint targetProcId;
                uint targetThread = GetWindowThreadProcessId(hwnd, out targetProcId);
                uint currentThread = GetCurrentThreadId();
                bool attached = false;
                if (targetThread != currentThread)
                {
                    attached = AttachThreadInput(currentThread, targetThread, true);
                }
                try
                {
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                }
                finally
                {
                    if (attached) AttachThreadInput(currentThread, targetThread, false);
                }
            }
            catch { /* best-effort */ }
        }

        private static string ResolveExePath()
        {
            return CandidatePaths.FirstOrDefault(File.Exists);
        }

        private static AutomationElement WaitForLoginWindow(int timeoutSec)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
            while (DateTime.UtcNow < deadline)
            {
                var window = FindLoginWindow();
                if (window != null) return window;
                Thread.Sleep(500);
            }
            return null;
        }

        private static AutomationElement FindLoginWindow()
        {
            try
            {
                var ownPid = (uint)Process.GetCurrentProcess().Id;
                var root = AutomationElement.RootElement;
                var windows = root.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                foreach (AutomationElement w in windows)
                {
                    // Skip windows owned by our own process — our launcher title contains "MarketSpeed" and "LOGIN".
                    try
                    {
                        var hwnd = new IntPtr(w.Current.NativeWindowHandle);
                        if (hwnd == IntPtr.Zero) continue;
                        uint pid;
                        GetWindowThreadProcessId(hwnd, out pid);
                        if (pid == ownPid) continue;
                    }
                    catch { continue; }

                    var name = SafeName(w);
                    var className = SafeClassName(w);
                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(className)) continue;

                    if (LooksLikeMarketSpeedWindow(name, className))
                    {
                        if (HasPasswordField(w)) return w;
                    }
                }
            }
            catch { /* tree may be transient */ }
            return null;
        }

        private static bool LooksLikeMarketSpeedWindow(string name, string className)
        {
            // Use upper-case title which matches MSⅡ's actual title "MARKETSPEED II ログイン",
            // avoiding accidental matches with the launcher's lower-case "MarketspeedⅡ".
            if (name != null)
            {
                if (name.Contains("MARKETSPEED")) return true;
                if (name.Contains("マーケットスピード")) return true;
            }
            var hay = (name + " " + className).ToLowerInvariant();
            return hay.Contains("market speed") || hay.Contains("ms2 ");
        }

        private static bool HasPasswordField(AutomationElement window)
        {
            try
            {
                var edits = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                foreach (AutomationElement e in edits)
                {
                    if (e.Current.IsPassword) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool FillCredentialsAndSubmit(AutomationElement window, string loginId, string password, Action<string> progress, out string error)
        {
            error = string.Empty;
            try
            {
                var allEdits = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
                                     .Cast<AutomationElement>()
                                     .Where(e => e.Current.IsEnabled && !e.Current.IsOffscreen)
                                     .ToList();

                if (allEdits.Count < 2)
                {
                    error = "入力フィールドが2つ未満です。MSⅡのバージョンが変わっている可能性があります。";
                    return false;
                }

                var passwordEdit = allEdits.FirstOrDefault(e => e.Current.IsPassword);
                if (passwordEdit == null)
                {
                    error = "パスワード入力欄が検出できませんでした。";
                    return false;
                }

                var idCandidates = allEdits.Where(e => !e.Current.IsPassword).ToList();
                if (idCandidates.Count == 0)
                {
                    error = "ID入力欄が検出できませんでした。";
                    return false;
                }

                var idEdit = idCandidates.OrderBy(e => GetTop(e)).ThenBy(e => GetLeft(e)).First();

                // Re-foreground in case focus shifted during element discovery.
                BringWindowForeground(window);
                Thread.Sleep(150);

                // --- ID field ---
                if (!FillAndVerifyIdField(idEdit, loginId))
                {
                    error = "ID入力の検証に失敗しました（フィールドが空のままです）。手動で入力してください。";
                    return false;
                }

                // --- Password field ---
                if (!FillPasswordField(passwordEdit, password))
                {
                    error = "パスワード入力に失敗しました。手動で入力してください。";
                    return false;
                }

                Thread.Sleep(300);

                SubmitLogin(window, passwordEdit, progress);
                return true;
            }
            catch (Exception ex)
            {
                error = "自動入力で例外：" + ex.Message;
                return false;
            }
        }

        private static bool FillAndVerifyIdField(AutomationElement idEdit, string loginId)
        {
            // Attempt 1: ValuePattern
            string current = TryReadValue(idEdit);
            object pat;
            if (idEdit.TryGetCurrentPattern(ValuePattern.Pattern, out pat))
            {
                try { ((ValuePattern)pat).SetValue(loginId); } catch { }
                Thread.Sleep(120);
                if (TryReadValue(idEdit) == loginId) return true;
            }

            // Attempt 2: focus, clear, SendKeys
            FocusAndType(idEdit, loginId);
            Thread.Sleep(150);
            if (TryReadValue(idEdit) == loginId) return true;

            // Attempt 3: one more retry after re-foregrounding
            var parent = TreeWalker.ControlViewWalker.GetParent(idEdit);
            while (parent != null && parent.Current.ControlType != ControlType.Window)
            {
                parent = TreeWalker.ControlViewWalker.GetParent(parent);
            }
            if (parent != null) BringWindowForeground(parent);
            Thread.Sleep(150);
            FocusAndType(idEdit, loginId);
            Thread.Sleep(150);
            return TryReadValue(idEdit) == loginId;
        }

        private static bool FillPasswordField(AutomationElement pwEdit, string password)
        {
            // PasswordBox typically does not expose ValuePattern. Use focus + SendKeys.
            FocusAndType(pwEdit, password);
            Thread.Sleep(120);
            // We cannot read back the password value (IsPassword obscures it).
            // Verification is implicit: if the login attempt succeeds, the field had the right contents.
            return true;
        }

        private static void FocusAndType(AutomationElement edit, string text)
        {
            try { edit.SetFocus(); } catch { }
            Thread.Sleep(80);
            SendKeysSafe("^a");
            Thread.Sleep(40);
            SendKeysSafe("{DEL}");
            Thread.Sleep(40);
            SendKeysSafe(EscapeForSendKeys(text));
        }

        private static string TryReadValue(AutomationElement edit)
        {
            try
            {
                object pat;
                if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out pat))
                {
                    return ((ValuePattern)pat).Current.Value ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        private static double GetTop(AutomationElement e)
        {
            try { return e.Current.BoundingRectangle.Top; } catch { return double.MaxValue; }
        }

        private static double GetLeft(AutomationElement e)
        {
            try { return e.Current.BoundingRectangle.Left; } catch { return double.MaxValue; }
        }

        private static void SubmitLogin(AutomationElement window, AutomationElement passwordEdit, Action<string> progress)
        {
            var loginBtn = FindLoginSubmitButton(window);
            if (loginBtn != null)
            {
                progress?.Invoke("ログインボタンを押下中...");
                BringWindowForeground(window);
                Thread.Sleep(250);
                if (MouseClickElement(loginBtn))
                {
                    // Give MSⅡ time to process the login before deciding to retry.
                    Thread.Sleep(1500);
                    if (!IsLoginStillVisible(window)) return;
                }
            }

            // Fallback: Enter on password field (default-button submit).
            progress?.Invoke("Enter キーでログイン試行中...");
            BringWindowForeground(window);
            Thread.Sleep(200);
            try { passwordEdit.SetFocus(); } catch { }
            Thread.Sleep(150);
            SendKeysSafe("{ENTER}");
        }

        private static AutomationElement FindLoginSubmitButton(AutomationElement window)
        {
            try
            {
                var buttons = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                                    .Cast<AutomationElement>()
                                    .Where(b => b.Current.IsEnabled && !b.Current.IsOffscreen)
                                    .ToList();

                // Tight exact matches (avoids "ログインID保存" / "ログインにお困りの方" / etc.)
                var exactNames = new[] { "ログイン", "ログインする", "Login", "LOGIN", "Log In", "サインイン", "Sign In", "OK", "ＯＫ" };
                var target = buttons.FirstOrDefault(b => exactNames.Any(n =>
                    string.Equals((SafeName(b) ?? string.Empty).Trim(), n, StringComparison.OrdinalIgnoreCase)));
                if (target != null) return target;

                // Loose fallback that explicitly excludes known non-submit labels.
                target = buttons.FirstOrDefault(b =>
                {
                    var name = (SafeName(b) ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(name)) return false;
                    if (name.Contains("保存") || name.Contains("お困り") || name.Contains("開設") || name.Contains("設定") || name.Contains("ビジター")) return false;
                    return name.StartsWith("ログイン", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Login", StringComparison.OrdinalIgnoreCase);
                });
                return target;
            }
            catch
            {
                return null;
            }
        }

        private static bool ClickLoginButton(AutomationElement window)
        {
            try
            {
                var buttons = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                                    .Cast<AutomationElement>()
                                    .Where(b => b.Current.IsEnabled && !b.Current.IsOffscreen)
                                    .ToList();

                var matchers = new[] { "ログイン", "login", "スタート", "start", "接続", "ＯＫ", "OK" };

                AutomationElement target = null;
                foreach (var m in matchers)
                {
                    target = buttons.FirstOrDefault(b => (SafeName(b) ?? string.Empty).IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (target != null) break;
                }

                if (target == null)
                {
                    // Heuristic fallback: enabled named button furthest to the right (the primary action)
                    target = buttons
                        .Where(b => !string.IsNullOrEmpty(SafeName(b)))
                        .OrderByDescending(b => b.Current.BoundingRectangle.Right)
                        .FirstOrDefault();
                }

                if (target == null) return false;

                // Strategy A: InvokePattern (some custom WPF buttons silently no-op on this).
                object patternObj;
                if (target.TryGetCurrentPattern(InvokePattern.Pattern, out patternObj))
                {
                    try { ((InvokePattern)patternObj).Invoke(); } catch { }
                    Thread.Sleep(400);
                    if (!IsLoginStillVisible(window)) return true;
                }

                // Strategy B: physical mouse click at the button's clickable point (most reliable).
                if (MouseClickElement(target))
                {
                    Thread.Sleep(400);
                    if (!IsLoginStillVisible(window)) return true;
                }

                // Strategy C: focus + space key
                try { target.SetFocus(); } catch { }
                Thread.Sleep(80);
                SendKeysSafe(" ");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool MouseClickElement(AutomationElement element)
        {
            try
            {
                System.Windows.Point pt;
                bool gotPoint = false;
                try { gotPoint = element.TryGetClickablePoint(out pt); } catch { pt = new System.Windows.Point(); }

                if (!gotPoint || (pt.X == 0 && pt.Y == 0))
                {
                    var rect = element.Current.BoundingRectangle;
                    if (rect.IsEmpty) return false;
                    pt = new System.Windows.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
                }

                // Re-foreground the owning window right before the click — focus may have shifted.
                var parent = TreeWalker.ControlViewWalker.GetParent(element);
                while (parent != null && parent.Current.ControlType != ControlType.Window)
                {
                    parent = TreeWalker.ControlViewWalker.GetParent(parent);
                }
                if (parent != null) BringWindowForeground(parent);
                Thread.Sleep(180);

                SetCursorPos((int)pt.X, (int)pt.Y);
                Thread.Sleep(120);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(60);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLoginStillVisible(AutomationElement window)
        {
            try
            {
                // Accessing properties throws when the element is no longer valid.
                var _ = window.Current.Name;
                var edits = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                foreach (AutomationElement e in edits)
                {
                    if (e.Current.IsPassword && !e.Current.IsOffscreen) return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeName(AutomationElement e)
        {
            try { return e.Current.Name ?? string.Empty; } catch { return string.Empty; }
        }

        private static string SafeClassName(AutomationElement e)
        {
            try { return e.Current.ClassName ?? string.Empty; } catch { return string.Empty; }
        }

        private static void SendKeysSafe(string keys)
        {
            try { System.Windows.Forms.SendKeys.SendWait(keys); } catch { }
        }

        private static string EscapeForSendKeys(string text)
        {
            // SendKeys treats + ^ % ~ ( ) { } [ ] as control characters; escape them.
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '+': case '^': case '%': case '~':
                    case '(': case ')': case '{': case '}':
                    case '[': case ']':
                        sb.Append('{').Append(ch).Append('}');
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
