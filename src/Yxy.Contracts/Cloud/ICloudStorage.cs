using System;

namespace Yxy.Contracts.Cloud
{
    public interface ICloudStorage
    {
        string ProviderKey { get; }

        bool IsImplemented { get; }

        void TestConnection();

        void SetSpeed(int minKbps, int maxKbps);

        void Upload(string localPath, string remoteKey);

        int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted);
    }
}
