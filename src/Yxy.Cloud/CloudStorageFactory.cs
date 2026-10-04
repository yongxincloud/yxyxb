using System;
using Yxy.Baidu;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Ctyun;
using Yxy.Qiniu;
using Yxy.TencentCloud;

namespace Yxy.Cloud
{
    public static class CloudStorageFactory
    {
        public static ICloudStorage Create(string providerKey)
        {
            return Create(providerKey, null);
        }

        public static ICloudStorage Create(string providerKey, CloudAccountDto account)
        {
            if (string.Equals(providerKey, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase))
            {
                var qiniu = new QiniuStorage();
                if (account != null)
                {
                    qiniu.Configure(account);
                }

                return qiniu;
            }

            if (string.Equals(providerKey, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase))
            {
                var baidu = new BaiduStorage();
                if (account != null)
                {
                    baidu.Configure(account);
                }

                return baidu;
            }

            if (string.Equals(providerKey, CloudProviders.Tencent, StringComparison.OrdinalIgnoreCase))
            {
                return new TencentCloudStorage();
            }

            if (string.Equals(providerKey, CloudProviders.Ctyun, StringComparison.OrdinalIgnoreCase))
            {
                return new CtyunStorage();
            }

            throw new NotSupportedException("未知云服务商：" + providerKey);
        }
    }
}
