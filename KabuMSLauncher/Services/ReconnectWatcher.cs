using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace KabuMSLauncher.Services
{
    // MSⅡの「サーバとの通信が切断されました。再接続します。」画面を見張り、OKを押して再接続する。
    // 本文は画面に直接描かれていて外から読めない（2026-10-12 実測）ため、次の3つが揃ったときだけ切断と判断する。
    //   1. 題名「MARKETSPEED II」の画面に、OK と ｷｬﾝｾﾙ の2つが見えている（ログイン直後のお知らせは OK だけ）
    //   2. その画面が出る直前まで、メイン画面「MarketSpeed2」が開いていた（起動時の更新確認などを除くため）
    //   3. OKを押す時点でメイン画面が消えている（ログアウト確認など、メイン画面がある状態の画面を押さないため）
    // 再接続できなかったときはリトライしない（三澤さん決定 2026-10-12）。
    public class ReconnectWatcher
    {
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, StringBuilder l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        private delegate bool EnumProc(IntPtr h, IntPtr l);

        private const uint WM_GETTEXT = 0x000D;
        private const uint WM_COMMAND = 0x0111;
        private const uint BM_CLICK = 0x00F5;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        private const string DialogTitle = "MARKETSPEED II";
        private const string MainTitle = "MarketSpeed2";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MainSeenWindow = TimeSpan.FromSeconds(20);   // 条件2：画面が出る何秒前までメイン画面が見えていればよいか
        private static readonly TimeSpan InternetWaitLimit = TimeSpan.FromMinutes(10); // ネットが戻らなくても、この時間で1回だけ押す
        private static readonly TimeSpan MainReturnLimit = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan AfterMainReturnDelay = TimeSpan.FromSeconds(10); // ログイン処理が落ち着くのを待つ

        private readonly Func<bool> _pressOrderButton;
        private Thread _thread;
        private volatile bool _running;
        private DateTime _lastMainSeenUtc = DateTime.MinValue;

        // 画面の表示用（メッセージ, 異常か）
        public event Action<string, bool> StatusChanged;
        // 1回の再接続が終わったとき（成功か, 経過の要約）。メール通知はここにつなぐ。
        public event Action<bool, string> SequenceFinished;

        public ReconnectWatcher(Func<bool> pressOrderButton)
        {
            _pressOrderButton = pressOrderButton;
        }

        public bool IsRunning => _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ReconnectWatcher" };
            _thread.Start();
            Log("見守りを開始しました");
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            Log("見守りを止めました");
        }

        private void Loop()
        {
            DateTime? dialogFirstSeenUtc = null;
            while (_running)
            {
                try
                {
                    var pids = MarketSpeedPids();
                    var tops = pids.Length == 0 ? new List<IntPtr>() : TopWindows(pids);
                    var main = tops.FirstOrDefault(h => WindowText(h) == MainTitle);
                    var now = DateTime.UtcNow;
                    if (main != IntPtr.Zero) _lastMainSeenUtc = now;

                    var dialog = tops.FirstOrDefault(IsDisconnectDialog);
                    if (dialog == IntPtr.Zero)
                    {
                        dialogFirstSeenUtc = null;
                    }
                    else
                    {
                        if (dialogFirstSeenUtc == null) dialogFirstSeenUtc = now;
                        bool mainWasOpenJustBefore = _lastMainSeenUtc != DateTime.MinValue
                            && dialogFirstSeenUtc.Value - _lastMainSeenUtc <= MainSeenWindow;
                        if (mainWasOpenJustBefore && main == IntPtr.Zero)
                        {
                            RunSequence(dialog, pids);
                            dialogFirstSeenUtc = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("見守り中に例外：" + ex.Message, true);
                }
                Thread.Sleep(PollInterval);
            }
        }

        private void RunSequence(IntPtr dialog, uint[] pids)
        {
            var summary = new StringBuilder();
            Action<string, bool> step = (msg, isError) => { summary.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  " + msg); Log(msg, isError); };

            step("通信が切断されました。インターネットが戻るのを待ちます", true);

            var waitStart = DateTime.UtcNow;
            while (_running && !IsInternetUp())
            {
                if (DateTime.UtcNow - waitStart > InternetWaitLimit)
                {
                    step("インターネットが10分戻りませんでした。OKを1回だけ押します", true);
                    break;
                }
                if (!IsWindow(dialog)) { step("切断の画面が閉じられました（手で押された可能性があります）", false); Finish(false, summary); return; }
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
            if (!_running) return;

            // ネット待ちの間に状況が変わっていたら押さない
            var tops = TopWindows(MarketSpeedPids());
            if (!IsWindow(dialog) || !IsDisconnectDialog(dialog))
            {
                step("切断の画面が閉じられていたので、OKは押しませんでした", false);
            }
            else if (tops.Any(h => WindowText(h) == MainTitle))
            {
                step("メイン画面が開いているので、OKは押しませんでした", true);
                Finish(false, summary);
                return;
            }
            else if (!PressOk(dialog))
            {
                step("OKを押せませんでした。手で押してください", true);
                Finish(false, summary);
                return;
            }
            else
            {
                step("OKを押しました。再接続を待っています", false);
            }

            // メイン画面が開き直るまで待つ（2026-10-12 の実測ではOKから12〜16秒）
            var mainWaitStart = DateTime.UtcNow;
            while (_running && !TopWindows(MarketSpeedPids()).Any(h => WindowText(h) == MainTitle))
            {
                if (DateTime.UtcNow - mainWaitStart > MainReturnLimit)
                {
                    step("3分待ってもMarketspeedⅡが再接続しませんでした。手で確認してください", true);
                    Finish(false, summary);
                    return;
                }
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            if (!_running) return;
            _lastMainSeenUtc = DateTime.UtcNow;
            step("MarketspeedⅡが再接続しました", false);

            Thread.Sleep(AfterMainReturnDelay);

            // Excel の楽天RSS「未接続」は必ず押す
            var rss = ExcelRssButtons.PressIfShowing("未接続");
            step(rss.Message, !rss.Ok);
            if (!rss.Ok) { Finish(false, summary); return; }

            // 「発注不可」は設定で選んだ人だけ
            if (_pressOrderButton())
            {
                var order = ExcelRssButtons.PressIfShowing("発注不可");
                step(order.Message, !order.Ok);
                Finish(order.Ok, summary);
            }
            else
            {
                step("「発注不可」は設定により押していません。必要なら手で押してください", false);
                Finish(true, summary);
            }
        }

        private void Finish(bool ok, StringBuilder summary)
        {
            try { SequenceFinished?.Invoke(ok, summary.ToString()); } catch { }
        }

        // ---- 判定 ----

        private static bool IsDisconnectDialog(IntPtr h)
        {
            if (!IsWindowVisible(h)) return false;
            if (WindowText(h) != DialogTitle) return false;
            bool ok = false, cancel = false;
            foreach (var c in Children(h))
            {
                if (!IsWindowVisible(c) || ClassName(c) != "Button") continue;
                var t = WindowText(c).Trim();
                if (t == "OK") ok = true;
                else if (t == "ｷｬﾝｾﾙ" || t == "キャンセル" || t.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) cancel = true;
            }
            return ok && cancel;
        }

        private static bool PressOk(IntPtr dialog)
        {
            var okButton = Children(dialog).FirstOrDefault(c => IsWindowVisible(c) && ClassName(c) == "Button" && WindowText(c).Trim() == "OK");
            if (okButton == IntPtr.Zero) return false;

            // 1. ボタンへ直接クリックの合図（マウスを動かさない）
            IntPtr r;
            SendMessageTimeout(okButton, BM_CLICK, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 5000, out r);
            if (WaitClosed(dialog, TimeSpan.FromSeconds(3))) return true;

            // 2. 親へ「OKが押された」の通知を直接送る
            var parent = GetParent(okButton);
            if (parent != IntPtr.Zero)
            {
                var wParam = (IntPtr)(GetDlgCtrlID(okButton) & 0xFFFF); // 上位16bit＝BN_CLICKED(0)
                SendMessageTimeout(parent, WM_COMMAND, wParam, okButton, SMTO_ABORTIFHUNG, 5000, out r);
                if (WaitClosed(dialog, TimeSpan.FromSeconds(3))) return true;
            }
            return false;
        }

        private static bool WaitClosed(IntPtr h, TimeSpan limit)
        {
            var end = DateTime.UtcNow + limit;
            while (DateTime.UtcNow < end)
            {
                if (!IsWindow(h) || !IsWindowVisible(h)) return true;
                Thread.Sleep(200);
            }
            return false;
        }

        private static bool IsInternetUp()
        {
            if (!NetworkInterface.GetIsNetworkAvailable()) return false;
            // ICMP を止めているネットワークもあるので、ping と 443番への接続のどちらかが通ればよい
            foreach (var ip in new[] { "8.8.8.8", "1.1.1.1" })
            {
                try
                {
                    using (var p = new Ping())
                    {
                        if (p.Send(ip, 1500).Status == IPStatus.Success) return true;
                    }
                }
                catch { }
            }
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect("www.rakuten-sec.co.jp", 443, null, null);
                    if (ar.AsyncWaitHandle.WaitOne(3000) && client.Connected) return true;
                }
            }
            catch { }
            return false;
        }

        // ---- Win32 の小道具 ----

        private static uint[] MarketSpeedPids()
        {
            try
            {
                return Process.GetProcessesByName("MarketSpeed2").Select(p => (uint)p.Id).ToArray();
            }
            catch { return new uint[0]; }
        }

        private static List<IntPtr> TopWindows(uint[] pids)
        {
            var set = new HashSet<uint>(pids);
            var list = new List<IntPtr>();
            EnumWindows((h, l) =>
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (set.Contains(pid) && IsWindowVisible(h)) list.Add(h);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        private static List<IntPtr> Children(IntPtr parent)
        {
            var list = new List<IntPtr>();
            EnumChildWindows(parent, (h, l) => { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        private static string WindowText(IntPtr h)
        {
            var sb = new StringBuilder(256);
            IntPtr r;
            SendMessageTimeout(h, WM_GETTEXT, (IntPtr)256, sb, SMTO_ABORTIFHUNG, 1000, out r);
            return sb.ToString();
        }

        private static string ClassName(IntPtr h)
        {
            var sb = new StringBuilder(128);
            GetClassName(h, sb, 128);
            return sb.ToString();
        }

        // ---- 記録 ----

        public static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KabuMSLauncher", "reconnect_log.txt");

        private void Log(string message, bool isError = false)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            try { StatusChanged?.Invoke(message, isError); } catch { }
        }
    }

    public class RssPressResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // Excel の楽天RSSボタン（クイックアクセスツールバー、またはリボンの「マーケットスピード II」タブ）を押す。
    // Excel を COM で触ると楽天RSSが止まるため、画面の部品（UI Automation）として押すだけにする。
    public static class ExcelRssButtons
    {
        private const string QatName = "クイック アクセス ツール バー";

        // 押したあとの表示（2026-10-12 実測）
        private static readonly Dictionary<string, string> OnLabels = new Dictionary<string, string>
        {
            { "未接続", "接続中" },
            { "発注不可", "発注可" },
        };

        public static RssPressResult PressIfShowing(string label)
        {
            var button = FindButton(label);
            string onLabel;
            if (button == null && OnLabels.TryGetValue(label, out onLabel) && FindButton(onLabel) != null)
            {
                return new RssPressResult { Ok = true, Message = "Excelはすでに「" + onLabel + "」になっていました" };
            }
            if (button == null)
            {
                // すでに切り替わっている（例：他の人が手で押した）か、ボタンが見当たらない
                return new RssPressResult
                {
                    Ok = false,
                    Message = "Excelに「" + label + "」のボタンが見つかりませんでした。Excelの表示を確認してください"
                };
            }

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                Press(button);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                if (FindButton(label) == null)
                {
                    return new RssPressResult { Ok = true, Message = "Excelの「" + label + "」を押しました" };
                }
                Thread.Sleep(TimeSpan.FromSeconds(10));
                button = FindButton(label);
                if (button == null) return new RssPressResult { Ok = true, Message = "Excelの「" + label + "」を押しました" };
            }
            return new RssPressResult { Ok = false, Message = "Excelの「" + label + "」を押しましたが、表示が変わりませんでした。手で確認してください" };
        }

        private static void Press(AutomationElement button)
        {
            object pat;
            if (button.TryGetCurrentPattern(TogglePattern.Pattern, out pat))
            {
                try { ((TogglePattern)pat).Toggle(); return; } catch { }
            }
            if (button.TryGetCurrentPattern(InvokePattern.Pattern, out pat))
            {
                try { ((InvokePattern)pat).Invoke(); } catch { }
            }
        }

        private static AutomationElement FindButton(string label)
        {
            Process[] excels;
            try { excels = Process.GetProcessesByName("EXCEL"); } catch { return null; }
            foreach (var p in excels)
            {
                AutomationElementCollection windows;
                try
                {
                    windows = AutomationElement.RootElement.FindAll(TreeScope.Children, new AndCondition(
                        new PropertyCondition(AutomationElement.ProcessIdProperty, p.Id),
                        new PropertyCondition(AutomationElement.ClassNameProperty, "XLMAIN")));
                }
                catch { continue; }

                foreach (AutomationElement w in windows)
                {
                    try
                    {
                        // まずクイックアクセスツールバーだけを探す（シート全体を探すと重い）
                        var qat = w.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, QatName));
                        var hit = FindIn(qat, label);
                        if (hit != null) return hit;
                        // 次にリボン（「マーケットスピード II」タブを開いている人向け）
                        var ribbon = w.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "リボン"));
                        hit = FindIn(ribbon, label);
                        if (hit != null) return hit;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static AutomationElement FindIn(AutomationElement scope, string label)
        {
            if (scope == null) return null;
            var buttons = scope.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement b in buttons)
            {
                string name;
                try { name = (b.Current.Name ?? string.Empty).Trim(); } catch { continue; }
                if (name == label) return b;
            }
            return null;
        }
    }
}
