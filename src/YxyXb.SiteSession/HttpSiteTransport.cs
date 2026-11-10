using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace YxyXb.SiteSession
{
    public sealed class HttpSiteTransport : ISiteHttpTransport
    {
        private readonly Uri _baseUri;

        public HttpSiteTransport(Uri baseUri)
        {
            if (baseUri == null)
            {
                throw new ArgumentNullException("baseUri");
            }

            _baseUri = baseUri;
        }

        public SiteHttpResponse Post(string path, IDictionary<string, object> payload)
        {
            TlsPolicy.Apply();
            var json = new JavaScriptSerializer().Serialize(payload ?? new Dictionary<string, object>());
            var bytes = Encoding.UTF8.GetBytes(json);
            var url = _baseUri.ToString().TrimEnd('/') + path;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.KeepAlive = false;
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                {
                    return new SiteHttpResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        Body = reader.ReadToEnd()
                    };
                }
            }
            catch (WebException ex)
            {
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                {
                    using (http)
                    using (var reader = new StreamReader(http.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                    {
                        return new SiteHttpResponse
                        {
                            StatusCode = (int)http.StatusCode,
                            Body = reader.ReadToEnd()
                        };
                    }
                }

                throw new InvalidOperationException("无法连接网站 " + _baseUri + "：" + RootMessage(ex));
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException)
                {
                    throw;
                }

                throw new InvalidOperationException("无法连接网站 " + _baseUri + "：" + RootMessage(ex));
            }
        }

        private static string RootMessage(Exception ex)
        {
            var current = ex;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.Message;
        }

        private static class TlsPolicy
        {
            public static void Apply()
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
        }
    }
}
