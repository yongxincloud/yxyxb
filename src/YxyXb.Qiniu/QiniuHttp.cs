using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using YxyXb.Core;

namespace YxyXb.Qiniu
{
    internal static class QiniuHttp
    {
        public static void EnsureTls12()
        {
            TlsPolicy.Apply();
        }

        public static string UploadHost(string zone)
        {
            if (string.Equals(zone, "z1", StringComparison.OrdinalIgnoreCase))
            {
                return "https://up-z1.qiniup.com";
            }

            if (string.Equals(zone, "z2", StringComparison.OrdinalIgnoreCase))
            {
                return "https://up-z2.qiniup.com";
            }

            if (string.Equals(zone, "na0", StringComparison.OrdinalIgnoreCase))
            {
                return "https://up-na0.qiniup.com";
            }

            if (string.Equals(zone, "as0", StringComparison.OrdinalIgnoreCase))
            {
                return "https://up-as0.qiniup.com";
            }

            return "https://up.qiniup.com";
        }

        public static string UploadToken(string ak, string sk, string bucket)
        {
            var deadline = (long)(DateTime.UtcNow.AddHours(24) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var policy = "{\"scope\":\"" + Escape(bucket) + "\",\"deadline\":" + deadline + "}";
            var encoded = UrlSafe(Encoding.UTF8.GetBytes(policy));
            var sign = UrlSafe(Hmac(sk, encoded));
            return ak + ":" + sign + ":" + encoded;
        }

        public static string ManageToken(string ak, string sk, string pathAndQuery, byte[] body)
        {
            var signing = pathAndQuery + "\n";
            if (body != null && body.Length > 0)
            {
                signing += Encoding.UTF8.GetString(body);
            }

            return "QBox " + ak + ":" + UrlSafe(Hmac(sk, signing));
        }

        public static string RequestJson(string method, string url, string authorization, byte[] body, string contentType)
        {
            EnsureTls12();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            if (!string.IsNullOrEmpty(authorization))
            {
                request.Headers[HttpRequestHeader.Authorization] = authorization;
            }

            if (body != null)
            {
                request.ContentType = string.IsNullOrEmpty(contentType) ? "application/x-www-form-urlencoded" : contentType;
                request.ContentLength = body.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                var detail = "";
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                {
                    using (var reader = new StreamReader(http.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                    {
                        detail = reader.ReadToEnd();
                    }

                    throw new InvalidOperationException("七牛 HTTP " + (int)http.StatusCode + "：" + detail);
                }

                throw new InvalidOperationException("七牛请求失败：" + ex.Message);
            }
        }

        public static void UploadFile(string host, string token, string key, string localPath, int maxKbps)
        {
            EnsureTls12();
            var fileLen = new FileInfo(localPath).Length;
            if (fileLen <= 0)
            {
                UploadForm(host, token, key, localPath);
                return;
            }

            const int blockSize = 4 * 1024 * 1024;
            var count = (int)((fileLen + blockSize - 1) / blockSize);
            var ctx = new string[count];
            var pacer = new BytePacer(maxKbps);
            var gate = new object();
            Exception error = null;
            var mkHost = host;

            System.Threading.Tasks.Parallel.For(0, count, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 2 }, delegate(int i)
            {
                lock (gate)
                {
                    if (error != null)
                    {
                        return;
                    }
                }

                var offset = (long)i * blockSize;
                var size = (int)Math.Min(blockSize, fileLen - offset);
                var buf = new byte[size];
                using (var input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    input.Seek(offset, SeekOrigin.Begin);
                    var got = 0;
                    while (got < size)
                    {
                        var n = input.Read(buf, got, size - got);
                        if (n <= 0)
                        {
                            throw new EndOfStreamException("读取本地包不完整。");
                        }

                        got += n;
                    }
                }

                Exception last = null;
                string one = null;
                string nextHost = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        string usedHost;
                        lock (gate)
                        {
                            usedHost = mkHost;
                        }

                        var json = PostRaw(usedHost.TrimEnd('/') + "/mkblk/" + size, "UpToken " + token, buf, "application/octet-stream", 120000, pacer);
                        var part = FromJson<QiniuBlk>(json);
                        if (part == null || string.IsNullOrEmpty(part.ctx))
                        {
                            throw new InvalidOperationException("分片没有返回 ctx。");
                        }

                        one = part.ctx;
                        if (!string.IsNullOrEmpty(part.host))
                        {
                            nextHost = part.host.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? part.host : "https://" + part.host.TrimStart('/');
                        }

                        last = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        System.Threading.Thread.Sleep(400 * (attempt + 1));
                    }
                }

                if (last != null)
                {
                    lock (gate)
                    {
                        if (error == null)
                        {
                            error = last;
                        }
                    }

                    return;
                }

                ctx[i] = one;
                if (!string.IsNullOrEmpty(nextHost))
                {
                    lock (gate)
                    {
                        mkHost = nextHost;
                    }
                }
            });

            if (error != null)
            {
                throw new InvalidOperationException("七牛分片上传失败：" + error.Message);
            }

            var body = Encoding.UTF8.GetBytes(string.Join(",", ctx));
            PostRaw(mkHost.TrimEnd('/') + "/mkfile/" + fileLen + "/key/" + UrlSafeBase64(key), "UpToken " + token, body, "text/plain", 120000, null);
        }

