using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Cloud;

namespace YxyXb.TencentCloud
{
    public sealed class TencentCloudStorage : ICloudStorage
    {
        private CloudAccountDto _account;
        private readonly ITencentTransport _transport;

        public TencentCloudStorage()
            : this(null, null)
        {
        }

        public TencentCloudStorage(CloudAccountDto account, ITencentTransport transport)
        {
            _account = account;
            _transport = transport ?? new TencentHttp();
            UtcNow = delegate { return DateTime.UtcNow; };
        }

        public Action<string> OnLog { get; set; }

        public Func<DateTime> UtcNow { get; set; }

        public string ProviderKey
        {
            get { return CloudProviders.Tencent; }
        }

        public bool IsImplemented
        {
            get { return true; }
        }

        public void Configure(CloudAccountDto account)
        {
            _account = account;
        }

        public void SetSpeed(int minKbps, int maxKbps)
        {
            var http = _transport as TencentHttp;
            if (http != null)
            {
                http.MaxKbps = maxKbps < 0 ? 0 : maxKbps;
            }
        }

        public void SetParallel(int parallel)
        {
        }

        public void TestConnection()
        {
            Ensure(Send("HEAD", "", null, "", null, false));
        }

        public void Upload(string localPath, string remoteKey)
        {
            Require();
            ObjectUpload.Run(localPath, remoteKey, new Api(this), OnLog, null);
        }

        public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            Require();
            var marker = "";
            var guard = 0;
            var deleted = 0;
            while (guard < 200)
            {
                guard++;
                var query = new Dictionary<string, string>();
                query["max-keys"] = "1000";
                query["prefix"] = prefix ?? "";
                if (!string.IsNullOrEmpty(marker))
                {
                    query["marker"] = marker;
                }

                var reply = Send("GET", "", query, "", null, false);
                Ensure(reply);
                var page = ReadPage(reply.Body);
                for (var i = 0; i < page.Items.Count; i++)
                {
                    var item = page.Items[i];
                    if (item.Key == null || !item.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var time = item.Modified;
                    if (time == DateTime.MinValue)
                    {
                        time = Stamp(item.Key);
                    }

                    if (time == DateTime.MinValue || time >= olderThanUtc)
                    {
                        continue;
                    }

                    Ensure(Send("DELETE", item.Key, null, "", null, false));
                    deleted++;
                    if (onDeleted != null)
                    {
                        onDeleted(item.Key);
                    }
                }

                if (!page.Truncated || string.IsNullOrEmpty(page.NextMarker))
                {
                    break;
                }

                marker = page.NextMarker;
            }

            return deleted;
        }

        private TencentReply Send(string method, string key, IDictionary<string, string> query, string contentType, byte[] body, bool privateAcl)
        {
            var account = Require();
            var path = string.IsNullOrEmpty(key) ? "/" : "/" + key.Replace('\\', '/').TrimStart('/');
            var url = ObjectUrls.Tencent(account.Bucket, account.Zone, key ?? "");
            var queryText = TencentSigner.Query(query);
            if (queryText.Length > 0)
            {
                url += "?" + queryText;
            }

            var headers = new Dictionary<string, string>();
            headers["host"] = HostOf(url);
            if (!string.IsNullOrEmpty(contentType))
            {
                headers["content-type"] = contentType;
            }

            var md5 = body != null && body.Length > 0 ? Md5(body) : "";
            if (md5.Length > 0)
            {
                headers["content-md5"] = md5;
            }

            if (privateAcl)
            {
                headers["x-cos-acl"] = "private";
            }

            var start = (long)(UtcNow().ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var keyTime = start.ToString(CultureInfo.InvariantCulture) + ";" + (start + 3600).ToString(CultureInfo.InvariantCulture);
            var call = new TencentExchange
            {
                Method = method,
                Url = url,
                Path = path,
                ContentType = contentType ?? "",
                ContentMd5 = md5,
                Body = body,
                Authorization = TencentSigner.Authorization(account.AccessKey, account.SecretKey, method, path, query, headers, keyTime),
                Headers = headers
            };
            return _transport.Send(call);
        }

        private static string HostOf(string url)
        {
            var rest = url.Substring("https://".Length);
            var slash = rest.IndexOf('/');
            var host = slash < 0 ? rest : rest.Substring(0, slash);
            var query = host.IndexOf('?');
            if (query >= 0)
            {
                host = host.Substring(0, query);
            }

            return host;
        }

        private static string Md5(byte[] body)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                return Convert.ToBase64String(md5.ComputeHash(body));
            }
        }

        private CloudAccountDto Require()
        {
            if (_account == null || !_account.IsObjectReady)
            {
                throw new InvalidOperationException("请先在「云备份 → 腾讯云」保存桶和密钥。");
            }

            return _account;
        }

