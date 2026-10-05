using System;
using System.IO;
using Yxy.Baidu;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core;
using Yxy.Core.Cloud;
using Yxy.Core.Data;
using Yxy.Core.Store;

namespace Yxy.Cloud
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
            if (baidu != null)
            {
                baidu.OnLog = delegate(string line)
                {
                    LogStore.Add(db, "baidu", "auto",
                        (line ?? "").IndexOf("失败", StringComparison.Ordinal) >= 0 ? "error" : "ok",
                        Path.GetFileName(localPath), line);
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
            }
        }

        public int DeleteExpired(SqliteDb db, string provider, string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            return CreateReady(db, provider).DeleteExpired(prefix, olderThanUtc, onDeleted);
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

            return account.IsReady;
        }
    }
}
