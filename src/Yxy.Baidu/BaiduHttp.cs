using System;
using System.IO;
using System.Net;
using System.Text;

namespace Yxy.Baidu
{
    internal sealed class BaiduHttp : IBaiduTransport
    {
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

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = string.IsNullOrEmpty(method) ? "GET" : method;
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
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
    }
}
