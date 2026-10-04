using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;

namespace Yxy.Baidu
{
    public sealed class BaiduStorage : ICloudStorage
    {
        private CloudAccountDto _account;
        private BaiduAppConfig _config;
        private readonly IBaiduTransport _transport;
        private int _maxKbps;
        private bool _did401Retry;

        public Action<CloudAccountDto> PersistAccount { get; set; }

        public BaiduStorage()
            : this(null, null, new BaiduHttp())
        {
        }

        internal BaiduStorage(CloudAccountDto account, BaiduAppConfig config, IBaiduTransport transport)
        {
            _account = account;
            _config = config;
            _transport = transport ?? new BaiduHttp();
        }

        public string ProviderKey
        {
            get { return CloudProviders.Baidu; }
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

        public void TestConnection()
        {
            RequireReady();
            EnsureToken();
            Api("GET", "https://pan.baidu.com/rest/2.0/xpan/nas?method=uinfo", null, null);
        }

        public void Upload(string localPath, string remoteKey)
        {
            RequireReady();
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException("本地包不存在。", localPath);
            }

            if (string.IsNullOrEmpty(remoteKey))
            {
                throw new InvalidOperationException("云对象名为空。");
            }

            EnsureToken();
            var cfg = Config();
            var path = CloudObjectKeys.BaiduRemotePath(cfg.AppFolderName, remoteKey);
            var hashes = BaiduSlices.Md5HexList(localPath);
            var ser = new JavaScriptSerializer();
            var blockList = ser.Serialize(hashes);
            var size = new FileInfo(localPath).Length;
            var pre = Form(
                "path", path,
                "size", size.ToString(CultureInfo.InvariantCulture),
                "isdir", "0",
                "autoinit", "1",
                "rtype", "1",
                "block_list", blockList);
            var preJson = Api("POST", "https://pan.baidu.com/rest/2.0/xpan/file?method=precreate", pre, "application/x-www-form-urlencoded");
            var preObj = ser.Deserialize<BaiduApiJson>(preJson) ?? new BaiduApiJson();
            if (preObj.return_type != 2)
            {
                UploadParts(localPath, path, preObj.uploadid ?? "u1", size);
            }

            var create = Form(
                "path", path,
                "size", size.ToString(CultureInfo.InvariantCulture),
                "isdir", "0",
                "rtype", "1",
                "uploadid", preObj.uploadid ?? "",
                "block_list", blockList);
            Api("POST", "https://pan.baidu.com/rest/2.0/xpan/file?method=create", create, "application/x-www-form-urlencoded");
        }

        public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            RequireReady();
            EnsureToken();
            var cfg = Config();
            var dir = CloudObjectKeys.BaiduRemotePath(cfg.AppFolderName, prefix ?? "");
            if (dir.EndsWith("/", StringComparison.Ordinal))
            {
                dir = dir.TrimEnd('/');
            }

            var ser = new JavaScriptSerializer();
            var deleted = 0;
            var start = 0;
            for (var guard = 0; guard < 200; guard++)
            {
                var url = "https://pan.baidu.com/rest/2.0/xpan/file?method=list"
                    + "&dir=" + Uri.EscapeDataString(dir)
                    + "&start=" + start.ToString(CultureInfo.InvariantCulture)
                    + "&limit=1000";
                var json = Api("GET", url, null, null);
                var page = ser.Deserialize<BaiduApiJson>(json) ?? new BaiduApiJson();
                if (page.list == null || page.list.Count == 0)
                {
                    break;
                }

                for (var i = 0; i < page.list.Count; i++)
                {
                    var item = page.list[i];
                    if (item == null || string.IsNullOrEmpty(item.path))
                    {
                        continue;
                    }

                    var time = FromUnix(item.server_mtime);
                    if (time == DateTime.MinValue)
                    {
                        time = ParseStampFromKey(item.path);
                    }

                    if (time == DateTime.MinValue || time >= olderThanUtc)
                    {
                        continue;
                    }

                    var filelist = ser.Serialize(new[] { new Dictionary<string, string> { { "path", item.path } } });
                    Api(
                        "POST",
                        "https://pan.baidu.com/rest/2.0/xpan/file?method=filemanager&opera=delete",
                        Form("async", "0", "filelist", filelist),
                        "application/x-www-form-urlencoded");
                    if (onDeleted != null)
                    {
                        onDeleted(item.path);
                    }

                    deleted++;
                }

                if (page.list.Count < 1000)
                {
                    break;
                }

                start += page.list.Count;
            }

            return deleted;
        }

