using System;
using Yxy.Contracts.Cloud;

namespace Yxy.Ctyun
{
    public sealed class CtyunStorage : ICloudStorage
    {
        public string ProviderKey
        {
            get { return CloudProviders.Ctyun; }
        }

        public bool IsImplemented
        {
            get { return false; }
        }

        public void TestConnection()
        {
            throw new NotSupportedException("天翼云后续开通。");
        }

        public void SetSpeed(int minKbps, int maxKbps)
        {
        }

        public void Upload(string localPath, string remoteKey)
        {
            throw new NotSupportedException("天翼云后续开通。");
        }

        public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
        {
            throw new NotSupportedException("天翼云后续开通。");
        }
    }
}
