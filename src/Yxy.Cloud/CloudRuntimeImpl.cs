using System;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
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
            return storage.IsImplemented && CloudAccountStore.Get(db, provider).IsReady;
        }

        public void Upload(SqliteDb db, string provider, string localPath, string remoteKey)
        {
            var settings = SettingsStore.Get(db);
            var storage = CreateReady(db, provider);
            storage.SetSpeed(settings.UploadMinKbps, settings.UploadMaxKbps);
            storage.Upload(localPath, remoteKey);
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

            if (!account.IsReady)
            {
                throw new InvalidOperationException("云账号未配置完成。");
            }

            return storage;
        }
    }
}
