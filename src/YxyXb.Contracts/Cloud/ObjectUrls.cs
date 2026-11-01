using System;

namespace YxyXb.Contracts.Cloud
{
    public sealed class CloudRegion
    {
        public string Id { get; set; }

        public string Label { get; set; }
    }

    public static class CloudRegions
    {
        public static readonly CloudRegion[] Aliyun = new[]
        {
            Region("oss-cn-hangzhou", "华东1（杭州）"),
            Region("oss-cn-shanghai", "华东2（上海）"),
            Region("oss-cn-nanjing", "华东5（南京）"),
            Region("oss-cn-qingdao", "华北1（青岛）"),
            Region("oss-cn-beijing", "华北2（北京）"),
            Region("oss-cn-zhangjiakou", "华北3（张家口）"),
            Region("oss-cn-huhehaote", "华北5（呼和浩特）"),
            Region("oss-cn-wulanchabu", "华北6（乌兰察布）"),
            Region("oss-cn-shenzhen", "华南1（深圳）"),
            Region("oss-cn-heyuan", "华南2（河源）"),
            Region("oss-cn-guangzhou", "华南3（广州）"),
            Region("oss-cn-chengdu", "西南1（成都）"),
            Region("oss-cn-hongkong", "中国香港"),
            Region("", "自定义 Endpoint")
        };

        public static readonly CloudRegion[] Tencent = new[]
        {
            Region("ap-guangzhou", "广州"),
            Region("ap-beijing", "北京"),
            Region("ap-shanghai", "上海"),
            Region("ap-nanjing", "南京"),
            Region("ap-chengdu", "成都"),
            Region("ap-chongqing", "重庆"),
            Region("ap-hongkong", "中国香港"),
            Region("", "自定义 Endpoint")
        };

        private static CloudRegion Region(string id, string label)
        {
            return new CloudRegion { Id = id, Label = label };
        }
    }

    public static class ObjectUrls
    {
        public static string Aliyun(string bucket, string zone, string key)
        {
            return Virtual("https://", bucket, Endpoint(zone, ".aliyuncs.com"), key);
        }

        public static string Tencent(string bucket, string zone, string key)
        {
            var raw = CloudEndpoints.Host(zone);
            var host = raw.IndexOf('.') >= 0 ? raw : "cos." + raw + ".myqcloud.com";
            return Virtual("https://", bucket, host, key);
        }

        public static string Ctyun(string zone, string bucket, string key)
        {
            return "https://" + CloudEndpoints.Host(zone) + "/" + Escape(bucket) + "/" + EscapeKey(key);
        }

        public static string EscapeKey(string key)
        {
            var text = (key ?? "").Replace('\\', '/').TrimStart('/');
            if (text.Length == 0)
            {
                return "";
            }

            var parts = text.Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = Uri.EscapeDataString(parts[i]);
            }

            return string.Join("/", parts);
        }

        private static string Endpoint(string zone, string suffix)
        {
            var raw = CloudEndpoints.Host(zone);
            if (raw.IndexOf('.') >= 0)
            {
                return raw;
            }

            return raw + suffix;
        }

        private static string Virtual(string scheme, string bucket, string host, string key)
        {
            return scheme + bucket + "." + host + "/" + EscapeKey(key);
        }

        private static string Escape(string text)
        {
            return Uri.EscapeDataString(text ?? "");
        }
    }
}
