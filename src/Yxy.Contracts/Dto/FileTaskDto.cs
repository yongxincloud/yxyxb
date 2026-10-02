using System;

namespace Yxy.Contracts.Dto
{
    public sealed class FileTaskDto
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool Enabled { get; set; }

        public string SourceKind { get; set; }

        public string SourcePath { get; set; }

        public int RetryMinutes { get; set; }

        public int SortOrder { get; set; }

        public string CycleKind { get; set; }

        public int CycleInterval { get; set; }

        public DateTime? NextRunUtc { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public int RunCount { get; set; }

        public bool DoNetwork { get; set; }

        public bool DoFtp { get; set; }

        public bool DoCloud { get; set; }

        public string CloudProvider { get; set; }

        public bool DoDeleteNow { get; set; }

        public bool DoEmail { get; set; }

        public string PostProgram { get; set; }

        public string EnabledText
        {
            get { return Enabled ? "是" : "否"; }
        }

        public string SourceKindText
        {
            get
            {
                if (SourceKind == "unc")
                {
                    return "UNC";
                }

                if (SourceKind == "ftp")
                {
                    return "FTP";
                }

                return "本地";
            }
        }

        public string NextRunLocal
        {
            get
            {
                return NextRunUtc.HasValue
                    ? NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "";
            }
        }

        public string CycleText
        {
            get
            {
                return CycleLabel(CycleKind) + " / " + CycleInterval;
            }
        }

        public static string CycleLabel(string kind)
        {
            if (kind == "minute")
            {
                return "分钟";
            }

            if (kind == "hour")
            {
                return "小时";
            }

            if (kind == "day")
            {
                return "天";
            }

            if (kind == "week")
            {
                return "每星期";
            }

            if (kind == "month")
            {
                return "每月";
            }

            if (kind == "once")
            {
                return "仅一次";
            }

            return kind ?? "";
        }
    }
}
