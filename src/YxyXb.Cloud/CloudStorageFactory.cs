using System;
using YxyXb.Aliyun;
using YxyXb.Baidu;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Ctyun;
using YxyXb.Qiniu;
using YxyXb.TencentCloud;

namespace YxyXb.Cloud
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

            if (string.Equals(providerKey, CloudProviders.Aliyun, StringComparison.OrdinalIgnoreCase))
            {
                var aliyun = new AliyunStorage();
                if (account != null)
                {
                    aliyun.Configure(account);
                }

                return aliyun;
            }

            if (string.Equals(providerKey, CloudProviders.Tencent, StringComparison.OrdinalIgnoreCase))
            {
                var tencent = new TencentCloudStorage();
                if (account != null)
                {
                    tencent.Configure(account);
                }

                return tencent;
            }

            if (string.Equals(providerKey, CloudProviders.Ctyun, StringComparison.OrdinalIgnoreCase))
            {
                var ctyun = new CtyunStorage();
                if (account != null)
                {
                    ctyun.Configure(account);
                }

                return ctyun;
            }

            throw new NotSupportedException("未知云服务商：" + providerKey);
        }
    }
}