        private void UploadParts(string localPath, string path, string uploadId, long fileLen)
        {
            var count = (int)((fileLen + BaiduSlices.BlockSize - 1) / BaiduSlices.BlockSize);
            if (count <= 0)
            {
                count = 1;
            }

            var pacer = new BytePacer(_maxKbps);
            var gate = new object();
            Exception error = null;
            System.Threading.Tasks.Parallel.For(0, count, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 2 }, delegate(int i)
            {
                lock (gate)
                {
                    if (error != null)
                    {
                        return;
                    }
                }

                var offset = (long)i * BaiduSlices.BlockSize;
                var size = (int)Math.Min(BaiduSlices.BlockSize, fileLen - offset);
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
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        pacer.Take(buf.Length);
                        var url = "https://d.pcs.baidu.com/rest/2.0/pcs/superfile2?method=upload&type=tmpfile"
                            + "&path=" + Uri.EscapeDataString(path)
                            + "&uploadid=" + Uri.EscapeDataString(uploadId ?? "")
                            + "&partseq=" + i.ToString(CultureInfo.InvariantCulture);
                        Api("POST", url, buf, "application/octet-stream");
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
                }
            });

            if (error != null)
            {
                throw new InvalidOperationException("百度分片上传失败：" + error.Message);
            }
        }

        private void EnsureToken()
        {
            var acc = RequireReady();
            var soon = DateTime.UtcNow.AddMinutes(5);
            if (acc.TokenExpiryUtc.HasValue && acc.TokenExpiryUtc.Value > soon && !string.IsNullOrEmpty(acc.AccessToken))
            {
                return;
            }

            RefreshToken();
        }

        private void RefreshToken()
        {
            var acc = RequireReady();
            var cfg = Config();
            var url = "https://openapi.baidu.com/oauth/2.0/token?grant_type=refresh_token"
                + "&refresh_token=" + Uri.EscapeDataString(acc.RefreshToken)
                + "&client_id=" + Uri.EscapeDataString(cfg.AppKey ?? "")
                + "&client_secret=" + Uri.EscapeDataString(cfg.AppSecret ?? "");
            string json;
            try
            {
                json = _transport.Call("POST", url, null, Encoding.UTF8.GetBytes(""), "application/x-www-form-urlencoded");
            }
            catch (Exception)
            {
                throw new InvalidOperationException("请重新授权");
            }

            BaiduOAuth.ApplyTokenJson(acc, json);
            if (PersistAccount != null)
            {
                PersistAccount(acc);
            }
        }

        private string Api(string method, string url, byte[] body, string contentType)
        {
            try
            {
                var json = _transport.Call(method, url, _account == null ? null : _account.AccessToken, body, contentType);
                CheckErrno(json);
                _did401Retry = false;
                return json;
            }
            catch (InvalidOperationException ex)
            {
                if (!_did401Retry && ex.Message.IndexOf(" 401", StringComparison.Ordinal) >= 0)
                {
                    _did401Retry = true;
                    RefreshToken();
                    return Api(method, url, body, contentType);
                }

                throw;
            }
        }

        private static void CheckErrno(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException("百度 HTTP errno=empty");
            }

            if (json.IndexOf("\"errno\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            var obj = new JavaScriptSerializer().Deserialize<BaiduApiJson>(json);
            if (obj != null && obj.errno != 0)
            {
                throw new InvalidOperationException("百度 HTTP errno=" + obj.errno);
            }
        }

        private BaiduAppConfig Config()
        {
            if (_config != null)
            {
                return _config;
            }

            _config = BaiduAppConfig.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaiduApp.json"));
            return _config;
        }

        private CloudAccountDto RequireReady()
        {
            if (_account == null || !_account.IsBaiduReady)
            {
                throw new InvalidOperationException("请先在「云备份 → 百度网盘」授权。");
            }

            return _account;
        }

        private static byte[] Form(params string[] parts)
        {
            var sb = new StringBuilder();
            for (var i = 0; i + 1 < parts.Length; i += 2)
            {
                if (sb.Length > 0)
                {
                    sb.Append('&');
                }

                sb.Append(Uri.EscapeDataString(parts[i] ?? "")).Append('=').Append(Uri.EscapeDataString(parts[i + 1] ?? ""));
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static DateTime FromUnix(long seconds)
        {
            if (seconds <= 0)
            {
                return DateTime.MinValue;
            }

            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
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
    }

    internal sealed class BaiduApiJson
    {
        public int errno { get; set; }

        public int return_type { get; set; }

        public string uploadid { get; set; }

        public IList<BaiduListItem> list { get; set; }
    }

    internal sealed class BaiduListItem
    {
        public string path { get; set; }

        public string server_filename { get; set; }

        public long server_mtime { get; set; }
    }
}
