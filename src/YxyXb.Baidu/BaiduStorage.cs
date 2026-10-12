using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core;

namespace YxyXb.Baidu
{
    public sealed class BaiduStorage : ICloudStorage
    {
        private CloudAccountDto _account;
        private BaiduAppConfig _config;
        private readonly IBaiduTransport _transport;
        private int _maxKbps;
        private int _parallel = 1;
        private bool _did401Retry;

        public Action<CloudAccountDto> PersistAccount { get; set; }

        public Action<string> OnLog { get; set; }

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

        public void SetParallel(int parallel)
        {
            if (parallel < 1)
            {
                _parallel = 1;
            }
            else if (parallel > 2)
            {
                _parallel = 2;
            }
            else
            {
                _parallel = parallel;
            }
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
            var full = Path.GetFullPath(localPath);
            var path = CloudObjectKeys.BaiduRemotePath(cfg.AppFolderName, remoteKey);
            var hashes = BaiduSlices.Md5HexList(full);
            var md5Arr = new string[hashes.Count];
            for (var i = 0; i < hashes.Count; i++)
            {
                md5Arr[i] = hashes[i];
            }

            var ser = new JavaScriptSerializer();
            var blockList = ser.Serialize(hashes);
            var size = new FileInfo(full).Length;
            var dataDir = AppPaths.DataDirectory;
            Directory.CreateDirectory(AppPaths.BaiduUploadDirectory);
            Note(dataDir, full, "开始上传 " + Path.GetFileName(full) + "，大小 " + size + " 字节，分片 " + hashes.Count + "，远程 " + path);
            var cp = BaiduUploadCheckpoint.TryLoad(dataDir, full, size, hashes);
            string uploadId;
            if (cp == null)
            {
                var pre = Form(
                    "path", path,
                    "size", size.ToString(CultureInfo.InvariantCulture),
                    "isdir", "0",
                    "autoinit", "1",
                    "rtype", "1",
                    "block_list", blockList);
                var preJson = Api("POST", "https://pan.baidu.com/rest/2.0/xpan/file?method=precreate", pre, "application/x-www-form-urlencoded");
                var preObj = ser.Deserialize<BaiduApiJson>(preJson) ?? new BaiduApiJson();
                if (preObj.return_type == 2)
                {
                    BaiduUploadCheckpoint.Delete(dataDir, full);
                    Note(dataDir, full, "秒传，跳过分片。");
                    CreateRemote(path, size, preObj.uploadid ?? "", blockList);
                    Note(dataDir, full, "合并完成。");
                    return;
                }

                uploadId = string.IsNullOrEmpty(preObj.uploadid) ? "u1" : preObj.uploadid;
                cp = new BaiduUploadCheckpoint
                {
                    LocalPath = full,
                    Size = size,
                    BlockSize = BaiduSlices.BlockSize,
                    BlockMd5 = md5Arr,
                    RemotePath = path,
                    UploadId = uploadId,
                    DonePartSeq = new int[0]
                };
                BaiduUploadCheckpoint.Save(dataDir, cp);
                Note(dataDir, full, "precreate 成功，uploadid=" + uploadId);
            }
            else
            {
                uploadId = cp.UploadId;
                if (!string.IsNullOrEmpty(cp.RemotePath))
                {
                    path = cp.RemotePath;
                }

                Note(dataDir, full, "续传 uploadid=" + uploadId + "，已完成 " + (cp.DonePartSeq == null ? 0 : cp.DonePartSeq.Length) + "/" + hashes.Count);
            }

            UploadParts(full, path, uploadId, size, cp, dataDir);
            if (!cp.IsComplete(hashes.Count))
            {
                throw new InvalidOperationException("百度分片未齐，禁止合并。");
            }

            CreateRemote(path, size, uploadId, blockList);
            Note(dataDir, full, "分片已齐，合并完成。");
            BaiduUploadCheckpoint.Delete(dataDir, full);
        }

