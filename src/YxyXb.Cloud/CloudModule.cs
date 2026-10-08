using YxyXb.Core;
using YxyXb.Core.Cloud;

namespace YxyXb.Cloud
{
    public static class CloudModule
    {
        public static void Register()
        {
            TlsPolicy.Apply();
            CloudRuntime.Handler = new CloudRuntimeImpl();
        }
    }
}
