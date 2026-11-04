using System;
using System.IO;
using YxyXb.Aliyun;
using YxyXb.Baidu;
using YxyXb.Qiniu;
using YxyXb.TencentCloud;
using YxyXb.Contracts.Cloud;
using YxyXb.Ctyun;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Cloud;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Cloud
{
    internal sealed class CloudRuntimeImpl : ICloudRuntime
    {
        public bool IsReady(SqliteDb db, string provider)
        {
            if (string.IsNullOrEmpty(provider))
            {
                return false;
            }

            var storage = CloudStorageFactory.Create(provider, CloudAccountStore.Get(db, provider));
            return storage.IsImplemented && AccountReady(provider, CloudAccountStore.Get(db, provider));
        }

        public void Upload(SqliteDb db, string provider, string localPath, string remoteKey)
        {
            var settings = SettingsStore.Get(db);
            var storage = CreateReady(db, provider);
            var uploadKbps = CloudSpeed.UploadKbps(settings.UploadMinKbps, settings.UploadMaxKbps);
            storage.SetSpeed(uploadKbps, uploadKbps);
            storage.SetParallel(settings.UploadParallel);
            var baidu = storage as BaiduStorage;
            var qiniu = storage as QiniuStorage;
            if (baidu != null)
            {
                baidu.OnLog = delegate(string line)
                {
                    LogStore.Add(db, "baidu", "auto", SliceResult(line), Path.GetFileName(localPath), line);
                };
            }

            if (qiniu != null)
            {
                qiniu.OnLog = delegate(string line)
                {
                    LogStore.Add(db, "qiniu", "auto", SliceResult(line), Path.GetFileName(localPath), line);
                };
            }

            var ctyun = storage as CtyunStorage;
            if (ctyun != null)
            {
                ctyun.OnLog = delegate(string line)
                {
                    LogStore.Add(db, "ctyun", "auto", SliceResult(line), Path.GetFileName(localPath), line);
                };
            }

            var tencent = storage as TencentCloudStorage;
            if (tencent != null)
            {
                tencent.OnLog = delegate(string line)
                {
                    LogStore.Add(db, "tencent", "auto", SliceResult(line), Path.GetFileName(localPath), line);
                };
            }

            var aliyun = storage as AliyunStorage;
            if (aliyun != null)
            {
                aliyun.OnLog = delegate(string line)
                {
                    LogStore.Add(db, "aliyun", "auto", SliceResult(line), Path.GetFileName(localPath), line);
                };
            }

            try
            {
                storage.Upload(localPath, remoteKey);
            }
            finally
            {
                if (baidu != null)
                {
                    baidu.OnLog = null;
                }

                if (qiniu != null)
                {
                    qiniu.OnLog = null;
                }

                if (ctyun != null)
                {
                    ctyun.OnLog = null;
                }

                if (tencent != null)
                {
                    tencent.OnLog = null;
                }

                if (aliyun != null)
                {
                    aliyun.OnLog = null;
                }
            }
        }

        public void Verify(SqliteDb db, string provider, string localPath, string remoteKey)
        {
            var info = CreateReady(db, provider).Stat(remoteKey);
            RemoteObjectVerifier.EnsureSameSize(localPath, info);
        }

        public int DeleteExpired(SqliteDb db, string provider, string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            return CreateReady(db, provider).DeleteExpired(prefix, olderThanUtc, onDeleted);
        }

        public void DiscardUpload(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return;
            }

            BaiduUploadCheckpoint.Delete(AppPaths.DataDirectory, localPath);
        }

        private static string SliceResult(string line)
        {
            var text = line ?? "";
            if (text.IndexOf("失败", StringComparison.Ordinal) >= 0 || text.IndexOf("未齐", StringComparison.Ordinal) >= 0)
            {
                return "error";
            }

            return "ok";
        }

        private static ICloudStorage CreateReady(SqliteDb db, string provider)
        {
            var account = CloudAccountStore.Get(db, provider);
            var storage = CloudStorageFactory.Create(provider, account);
            if (!storage.IsImplemented)
            {
                throw new NotSupportedException("该云服务商后续开通。");
            }

            if (!AccountReady(provider, account))
            {
                throw new InvalidOperationException("云账号未配置完成。");
            }

            var baidu = storage as BaiduStorage;
            if (baidu != null)
            {
                baidu.PersistAccount = delegate(CloudAccountDto updated)
                {
                    CloudAccountStore.Save(db, updated);
                };
            }

            return storage;
        }

        private static bool AccountReady(string provider, CloudAccountDto account)
        {
            if (account == null)
            {
                return false;
            }

            if (string.Equals(provider, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase))
            {
                return account.IsBaiduReady;
            }

            if (string.Equals(provider, CloudProviders.Aliyun, StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, CloudProviders.Tencent, StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, CloudProviders.Ctyun, StringComparison.OrdinalIgnoreCase))
            {
                return account.IsObjectReady;
            }

            return account.IsReady;
        }
    }
}
