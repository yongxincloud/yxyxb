using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using Yxy.Contracts.Dto;

namespace Yxy.Baidu
{
    public static class BaiduOAuth
    {
        public static string AuthorizeUrl(BaiduAppConfig cfg, string state)
        {
            if (cfg == null)
            {
                throw new ArgumentNullException("cfg");
            }

            return "https://openapi.baidu.com/oauth/2.0/authorize"
                + "?response_type=code"
                + "&client_id=" + Uri.EscapeDataString(cfg.AppKey ?? "")
                + "&redirect_uri=" + Uri.EscapeDataString(cfg.RedirectUri ?? "")
                + "&scope=basic,netdisk"
                + "&qrcode=1"
                + "&state=" + Uri.EscapeDataString(state ?? "");
        }

        public static bool TryParseCallback(string url, string expectedState, out string code, out string error)
        {
            code = null;
            error = null;
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return false;
            }

            var query = HttpUtility.ParseQueryString(uri.Query);
            error = query["error"];
            var state = query["state"];
            code = query["code"];
            if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                code = null;
                return false;
            }

            return !string.IsNullOrEmpty(code);
        }

        public static void ApplyTokenJson(CloudAccountDto account, string json)
        {
            if (account == null)
            {
                throw new ArgumentNullException("account");
            }

            var map = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json ?? "")
                ?? new Dictionary<string, object>();
            object at;
            if (!map.TryGetValue("access_token", out at) || at == null || string.IsNullOrEmpty(at.ToString()))
            {
                throw new InvalidOperationException("请重新授权");
            }

            account.AccessToken = at.ToString();
            object rt;
            if (map.TryGetValue("refresh_token", out rt) && rt != null && !string.IsNullOrEmpty(rt.ToString()))
            {
                account.RefreshToken = rt.ToString();
            }

            object exp;
            var seconds = 0;
            if (map.TryGetValue("expires_in", out exp) && exp != null)
            {
                int.TryParse(exp.ToString(), out seconds);
            }

            account.TokenExpiryUtc = DateTime.UtcNow.AddSeconds(seconds > 0 ? seconds : 3600);
        }

        public static CloudAccountDto CompleteInBrowser(BaiduAppConfig cfg, int timeoutMs, Action<string> openUrl)
        {
            if (cfg == null)
            {
                throw new ArgumentNullException("cfg");
            }

            var prefix = cfg.RedirectUri;
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = BaiduAppConfig.DefaultRedirectUri;
            }

            if (!prefix.EndsWith("/", StringComparison.Ordinal))
            {
                prefix += "/";
            }

            var state = Guid.NewGuid().ToString("N");
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                throw new InvalidOperationException("端口被占用");
            }

            try
            {
                var authUrl = AuthorizeUrl(cfg, state);
                if (openUrl != null)
                {
                    openUrl(authUrl);
                }
                else
                {
                    Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
                }

                var wait = timeoutMs <= 0 ? 180000 : timeoutMs;
                var ar = listener.BeginGetContext(null, null);
                if (!ar.AsyncWaitHandle.WaitOne(wait))
                {
                    throw new InvalidOperationException("授权超时");
                }

                var ctx = listener.EndGetContext(ar);
                var html = Encoding.UTF8.GetBytes("<html><body>可以关闭此页</body></html>");
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.OutputStream.Write(html, 0, html.Length);
                ctx.Response.Close();
                string code;
                string error;
                if (!TryParseCallback(ctx.Request.Url == null ? "" : ctx.Request.Url.ToString(), state, out code, out error)
                    || string.IsNullOrEmpty(code))
                {
                    throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "授权失败" : error);
                }

                var tokenUrl = "https://openapi.baidu.com/oauth/2.0/token?grant_type=authorization_code"
                    + "&code=" + Uri.EscapeDataString(code)
                    + "&client_id=" + Uri.EscapeDataString(cfg.AppKey ?? "")
                    + "&client_secret=" + Uri.EscapeDataString(cfg.AppSecret ?? "")
                    + "&redirect_uri=" + Uri.EscapeDataString(prefix);
                var http = new BaiduHttp();
                var json = http.Call("POST", tokenUrl, null, Encoding.UTF8.GetBytes(""), "application/x-www-form-urlencoded");
                var acc = new CloudAccountDto();
                ApplyTokenJson(acc, json);
                try
                {
                    var uinfo = http.Call("GET", "https://pan.baidu.com/rest/2.0/xpan/nas?method=uinfo", acc.AccessToken, null, null);
                    var map = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(uinfo ?? "")
                        ?? new Dictionary<string, object>();
                    object name;
                    if (map.TryGetValue("baidu_name", out name) && name != null && !string.IsNullOrEmpty(name.ToString()))
                    {
                        acc.DisplayName = name.ToString();
                    }
                    else if (map.TryGetValue("netdisk_name", out name) && name != null)
                    {
                        acc.DisplayName = name.ToString();
                    }
                }
                catch (Exception)
                {
                }

                return acc;
            }
            finally
            {
                listener.Close();
            }
        }
    }
}
