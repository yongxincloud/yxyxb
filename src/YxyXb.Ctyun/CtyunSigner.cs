using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Ctyun
{
    public static class CtyunSigner
    {
        public static string Region(string zone)
        {
            var host = CloudEndpoints.Host(zone);
            var dot = host.IndexOf('.');
            if (dot <= 0)
            {
                return host;
            }

            return host.Substring(0, dot);
        }

        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
            }
        }

        public static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                return Hex(sha.ComputeHash(data ?? new byte[0]));
            }
        }

        public static string Authorization(string accessKey, string secretKey, string method, string path, IDictionary<string, string> query, string host, DateTime utc, string payloadHash)
        {
            var stamp = utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
            var day = stamp.Substring(0, 8);
            var region = Region(host);
            var scope = day + "/" + region + "/s3/aws4_request";

            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            headers["host"] = CloudEndpoints.Host(host);
            headers["x-amz-content-sha256"] = payloadHash ?? "";
            headers["x-amz-date"] = stamp;
            var names = new List<string>(headers.Keys);
            names.Sort(StringComparer.Ordinal);
            var signedHeaders = string.Join(";", names.ToArray());
            var canonicalHeaders = new StringBuilder();
            for (var i = 0; i < names.Count; i++)
            {
                canonicalHeaders.Append(names[i]).Append(':').Append(headers[names[i]].Trim()).Append('\n');
            }

            var canonical = (method ?? "GET").ToUpperInvariant() + "\n"
                + (string.IsNullOrEmpty(path) ? "/" : path) + "\n"
                + CanonicalQuery(query) + "\n"
                + canonicalHeaders.ToString()
                + signedHeaders + "\n"
                + (payloadHash ?? "");
            var toSign = "AWS4-HMAC-SHA256\n" + stamp + "\n" + scope + "\n" + Sha256Hex(canonical);
            var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + (secretKey ?? "")), day);
            key = Hmac(key, region);
            key = Hmac(key, "s3");
            key = Hmac(key, "aws4_request");
            var signature = Hex(Hmac(key, toSign));
            return "AWS4-HMAC-SHA256 Credential=" + (accessKey ?? "") + "/" + scope
                + ", SignedHeaders=" + signedHeaders + ", Signature=" + signature;
        }

        public static string CanonicalQuery(IDictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
            {
                return "";
            }

            var pairs = new List<string>();
            foreach (var pair in query)
            {
                pairs.Add(Escape(pair.Key) + "=" + Escape(pair.Value ?? ""));
            }

            pairs.Sort(StringComparer.Ordinal);
            return string.Join("&", pairs.ToArray());
        }

        public static string Query(IDictionary<string, string> query)
        {
            return CanonicalQuery(query);
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? "").Replace("+", "%20").Replace("%7E", "~");
        }

        private static byte[] Hmac(byte[] key, string text)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
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
