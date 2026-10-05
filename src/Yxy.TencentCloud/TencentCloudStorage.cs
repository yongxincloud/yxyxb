using System;
using Yxy.Contracts.Cloud;

namespace Yxy.TencentCloud
{
    public sealed class TencentCloudStorage : ICloudStorage
    {
        public string ProviderKey
        {
            get { return CloudProviders.Tencent; }
        }

        public bool IsImplemented
        {
            get { return false; }
        }

        public void TestConnection()
        {
            throw new NotSupportedException("腾讯云后续开通。");
        }

        public void SetSpeed(int minKbps, int maxKbps)
        {
        }

        public void SetParallel(int parallel)
        {
        }

        public void Upload(string localPath, string remoteKey)
        {
            throw new NotSupportedException("腾讯云后续开通。");
        }

        public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            throw new NotSupportedException("腾讯云后续开通。");
        }
    }
}
