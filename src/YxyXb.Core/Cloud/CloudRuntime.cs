using System;
using YxyXb.Core.Data;

namespace YxyXb.Core.Cloud
{
    public interface ICloudRuntime
    {
        bool IsReady(SqliteDb db, string provider);

        void Upload(SqliteDb db, string provider, string localPath, string remoteKey);

        int DeleteExpired(SqliteDb db, string provider, string prefix, DateTime olderThanUtc, Action<string> onDeleted);
    }

    public static class CloudRuntime
    {
        public static ICloudRuntime Handler { get; set; }

        public static ICloudRuntime Require()
        {
            if (Handler == null)
            {
                throw new InvalidOperationException("云模块未注册。");
            }

            return Handler;
        }
    }
}
