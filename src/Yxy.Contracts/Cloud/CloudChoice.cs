using System;
using System.Collections.Generic;

namespace Yxy.Contracts.Cloud
{
    public static class CloudChoice
    {
        public static string Resolve(IList<string> ready, bool doCloud, string selected)
        {
            if (!doCloud || ready == null || ready.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(selected))
            {
                for (var i = 0; i < ready.Count; i++)
                {
                    if (string.Equals(ready[i], selected, StringComparison.OrdinalIgnoreCase))
                    {
                        return ready[i];
                    }
                }
            }

            return ready[0];
        }

        public static string Label(string provider)
        {
            if (string.Equals(provider, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase))
            {
                return "百度网盘";
            }

            if (string.Equals(provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase))
            {
                return "七牛云";
            }

            return provider ?? "";
        }
    }
}
