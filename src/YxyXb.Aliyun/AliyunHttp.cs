using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using YxyXb.Core;

namespace YxyXb.Aliyun
{
    public sealed class AliyunExchange
    {
        public string Method { get; set; }

        public string Url { get; set; }

        public string Resource { get; set; }

        public string ContentType { get; set; }

        public string ContentMd5 { get; set; }

        public byte[] Body { get; set; }

        public string Date { get; set; }

        public string Authorization { get; set; }

        public Dictionary<string, string> OssHeaders { get; set; }
    }

    public sealed class AliyunReply
    {
        public int Status { get; set; }

        public string Body { get; set; }

        public string ETag { get; set; }

        public long ContentLength { get; set; }
    }

    public interface IAliyunTransport
    {
        AliyunReply Send(AliyunExchange call);
    }

    public sealed class AliyunHttp : IAliyunTransport
    {
        public int MaxKbps { get; set; }

        public AliyunReply Send(AliyunExchange call)
        {
            TlsPolicy.Apply();
            if (ServicePointManager.DefaultConnectionLimit < 8)
            {
                ServicePointManager.DefaultConnectionLimit = 8;
            }

            var request = (HttpWebRequest)WebRequest.Create(call.Url);
            request.Method = call.Method ?? "GET";
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
            request.KeepAlive = false;
            request.ProtocolVersion = HttpVersion.Version11;
            request.ServicePoint.Expect100Continue = false;
            if (request.ServicePoint.ConnectionLimit < 8)
            {
                request.ServicePoint.ConnectionLimit = 8;
            }

            DateTime when;
            if (DateTime.TryParseExact(call.Date, "ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out when))
            {
                request.Date = when.ToUniversalTime();
            }

            if (!string.IsNullOrEmpty(call.ContentType))
            {
                request.ContentType = call.ContentType;
            }

            if (!string.IsNullOrEmpty(call.ContentMd5))
            {
                request.Headers["Content-MD5"] = call.ContentMd5;
            }

            if (!string.IsNullOrEmpty(call.Authorization))
            {
                request.Headers[HttpRequestHeader.Authorization] = call.Authorization;
            }

            if (call.OssHeaders != null)
            {
                foreach (var pair in call.OssHeaders)
                {
                    request.Headers[pair.Key] = pair.Value;
                }
            }

            var body = call.Body;
            if (body != null && body.Length > 0)
            {
                request.ContentLength = body.Length;
                var pacer = new Pacer(MaxKbps);
                using (var output = request.GetRequestStream())
                {
                    var offset = 0;
                    while (offset < body.Length)
                    {
                        var count = Math.Min(32 * 1024, body.Length - offset);
                        pacer.Take(count);
                        output.Write(body, offset, count);
                        offset += count;
                    }
                }
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return Read(response);
                }
            }
            catch (WebException ex)
            {
                var http = ex.Response as HttpWebResponse;
                if (http == null)
                {
                    throw new InvalidOperationException("阿里云请求失败：" + ex.Message);
                }

                using (http)
                {
                    return Read(http);
                }
            }
        }

        private static AliyunReply Read(HttpWebResponse response)
        {
            var text = "";
            var stream = response.GetResponseStream();
            if (stream != null)
            {
                using (stream)
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                }
            }

            return new AliyunReply
            {
                Status = (int)response.StatusCode,
                Body = text,
                ETag = response.Headers["ETag"],
                ContentLength = response.ContentLength
            };
        }

        private sealed class Pacer
        {
            private readonly int _bytesPerSecond;
            private readonly object _gate = new object();
            private int _windowStart;
            private int _used;

            public Pacer(int maxKbps)
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
                    _used = 0;
                }
            }
        }
    }
}
