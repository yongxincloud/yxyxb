using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace YxyXb.Aliyun
{
    public static class AliyunSigner
    {
        public static string Canonical(string method, string contentMd5, string contentType, string date, IEnumerable<KeyValuePair<string, string>> ossHeaders, string resource)
        {
            var builder = new StringBuilder();
            builder.Append(method ?? "GET").Append('\n');
            builder.Append(contentMd5 ?? "").Append('\n');
            builder.Append(contentType ?? "").Append('\n');
            builder.Append(date ?? "").Append('\n');
            var lines = new List<string>();
            if (ossHeaders != null)
            {
                foreach (var pair in ossHeaders)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                    {
                        continue;
                    }

                    var name = pair.Key.Trim().ToLowerInvariant();
                    if (!name.StartsWith("x-oss-", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    lines.Add(name + ":" + (pair.Value ?? "").Trim());
                }
            }

            lines.Sort(StringComparer.Ordinal);
            for (var i = 0; i < lines.Count; i++)
            {
                builder.Append(lines[i]).Append('\n');
            }

            builder.Append(resource ?? "");
            return builder.ToString();
        }

        public static string Authorization(string accessKeyId, string accessKeySecret, string canonical)
        {
            using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(accessKeySecret ?? "")))
            {
                var sign = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical ?? "")));
                return "OSS " + (accessKeyId ?? "") + ":" + sign;
            }
        }

        private static readonly string[] SubResources = new[]
        {
            "acl", "uploads", "uploadId", "partNumber", "location", "logging",
            "website", "referer", "lifecycle", "delete", "append", "position",
            "security-token", "objectMeta", "cors", "tagging", "restore",
            "versioning", "versions", "versionId"
        };

        public static string Resource(string bucket, string key, IDictionary<string, string> query)
        {
            var path = "/" + (bucket ?? "") + "/" + (key ?? "").Replace('\\', '/').TrimStart('/');
            if (query == null || query.Count == 0)
            {
                return path;
            }

            var names = new List<string>();
            foreach (var name in query.Keys)
            {
                if (IsSubResource(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count == 0)
            {
                return path;
            }

            names.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder(path);
            builder.Append('?');
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('&');
                }

                builder.Append(names[i]);
                var value = query[names[i]];
                if (value != null && value.Length > 0)
                {
                    builder.Append('=').Append(Uri.EscapeDataString(value));
                }
            }

            return builder.ToString();
        }

        private static bool IsSubResource(string name)
        {
            for (var i = 0; i < SubResources.Length; i++)
            {
                if (string.Equals(SubResources[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string FormatDate(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Md5Base64(byte[] body)
        {
            using (var md5 = MD5.Create())
            {
                return Convert.ToBase64String(md5.ComputeHash(body ?? new byte[0]));
            }
        }
    }
}
