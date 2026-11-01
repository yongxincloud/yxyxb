using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace YxyXb.TencentCloud
{
    public static class TencentSigner
    {
        public static string HttpString(string method, string path, IDictionary<string, string> query, IDictionary<string, string> headers)
        {
            string ignored;
            var parameters = Encode(query, out ignored);
            var headerText = Encode(headers, out ignored);
            return (method ?? "GET").ToLowerInvariant() + "\n" + (path ?? "/") + "\n" + parameters + "\n" + headerText + "\n";
        }

        public static string Authorization(string secretId, string secretKey, string method, string path, IDictionary<string, string> query, IDictionary<string, string> headers, string keyTime)
        {
            string paramList;
            string headerList;
            var http = (method ?? "GET").ToLowerInvariant() + "\n"
                + (path ?? "/") + "\n"
                + Encode(query, out paramList) + "\n"
                + Encode(headers, out headerList) + "\n";
            var toSign = "sha1\n" + keyTime + "\n" + Sha1Hex(http) + "\n";
            var signKey = HmacHex(secretKey ?? "", keyTime ?? "");
            var signature = HmacHex(signKey, toSign);
            return "q-sign-algorithm=sha1&q-ak=" + (secretId ?? "")
                + "&q-sign-time=" + keyTime
                + "&q-key-time=" + keyTime
                + "&q-header-list=" + headerList
                + "&q-url-param-list=" + paramList
                + "&q-signature=" + signature;
        }

        public static string Query(IDictionary<string, string> query)
        {
            string ignored;
            return Encode(query, out ignored);
        }

        private static string Encode(IDictionary<string, string> source, out string nameList)
        {
            nameList = "";
            if (source == null || source.Count == 0)
            {
                return "";
            }

            var names = new List<string>();
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                var name = pair.Key.Trim().ToLowerInvariant();
                if (values.ContainsKey(name))
                {
                    continue;
                }

                names.Add(name);
                values[name] = pair.Value ?? "";
            }

            names.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder();
            var list = new StringBuilder();
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('&');
                    list.Append(';');
                }

                builder.Append(names[i]).Append('=').Append(Uri.EscapeDataString(values[names[i]]));
                list.Append(names[i]);
            }

            nameList = list.ToString();
            return builder.ToString();
        }

        private static string Sha1Hex(string text)
        {
            using (var sha = SHA1.Create())
            {
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
            }
        }

        private static string HmacHex(string key, string text)
        {
            using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key ?? "")))
            {
                return Hex(hmac.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
            }
        }

        private static string Hex(byte[] data)
        {
            var builder = new StringBuilder(data.Length * 2);
            for (var i = 0; i < data.Length; i++)
            {
                builder.Append(data[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
