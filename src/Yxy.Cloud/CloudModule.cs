using Yxy.Core;
using Yxy.Core.Cloud;

namespace Yxy.Cloud
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
