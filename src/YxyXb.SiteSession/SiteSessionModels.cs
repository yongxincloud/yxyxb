using System;
using System.Collections.Generic;

namespace YxyXb.SiteSession
{
    public sealed class SiteDeviceContext
    {
        public string DeviceId { get; set; }
        public string DisplayName { get; set; }
        public string Note { get; set; }
        public string TaskStatus { get; set; }
    }

    public sealed class SiteSessionState
    {
        public bool LoggedIn { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string CompanyName { get; set; }
        public bool Valid { get; set; }
        public DateTime? AuthEndUtc { get; set; }
        public string AuthStatus { get; set; }
        public int NextCheckDays { get; set; }
        public string ProductNo { get; set; }
        public string ProductName { get; set; }
        public int? MaxDevices { get; set; }
    }

    public sealed class SiteLoginOutcome
    {
        public bool Ok { get; set; }
        public bool DeviceLimited { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }
        public SiteSessionState Session { get; set; }
    }

    public sealed class SiteCodeOutcome
    {
        public bool Ok { get; set; }
        public string DevCode { get; set; }
        public string Error { get; set; }
    }

    public sealed class SiteDeviceInfo
    {
        public string DeviceId { get; set; }
        public string DisplayName { get; set; }
        public string Note { get; set; }
        public DateTime? FirstSeenUtc { get; set; }
        public DateTime? LastSeenUtc { get; set; }
        public string WanIp { get; set; }
        public string TaskStatus { get; set; }
    }

    public sealed class SiteDeviceListState
    {
        public int? MaxDevices { get; set; }
        public string ProductNo { get; set; }
        public string ProductName { get; set; }
        public List<SiteDeviceInfo> Devices { get; set; }
    }

    public sealed class SiteHttpResponse
    {
        public int StatusCode { get; set; }
        public string Body { get; set; }
    }

    public interface ISiteHttpTransport
    {
        SiteHttpResponse Post(string path, IDictionary<string, object> payload);
    }

    public interface ISiteSessionStore
    {
        SiteSessionState Load();
        string LoadToken();
        void Save(SiteSessionState session);
        void Clear();
    }
}