        private static void Ensure(TencentReply reply)
        {
            var status = reply == null ? 0 : reply.Status;
            if (status >= 200 && status <= 299)
            {
                return;
            }

            if (status == 403)
            {
                throw new InvalidOperationException("密钥不正确。请检查 SecretId 和 SecretKey，并使用公网地址。");
            }

            if (status == 404)
            {
                throw new InvalidOperationException("桶不存在，或地域、Endpoint 不正确。请使用公网地址。");
            }

            throw new InvalidOperationException("腾讯云请求失败：" + status);
        }

        private static Page ReadPage(string body)
        {
            var page = new Page();
            if (string.IsNullOrEmpty(body))
            {
                return page;
            }

            var doc = XDocument.Parse(body);
            var truncated = doc.Descendants().FirstOrDefault(item => item.Name.LocalName == "IsTruncated");
            page.Truncated = truncated != null && string.Equals(truncated.Value, "true", StringComparison.OrdinalIgnoreCase);
            var next = doc.Descendants().FirstOrDefault(item => item.Name.LocalName == "NextMarker");
            page.NextMarker = next == null ? "" : (next.Value ?? "");
            foreach (var node in doc.Descendants().Where(item => item.Name.LocalName == "Contents"))
            {
                var key = Child(node, "Key");
                var modified = Child(node, "LastModified");
                var when = DateTime.MinValue;
                DateTime parsed;
                if (!string.IsNullOrEmpty(modified) && DateTime.TryParse(modified, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                {
                    when = parsed.ToUniversalTime();
                }

                page.Items.Add(new Item { Key = key, Modified = when });
            }

            if (page.Truncated && string.IsNullOrEmpty(page.NextMarker) && page.Items.Count > 0)
            {
                page.NextMarker = page.Items[page.Items.Count - 1].Key;
            }

            return page;
        }

        private static string Child(XElement node, string name)
        {
            var child = node.Elements().FirstOrDefault(item => item.Name.LocalName == name);
            return child == null ? "" : child.Value;
        }

        private static DateTime Stamp(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return DateTime.MinValue;
            }

            var name = Path.GetFileNameWithoutExtension(key);
            var parts = name.Split('_');
            if (parts.Length == 0)
            {
                return DateTime.MinValue;
            }

            DateTime local;
            if (DateTime.TryParseExact(parts[parts.Length - 1], "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        private sealed class Api : IObjectApi
        {
            private readonly TencentCloudStorage _owner;

            public Api(TencentCloudStorage owner)
            {
                _owner = owner;
            }

            public void PutWhole(string key, byte[] body)
            {
                Ensure(_owner.Send("PUT", key, null, "application/octet-stream", body, true));
            }

            public string Start(string key)
            {
                var query = new Dictionary<string, string>();
                query["uploads"] = "";
                var reply = _owner.Send("POST", key, query, "", null, true);
                Ensure(reply);
                return Between(reply.Body, "UploadId");
            }

            public string PutPart(string key, string uploadId, int number, byte[] body)
            {
                var query = new Dictionary<string, string>();
                query["partNumber"] = number.ToString(CultureInfo.InvariantCulture);
                query["uploadId"] = uploadId ?? "";
                var reply = _owner.Send("PUT", key, query, "application/octet-stream", body, false);
                Ensure(reply);
                return reply.ETag ?? "";
            }

            public void Complete(string key, string uploadId, string[] etags)
            {
                var query = new Dictionary<string, string>();
                query["uploadId"] = uploadId ?? "";
                var xml = new StringBuilder();
                xml.Append("<CompleteMultipartUpload>");
                if (etags != null)
                {
                    for (var i = 0; i < etags.Length; i++)
                    {
                        xml.Append("<Part><PartNumber>").Append(i + 1).Append("</PartNumber><ETag>")
                            .Append(etags[i] ?? "").Append("</ETag></Part>");
                    }
                }

                xml.Append("</CompleteMultipartUpload>");
                Ensure(_owner.Send("POST", key, query, "application/xml", Encoding.UTF8.GetBytes(xml.ToString()), false));
            }

            public void Abort(string key, string uploadId)
            {
                var query = new Dictionary<string, string>();
                query["uploadId"] = uploadId ?? "";
                Ensure(_owner.Send("DELETE", key, query, "", null, false));
            }

            private static string Between(string body, string tag)
            {
                var text = body ?? "";
                var open = "<" + tag + ">";
                var start = text.IndexOf(open, StringComparison.Ordinal);
                if (start < 0)
                {
                    throw new InvalidOperationException("腾讯云没有返回上传编号。");
                }

                start += open.Length;
                var end = text.IndexOf("</" + tag + ">", start, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new InvalidOperationException("腾讯云没有返回上传编号。");
                }

                return text.Substring(start, end - start).Trim();
            }
        }

        private sealed class Page
        {
            public bool Truncated;
            public string NextMarker = "";
            public List<Item> Items = new List<Item>();
        }

        private sealed class Item
        {
            public string Key;
            public DateTime Modified;
        }
    }
}
