using System;
using System.Web;

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
    }
}
