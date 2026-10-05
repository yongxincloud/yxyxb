using System;

namespace Yxy.Contracts.Dto
{
    public sealed class AppSettingsDto
    {
        public string BakPath { get; set; }

        public string SoftwareId { get; set; }

        public int NameFlags { get; set; }

        public int ZipLevel { get; set; }

        public int UploadMinKbps { get; set; }

        public int UploadMaxKbps { get; set; }

        public int UploadParallel { get; set; }

        public string ZipPassword { get; set; }

        public bool StartWithWindows { get; set; }

        public bool CloseToTray { get; set; }

        public string Theme { get; set; }

        public string LockPassword { get; set; }

        public bool EngineAdvanced { get; set; }

        public int LocalRetainDays { get; set; }

        public int NetworkRetainDays { get; set; }

        public int FtpRetainDays { get; set; }

        public int QiniuRetainDays { get; set; }

        public int BaiduRetainDays { get; set; }

        public bool PurgeLocal { get; set; }

        public bool PurgeNetwork { get; set; }

        public bool PurgeFtp { get; set; }

        public bool PurgeQiniu { get; set; }

        public bool PurgeBaidu { get; set; }

        public string PurgeTime { get; set; }

        public DateTime? LastPurgeUtc { get; set; }

        public int LogRetainDays { get; set; }
    }

    public sealed class UncSettingsDto
    {
        public string Path { get; set; }

        public string Domain { get; set; }

        public string User { get; set; }

        public string Password { get; set; }
    }

    public sealed class FtpSettingsDto
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        public bool Passive { get; set; } = true;

        public string RemoteDir { get; set; }
    }

    public sealed class SmtpSettingsDto
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public bool Ssl { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public string Footer { get; set; }

        public bool SendAfterTask { get; set; }

        public string SendWhen { get; set; }
    }
}
