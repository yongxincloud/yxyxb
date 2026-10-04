using System;
using System.Collections.Generic;
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
    }
}
