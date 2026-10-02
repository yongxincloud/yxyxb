using System;

namespace Yxy.Contracts.Dto
{
    public sealed class MediaRecordDto
    {
        public long Id { get; set; }

        public string SerialNumber { get; set; }

        public string DiskSignature { get; set; }

        public int CapacityGb { get; set; }

        public string VolumeGuid { get; set; }

        public string RegisteredDeviceId { get; set; }

        public DateTime RegisteredUtc { get; set; }

        public DateTime? LastSeenUtc { get; set; }

        public bool TamperPending { get; set; }

        public bool AutoHeld { get; set; }

        public string LetterCmd { get; set; }

        public string LetterResult { get; set; }

        public string FreeSpaceText { get; set; }

        public string LetterText { get; set; }

        public string TamperText
        {
            get { return TamperPending ? "是" : "否"; }
        }

        public string IdentityText
        {
            get
            {
                return (SerialNumber ?? "-") + " / " + (DiskSignature ?? "-") + " / " + CapacityGb + "GB";
            }
        }
    }

    public sealed class MediaTaskDto
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public long MediaId { get; set; }

        public bool Enabled { get; set; }

        public string SourcePath { get; set; }

        public string TargetFolder { get; set; }

        public string WindowStart { get; set; }

        public string WindowEnd { get; set; }

        public int WaitMinutes { get; set; }

        public int RetainVersions { get; set; }

        public int RetainDays { get; set; }

        public string ConflictMode { get; set; }

        public bool IsolateAfter { get; set; }

        public int IsolateLevel { get; set; }

        public bool UseSha256 { get; set; }

        public string CycleKind { get; set; }

        public int CycleInterval { get; set; }

        public DateTime? NextRunUtc { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public DateTime? WaitingSinceUtc { get; set; }

        public int RunCount { get; set; }

        public string EnabledText
        {
            get { return Enabled ? "是" : "否"; }
        }

        public string CycleText
        {
            get { return FileTaskDto.CycleLabel(CycleKind) + " / " + CycleInterval; }
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

        public string LastRunLocal
        {
            get
            {
                return LastRunUtc.HasValue
                    ? LastRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "未执行";
            }
        }

        public string WindowText
        {
            get { return (WindowStart ?? "") + "–" + (WindowEnd ?? ""); }
        }
    }

    public sealed class LiveDiskDto
    {
        public int DiskNumber { get; set; }

        public string SerialNumber { get; set; }

        public string DiskSignature { get; set; }

        public int CapacityGb { get; set; }

        public string BusType { get; set; }

        public string VolumeGuid { get; set; }

        public string DriveLetter { get; set; }

        public bool IsSystem { get; set; }

        public bool Online { get; set; }

        public string PnpDeviceId { get; set; }

        public long FreeBytes { get; set; }

        public string FreeSpaceText { get; set; }

        public string IdentityText
        {
            get
            {
                return (SerialNumber ?? "-") + " / " + (DiskSignature ?? "-") + " / " + CapacityGb + "GB";
            }
        }

        public string StatusText
        {
            get
            {
                if (IsSystem)
                {
                    return "系统盘";
                }

                if (!string.IsNullOrEmpty(DriveLetter))
                {
                    return "已挂载 " + DriveLetter;
                }

                return Online ? "离线" : "已隔离/离线";
            }
        }
    }
}
