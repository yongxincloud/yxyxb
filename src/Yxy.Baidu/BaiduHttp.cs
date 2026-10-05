using System;
using System.IO;
using System.Net;
using System.Text;

namespace Yxy.Baidu
{
    internal sealed class BaiduHttp : IBaiduTransport
    {
        public int TimeoutMs { get; set; }

        public BytePacer Pacer { get; set; }

        public static void EnsureTls12()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            }
            catch (NotSupportedException)
            {
            }
        }

        public string Call(string method, string url, string accessToken, byte[] body, string contentType)
        {
            EnsureTls12();
            if (!string.IsNullOrEmpty(accessToken) && url.IndexOf("access_token=", StringComparison.Ordinal) < 0)
            {
                url += (url.IndexOf('?') >= 0 ? "&" : "?") + "access_token=" + Uri.EscapeDataString(accessToken);
            }

            var timeout = TimeoutMs > 0 ? TimeoutMs : 120000;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = string.IsNullOrEmpty(method) ? "GET" : method;
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;
            if (body != null)
            {
                request.ContentType = string.IsNullOrEmpty(contentType) ? "application/x-www-form-urlencoded" : contentType;
                request.ContentLength = body.Length;
                using (var stream = request.GetRequestStream())
                {
                    WritePaced(stream, body);
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
                var http = ex.Response as HttpWebResponse;
                var detail = "";
                if (http != null)
                {
                    using (var reader = new StreamReader(http.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                    {
                        detail = reader.ReadToEnd();
                    }

                    throw new InvalidOperationException("百度 HTTP " + (int)http.StatusCode + "：" + detail);
                }

                throw new InvalidOperationException("百度请求失败：" + ex.Message);
            }
        }

        private void WritePaced(Stream stream, byte[] body)
        {
            var offset = 0;
            while (offset < body.Length)
            {
                var n = Math.Min(64 * 1024, body.Length - offset);
                if (Pacer != null)
                {
                    Pacer.Take(n);
                }

                stream.Write(body, offset, n);
                offset += n;
            }
        }
    }
}
