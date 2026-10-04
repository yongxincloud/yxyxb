using System;
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

        public Action<CloudAccountDto> PersistAccount { get; set; }

        public BaiduStorage()
        {
        }

        internal BaiduStorage(CloudAccountDto account, BaiduAppConfig config, IBaiduTransport transport)
        {
            _account = account;
            _config = config;
            _transport = transport;
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
        }

        public void Upload(string localPath, string remoteKey)
        {
            RequireReady();
        }

        public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            RequireReady();
            return 0;
        }

        private CloudAccountDto RequireReady()
        {
            if (_account == null || !_account.IsBaiduReady)
            {
                throw new InvalidOperationException("请先在「云备份 → 百度网盘」授权。");
            }

            return _account;
        }
    }
}
