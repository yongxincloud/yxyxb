using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Yxy.Baidu
{
    public sealed class BaiduAppConfig
    {
        public const string DefaultRedirectUri = "http://127.0.0.1:17890/baidu/";
        public const string DefaultAppFolderName = "用心云循备";

        public string AppKey { get; set; }

        public string AppSecret { get; set; }

        public string RedirectUri { get; set; }

        public string AppFolderName { get; set; }

        public static BaiduAppConfig Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new InvalidOperationException("未配置百度应用");
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var cfg = new JavaScriptSerializer().Deserialize<BaiduAppConfig>(json) ?? new BaiduAppConfig();
            if (string.IsNullOrWhiteSpace(cfg.AppKey) || string.IsNullOrWhiteSpace(cfg.AppSecret))
            {
                throw new InvalidOperationException("未配置百度应用");
            }

            if (string.IsNullOrWhiteSpace(cfg.RedirectUri))
            {
                cfg.RedirectUri = DefaultRedirectUri;
            }
            else if (!cfg.RedirectUri.EndsWith("/", StringComparison.Ordinal))
            {
                cfg.RedirectUri += "/";
            }

            if (string.IsNullOrWhiteSpace(cfg.AppFolderName))
            {
                cfg.AppFolderName = DefaultAppFolderName;
            }

            return cfg;
        }
    }
}