        private void CreateRemote(string path, long size, string uploadId, string blockList)
        {
            var create = Form(
                "path", path,
                "size", size.ToString(CultureInfo.InvariantCulture),
                "isdir", "0",
                "rtype", "1",
                "uploadid", uploadId ?? "",
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

            return DeleteExpiredUnder(dir, olderThanUtc, onDeleted, 0);
        }

        /// <summary>
        /// 递归列出子目录，只按文件时间戳删除过期 zip；不按文件夹 mtime 整夹删除。
        /// </summary>
        private int DeleteExpiredUnder(string dir, DateTime olderThanUtc, Action<string> onDeleted, int depth)
        {
            if (depth > 12 || string.IsNullOrEmpty(dir))
            {
                return 0;
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

                    // 目录：只递归进入，永远不调用 delete（禁止整夹删除）
                    if (item.isdir != 0)
                    {
                        deleted += DeleteExpiredUnder(item.path, olderThanUtc, onDeleted, depth + 1);
                        continue;
                    }

                    // 只删过期 zip 文件，其它文件类型一律保留
                    if (!item.path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var time = ParseStampFromKey(item.path);
                    if (time == DateTime.MinValue)
                    {
                        time = FromUnix(item.server_mtime);
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

        private void UploadParts(string localPath, string path, string uploadId, long fileLen, BaiduUploadCheckpoint cp, string dataDir)
        {
            var count = (int)((fileLen + BaiduSlices.BlockSize - 1) / BaiduSlices.BlockSize);
            if (count <= 0)
            {
                count = 1;
            }

            var done = new HashSet<int>();
            if (cp != null && cp.DonePartSeq != null)
            {
                for (var d = 0; d < cp.DonePartSeq.Length; d++)
                {
                    done.Add(cp.DonePartSeq[d]);
                }
            }

            var gate = new object();
            Exception error = null;
            System.Threading.Tasks.Parallel.For(0, count, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = _parallel }, delegate(int i)
            {
                if (done.Contains(i))
                {
                    Note(dataDir, localPath, "跳过已完成分片 " + (i + 1) + "/" + count);
                    return;
                }

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
                for (var attempt = 0; attempt < BaiduUploadRetry.Attempts; attempt++)
                {
                    var started = Environment.TickCount;
                    try
                    {
                        var url = "https://d.pcs.baidu.com/rest/2.0/pcs/superfile2?method=upload&type=tmpfile"
                            + "&path=" + Uri.EscapeDataString(path)
                            + "&uploadid=" + Uri.EscapeDataString(uploadId ?? "")
                            + "&partseq=" + i.ToString(CultureInfo.InvariantCulture);
                        Api("POST", url, buf, "application/octet-stream");
                        lock (gate)
                        {
                            cp.MarkDone(i);
                            BaiduUploadCheckpoint.Save(dataDir, cp);
                        }

                        Note(dataDir, localPath, "分片 " + (i + 1) + "/" + count + " 成功，" + size + " 字节" + (attempt > 0 ? "（重试 " + attempt + "）" : ""));

                        var gap = BaiduUploadRetry.GapMs(_maxKbps, size, unchecked(Environment.TickCount - started));
                        if (gap > 0)
                        {
                            System.Threading.Thread.Sleep(gap);
                        }

                        last = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Note(dataDir, localPath, "分片 " + (i + 1) + "/" + count + " 失败第 " + (attempt + 1) + " 次：" + ex.Message);
                        System.Threading.Thread.Sleep(BaiduUploadRetry.SleepMs(attempt));
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
                Note(dataDir, localPath, "分片未齐，停止合并。");
                throw new InvalidOperationException("百度分片上传失败：" + error.Message);
            }
        }

        private void Note(string dataDir, string localPath, string message)
        {
            try
            {
                BaiduUploadLog.Write(dataDir, localPath, message);
            }
            catch (IOException)
            {
            }

            var sink = OnLog;
            if (sink != null)
            {
                try
                {
                    sink(message);
                }
                catch
                {
                }
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
            var http = _transport as BaiduHttp;
            if (http != null)
            {
                var blockBytes = body == null ? BaiduSlices.BlockSize : body.Length;
                http.TimeoutMs = BaiduUploadTimeout.Seconds(_maxKbps, blockBytes) * 1000;
            }

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

        /// <summary>百度 list：0=文件，1=目录。</summary>
        public int isdir { get; set; }
    }
}
