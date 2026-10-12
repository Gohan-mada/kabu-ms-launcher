using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace KabuMSLauncher.Services
{
    public class NotifyResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = string.Empty;   // サーバーの error（bad_email / wrong_code / too_many など）か、通信の失敗
        public string Token { get; set; } = string.Empty;
        public bool NetworkFailed { get; set; }
    }

    // かぶ自動LOGIN のメール通知（会社の受け口 notify-worker を通して送る）
    public static class MailNotifier
    {
        private const string BaseUrl = "https://kabu-login-notify.round-disk-87b9.workers.dev";
        private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan RetryLimit = TimeSpan.FromHours(1);

        static MailNotifier()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public static NotifyResult Register(string email) => Post("/v1/register", new Dictionary<string, object> { { "email", email } });

        public static NotifyResult Verify(string email, string code) => Post("/v1/verify", new Dictionary<string, object> { { "email", email }, { "code", code } });

        public static NotifyResult Unregister(string token) => Post("/v1/unregister", new Dictionary<string, object> { { "token", token } });

        public static NotifyResult Send(string token, string kind, string detail) =>
            Post("/v1/notify", new Dictionary<string, object> { { "token", token }, { "kind", kind }, { "detail", detail ?? string.Empty } });

        // 通知は切断の直後に送るので、ネットが戻っていないことがある。届くまで1分おきに最大1時間送り直す
        public static void SendInBackground(string token, string kind, string detail, Action<string> log)
        {
            var thread = new Thread(() =>
            {
                var start = DateTime.UtcNow;
                while (true)
                {
                    var r = Send(token, kind, detail);
                    if (r.Ok) { log?.Invoke("メールで知らせました"); return; }
                    if (!r.NetworkFailed && r.Error != "server")
                    {
                        log?.Invoke("メールを送れませんでした（" + Describe(r.Error) + "）");
                        return;
                    }
                    if (DateTime.UtcNow - start > RetryLimit)
                    {
                        log?.Invoke("1時間送り直しましたが、メールを送れませんでした");
                        return;
                    }
                    Thread.Sleep(RetryInterval);
                }
            }) { IsBackground = true, Name = "MailNotifier" };
            thread.Start();
        }

        public static string Describe(string error)
        {
            switch (error)
            {
                case "bad_email": return "メールアドレスの形が正しくありません";
                case "bad_input": return "確認コードは6けたの数字です";
                case "wrong_code": return "確認コードが違います";
                case "expired": return "確認コードの有効期限（10分）が切れました。もう一度送ってください";
                case "too_many": return "回数の上限に達しました。しばらくしてからお試しください";
                case "bad_token": return "登録が見つかりません。もう一度登録してください";
                case "network": return "通信できませんでした。インターネットの接続を確認してください";
                default: return "サーバーでエラーが起きました（" + error + "）";
            }
        }

        private static NotifyResult Post(string path, Dictionary<string, object> payload)
        {
            var ser = new JavaScriptSerializer();
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.Timeout = 20000;
                req.UserAgent = "KabuMSLauncher/1.1";
                var bytes = Encoding.UTF8.GetBytes(ser.Serialize(payload));
                using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);

                string text;
                try
                {
                    using (var res = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                        text = reader.ReadToEnd();
                }
                catch (WebException ex) when (ex.Response != null)
                {
                    // 4xx/5xx でも本文に error が入っている
                    using (var reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
                        text = reader.ReadToEnd();
                }

                var obj = ser.Deserialize<Dictionary<string, object>>(text);
                var result = new NotifyResult { Ok = obj.ContainsKey("ok") && obj["ok"] is bool && (bool)obj["ok"] };
                if (obj.ContainsKey("error")) result.Error = Convert.ToString(obj["error"]);
                if (obj.ContainsKey("token")) result.Token = Convert.ToString(obj["token"]);
                return result;
            }
            catch (Exception)
            {
                return new NotifyResult { Ok = false, Error = "network", NetworkFailed = true };
            }
        }
    }
}
