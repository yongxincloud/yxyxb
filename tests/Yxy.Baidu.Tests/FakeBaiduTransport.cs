using System;
using System.Collections.Generic;
using System.Text;
using Yxy.Baidu;

namespace Yxy.Baidu.Tests
{
    internal sealed class FakeBaiduTransport : IBaiduTransport
    {
        public readonly List<string> Calls = new List<string>();
        public int RapidReturnType = 1;
        public string ListJson;
        /// <summary>按 list 的 dir 查询参数返回不同 JSON；未命中时回退 ListJson。</summary>
        public Dictionary<string, string> ListJsonByDir;
        public int? FailPartSeq;

        public string Call(string method, string url, string accessToken, byte[] body, string contentType)
        {
            var text = body == null ? "" : Encoding.UTF8.GetString(body);
            Calls.Add((method ?? "") + " " + (url ?? "") + " " + text);
            if (url != null && url.IndexOf("grant_type=refresh_token", StringComparison.Ordinal) >= 0)
            {
                return "{\"access_token\":\"at-new\",\"refresh_token\":\"rt-new\",\"expires_in\":2592000}";
            }

            if (url != null && url.IndexOf("method=precreate", StringComparison.Ordinal) >= 0)
            {
                return "{\"errno\":0,\"uploadid\":\"u1\",\"return_type\":" + RapidReturnType + "}";
            }

            if (url != null && url.IndexOf("superfile2", StringComparison.Ordinal) >= 0)
            {
                if (FailPartSeq.HasValue && url.IndexOf("partseq=" + FailPartSeq.Value, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException("fake part fail");
                }

                return "{\"errno\":0}";
            }

            if (url != null && url.IndexOf("method=create", StringComparison.Ordinal) >= 0)
            {
                return "{\"errno\":0}";
            }

            if (url != null && url.IndexOf("method=list", StringComparison.Ordinal) >= 0)
            {
                if (ListJsonByDir != null)
                {
                    var dir = ExtractQuery(url, "dir");
                    string byDir;
                    if (!string.IsNullOrEmpty(dir) && ListJsonByDir.TryGetValue(Uri.UnescapeDataString(dir), out byDir))
                    {
                        return byDir;
                    }
                }

                return ListJson ?? "{\"errno\":0,\"list\":[]}";
            }

            if (url != null && url.IndexOf("filemanager", StringComparison.Ordinal) >= 0)
            {
                return "{\"errno\":0}";
            }

            if (url != null && url.IndexOf("method=uinfo", StringComparison.Ordinal) >= 0)
            {
                return "{\"errno\":0,\"baidu_name\":\"张三\"}";
            }

            return "{\"errno\":0}";
        }

        private static string ExtractQuery(string url, string key)
        {
            var marker = key + "=";
            var i = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return null;
            }

            var start = i + marker.Length;
            var end = url.IndexOf('&', start);
            return end < 0 ? url.Substring(start) : url.Substring(start, end - start);
        }
    }
}
