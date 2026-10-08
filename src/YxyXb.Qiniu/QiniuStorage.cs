using System;
using System.Globalization;
using System.IO;
using System.Text;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;

namespace YxyXb.Qiniu
{
    public sealed class QiniuStorage : ICloudStorage
    {
        private CloudAccountDto _account;
        private int _maxKbps;

        public string ProviderKey
        {
            get { return CloudProviders.Qiniu; }
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
            _maxKbps = maxKbps < 0 ? 0 : maxKbps;
        }

        public void SetParallel(int parallel)
        {
        }

        public void TestConnection()
        {
            var acc = Require();
            QiniuHttp.EnsureTls12();
            var path = "/buckets";
            var auth = QiniuHttp.ManageToken(acc.AccessKey, acc.SecretKey, path, null);
            var json = QiniuHttp.RequestJson("GET", "https://rs.qiniu.com" + path, auth, null, null);
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException("七牛测试无返回。");
            }

            if (!string.IsNullOrEmpty(acc.Bucket) && json.IndexOf(acc.Bucket, StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("密钥可用，但空间 " + acc.Bucket + " 不在账户列表中。");
            }
        }

        public void Upload(string localPath, string remoteKey)
        {
            var acc = Require();
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException("本地包不存在。", localPath);
            }

            if (string.IsNullOrEmpty(remoteKey))
            {
                throw new InvalidOperationException("云对象名为空。");
            }

            var token = QiniuHttp.UploadToken(acc.AccessKey, acc.SecretKey, acc.Bucket);
            QiniuHttp.UploadFile(QiniuHttp.UploadHost(acc.Zone), token, remoteKey, localPath, _maxKbps);
        }

        public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            var acc = Require();
            var marker = "";
            var guard = 0;
            var deleted = 0;
            while (guard < 200)
            {
                guard++;
                var query = "/list?bucket=" + Uri.EscapeDataString(acc.Bucket)
                    + "&prefix=" + Uri.EscapeDataString(prefix ?? "")
                    + "&limit=1000";
                if (!string.IsNullOrEmpty(marker))
                {
                    query += "&marker=" + Uri.EscapeDataString(marker);
                }

                var auth = QiniuHttp.ManageToken(acc.AccessKey, acc.SecretKey, query, null);
                var json = QiniuHttp.RequestJson("GET", "https://rsf.qiniu.com" + query, auth, null, null);
                var page = QiniuHttp.FromJson<QiniuListResult>(json);
                if (page == null || page.items == null || page.items.Count == 0)
                {
                    break;
                }

                for (var i = 0; i < page.items.Count; i++)
                {
                    var item = page.items[i];
                    if (item == null || string.IsNullOrEmpty(item.key))
                    {
                        continue;
                    }

                    // 只删过期 zip；不以「目录前缀」为删除对象
                    if (!item.key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var time = QiniuHttp.FromPutTime(item.putTime);
                    if (time == DateTime.MinValue)
                    {
                        time = ParseStampFromKey(item.key);
                    }

                    if (time == DateTime.MinValue || time >= olderThanUtc)
                    {
                        continue;
                    }

                    DeleteOne(acc, item.key, onDeleted);
                    deleted++;
                }

                if (string.IsNullOrEmpty(page.marker))
                {
                    break;
                }

                marker = page.marker;
            }

            return deleted;
        }

        private static void DeleteOne(CloudAccountDto acc, string key, Action<string> onDeleted)
        {
            var encoded = QiniuHttp.UrlSafeBase64(acc.Bucket + ":" + key);
            var path = "/delete/" + encoded;
            var auth = QiniuHttp.ManageToken(acc.AccessKey, acc.SecretKey, path, null);
            QiniuHttp.RequestJson("POST", "https://rs.qiniu.com" + path, auth, Encoding.UTF8.GetBytes(""), "application/x-www-form-urlencoded");
            if (onDeleted != null)
            {
                onDeleted(key);
            }
        }

        private static DateTime ParseStampFromKey(string key)
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

            var stamp = parts[parts.Length - 1];
            DateTime local;
            if (DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        private CloudAccountDto Require()
        {
            if (_account == null || !_account.IsReady)
            {
                throw new InvalidOperationException("请先在「云备份 → 七牛云」保存空间和密钥。");
            }

            return _account;
        }
    }
}
