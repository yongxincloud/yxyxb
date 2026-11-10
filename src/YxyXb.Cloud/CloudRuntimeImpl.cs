using System;
using System.IO;
using YxyXb.Baidu;
using YxyXb.Contracts.Cloud;
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

            var account = CloudAccountStore.Get(db, provider);
            var storage = CloudStorageFactory.Create(provider, account);
            return storage.IsImplemented && AccountReady(provider, account);
        }

        public void Upload(SqliteDb db, string provider, string localPath, string remoteKey)
        {
            var settings = SettingsStore.Get(db);
            var storage = CreateReady(db, provider);
            var uploadKbps = CloudSpeed.UploadKbps(settings.UploadMinKbps, settings.UploadMaxKbps);
            storage.SetSpeed(uploadKbps, uploadKbps);
            storage.SetParallel(settings.UploadParallel);
            storage.OnLog = delegate(string line)
            {
                LogStore.Add(db, provider, "auto", SliceResult(line), Path.GetFileName(localPath), line);
            };

            try
            {
                CloudUploadRecovery.Upload(storage, localPath, remoteKey);
            }
            finally
            {
                storage.OnLog = null;
            }
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