        private static void UploadForm(string host, string token, string key, string localPath)
        {
            var boundary = "----yxy" + Guid.NewGuid().ToString("N");
            var head = Encoding.UTF8.GetBytes(
                "--" + boundary + "\r\n"
                + "Content-Disposition: form-data; name=\"token\"\r\n\r\n" + token + "\r\n"
                + "--" + boundary + "\r\n"
                + "Content-Disposition: form-data; name=\"key\"\r\n\r\n" + key + "\r\n"
                + "--" + boundary + "\r\n"
                + "Content-Disposition: form-data; name=\"file\"; filename=\"" + Path.GetFileName(localPath) + "\"\r\n"
                + "Content-Type: application/octet-stream\r\n\r\n");
            var tail = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
            var fileLen = new FileInfo(localPath).Length;
            var request = (HttpWebRequest)WebRequest.Create(host);
            request.Method = "POST";
            request.Timeout = 300000;
            request.ReadWriteTimeout = 300000;
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.KeepAlive = false;
            request.ContentLength = head.Length + fileLen + tail.Length;
            using (var output = request.GetRequestStream())
            {
                output.Write(head, 0, head.Length);
                using (var input = File.OpenRead(localPath))
                {
                    input.CopyTo(output);
                }

                output.Write(tail, 0, tail.Length);
            }

            ReadResponse(request);
        }

        private static string PostRaw(string url, string authorization, byte[] body, string contentType, int timeoutMs, BytePacer pacer)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.KeepAlive = true;
            request.Headers[HttpRequestHeader.Authorization] = authorization;
            request.ContentType = contentType;
            request.ContentLength = body == null ? 0 : body.Length;
            using (var output = request.GetRequestStream())
            {
                if (body != null)
                {
                    var off = 0;
                    while (off < body.Length)
                    {
                        var n = Math.Min(32 * 1024, body.Length - off);
                        if (pacer != null)
                        {
                            pacer.Take(n);
                        }

                        output.Write(body, off, n);
                        off += n;
                    }
                }
            }

            return ReadResponse(request);
        }

        private static string ReadResponse(HttpWebRequest request)
        {
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                {
                    var text = reader.ReadToEnd();
                    if ((int)response.StatusCode >= 400)
                    {
                        throw new InvalidOperationException("七牛上传失败：" + text);
                    }

                    return text;
                }
            }
            catch (WebException ex)
            {
                var detail = "";
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                {
                    using (var reader = new StreamReader(http.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                    {
                        detail = reader.ReadToEnd();
                    }
                }

                throw new InvalidOperationException("七牛上传失败：" + (string.IsNullOrEmpty(detail) ? ex.Message : detail));
            }
        }

        public static string UrlSafeBase64(string text)
        {
            return UrlSafe(Encoding.UTF8.GetBytes(text ?? ""));
        }

        public static DateTime FromPutTime(long putTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            try
            {
                var a = epoch.AddTicks(putTime);
                if (a.Year >= 2000 && a.Year < 2100)
                {
                    return a;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            try
            {
                var b = epoch.AddMilliseconds(putTime / 10000d);
                if (b.Year >= 2000 && b.Year < 2100)
                {
                    return b;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            return DateTime.MinValue;
        }

        public static T FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return new JavaScriptSerializer().Deserialize<T>(json);
        }

        private static byte[] Hmac(string sk, string data)
        {
            using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(sk ?? "")))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(data ?? ""));
            }
        }

        private static string UrlSafe(byte[] raw)
        {
            return Convert.ToBase64String(raw).Replace('+', '-').Replace('/', '_');
        }

        private static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class QiniuListResult
    {
        public List<QiniuListItem> items { get; set; }

        public string marker { get; set; }
    }

    internal sealed class QiniuListItem
    {
        public string key { get; set; }

        public long putTime { get; set; }
    }

    internal sealed class QiniuBlk
    {
        public string ctx { get; set; }

        public string host { get; set; }
    }

    internal sealed class BytePacer
    {
        private readonly int _bytesPerSecond;
        private readonly object _gate = new object();
        private int _windowStart;
        private int _used;

        public BytePacer(int maxKbps)
        {
            _bytesPerSecond = maxKbps <= 0 ? 0 : maxKbps * 1024;
        }

        public void Take(int bytes)
        {
            if (_bytesPerSecond <= 0 || bytes <= 0)
            {
                return;
            }

            lock (_gate)
            {
                var now = Environment.TickCount;
                if (_windowStart == 0 || unchecked(now - _windowStart) >= 1000)
                {
                    _windowStart = now;
                    _used = 0;
                }

                _used += bytes;
                if (_used <= _bytesPerSecond)
                {
                    return;
                }

                var wait = 1000 - unchecked(now - _windowStart);
                if (wait > 0 && wait <= 1000)
                {
                    System.Threading.Thread.Sleep(wait);
                }

                _windowStart = Environment.TickCount;
                _used = bytes;
            }
        }
    }
}
