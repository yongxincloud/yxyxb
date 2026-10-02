using System;
using System.Net;
using Yxy.Core.Cloud;

namespace Yxy.Cloud
{
    public static class CloudModule
    {
        public static void Register()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            }
            catch (NotSupportedException)
            {
            }

            CloudRuntime.Handler = new CloudRuntimeImpl();
        }
    }
}
