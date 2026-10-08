using System;
using System.IO;
using System.Threading;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;
using Yxy.Core.Store;
using Yxy.Media.Disks;

namespace Yxy.Media
{
    public static class MediaService
    {
        public const string ForeignPrefix = "FOREIGN:";

        public static string Register(SqliteDb db, LiveDiskDto disk, string deviceId, string softwareId, string bakPath, bool takeOver)
        {
            try
            {
                var refuse = MediaSafety.RefuseWrite(disk, bakPath);
                if (refuse != null)
                {
                    return refuse;
                }

                if (string.IsNullOrEmpty(disk.DriveLetter))
                {
                    return "请先让系统为该盘分配盘符后再登记。";
                }

                var root = disk.DriveLetter.TrimEnd('\\') + "\\";
                var existing = VolumeMarkerStore.Read(root);
                var foreign = VolumeMarkerStore.CheckForeign(existing, deviceId);
                if (foreign != null && !takeOver)
                {
                    return ForeignPrefix + foreign;
                }

                var found = MediaStore.Find(db, disk.SerialNumber, disk.DiskSignature, disk.CapacityGb);
                if (found == null)
                {
                    MediaStore.Insert(db, new MediaRecordDto
                    {
                        SerialNumber = disk.SerialNumber,
                        DiskSignature = disk.DiskSignature,
                        CapacityGb = disk.CapacityGb,
                        VolumeGuid = disk.VolumeGuid,
                        RegisteredDeviceId = deviceId
                    });
                }

                var writeError = VolumeMarkerStore.Write(root, new VolumeMarker
                {
                    Serial = disk.SerialNumber,
                    Signature = disk.DiskSignature,
                    CapacityGb = disk.CapacityGb,
                    DeviceId = deviceId,
                    RegisteredUtc = DateTime.UtcNow.ToString("o"),
                    SoftwareId = softwareId
                });
                if (writeError != null)
                {
                    return "已写入本机列表，但盘上标记失败：" + writeError;
                }

                return found != null ? "该介质已在列表中，已刷新盘上标记。" : null;
            }
            catch (Exception ex)
            {
                return "登记失败：" + ex.Message;
            }
        }

        public static int RecoverAttached(SqliteDb db, string deviceId)
        {
            var n = 0;
            var live = DiskEnumerator.ListRemovable();
            for (var i = 0; i < live.Count; i++)
            {
                var disk = live[i];
                if (string.IsNullOrEmpty(disk.DriveLetter))
                {
                    continue;
                }

                if (MediaStore.Find(db, disk.SerialNumber, disk.DiskSignature, disk.CapacityGb) != null)
                {
                    continue;
                }

                var marker = VolumeMarkerStore.Read(disk.DriveLetter.TrimEnd('\\') + "\\");
                if (marker == null || string.IsNullOrEmpty(marker.Serial))
                {
                    continue;
                }

                if (!string.Equals(marker.Serial, disk.SerialNumber, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MediaStore.Insert(db, new MediaRecordDto
                {
                    SerialNumber = disk.SerialNumber,
                    DiskSignature = disk.DiskSignature,
                    CapacityGb = disk.CapacityGb,
                    VolumeGuid = disk.VolumeGuid,
                    RegisteredDeviceId = string.IsNullOrEmpty(marker.DeviceId) ? deviceId : marker.DeviceId
                });
                n++;
            }

            return n;
        }

        public static string UnbindHint(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return "解绑后本机不再管理该盘。确定解绑？";
            }

            var data = Path.Combine(letter.TrimEnd('\\') + "\\", "用心云循备");
            if (Directory.Exists(data))
            {
                return "盘上已有本产品数据。解绑后不再受管，盘内文件不会删除。确定解绑？";
            }

            return "确定解绑该介质？";
        }

        public static string ToggleLetter(SqliteDb db, MediaRecordDto record)
        {
            if (record == null)
            {
                return "请先选择已登记介质。";
            }

            var live = FindLive(record);
            if (live == null)
            {
                return "盘未接入，无法切换。请先插上这块盘。";
            }

            var cmd = HasLetter(live) ? "hide" : "show";
            MediaStore.RequestLetter(db, record.Id, cmd);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(400);
                var current = MediaStore.GetById(db, record.Id);
                if (current == null)
                {
                    return "登记已不存在。";
                }

                if (!string.IsNullOrEmpty(current.LetterCmd))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(current.LetterResult))
                {
                    return current.LetterResult;
                }
            }

            return "引擎没有在 20 秒内完成。请到首页确认服务正在运行。";
        }

        public static string ShowLetter(MediaRecordDto record, out string letter)
        {
            letter = null;
            var live = FindLive(record);
            if (live == null)
            {
                return "盘未接入。";
            }

            if (!HasLetter(live))
            {
                try
                {
                    DiskIsolate.BringOnline(live, string.IsNullOrEmpty(live.VolumeGuid) ? record.VolumeGuid : live.VolumeGuid);
                }
                catch (InvalidOperationException)
                {
                }

                var keptVolume = live.VolumeGuid;
                Thread.Sleep(800);
                var again = FindLive(record);
                if (again != null)
                {
                    if (string.IsNullOrEmpty(again.VolumeGuid))
                    {
                        again.VolumeGuid = keptVolume;
                    }

                    live = again;
                }

                if (string.IsNullOrEmpty(live.VolumeGuid))
                {
                    live.VolumeGuid = string.IsNullOrEmpty(keptVolume) ? record.VolumeGuid : keptVolume;
                }

                try
                {
                    letter = DiskMount.EnsureLetter(live);
                }
                catch (InvalidOperationException ex)
                {
                    letter = null;
                    return ex.Message;
                }
            }
            else
            {
                letter = live.DriveLetter;
            }

            if (!string.IsNullOrEmpty(letter))
            {
                var root = letter.TrimEnd('\\') + "\\";
                for (var i = 0; i < 20 && !Directory.Exists(root); i++)
                {
                    Thread.Sleep(500);
                }
            }

            if (string.IsNullOrEmpty(letter))
            {
                return "未能分配盘符。";
            }

            if (!Directory.Exists(letter.TrimEnd('\\') + "\\"))
            {
                letter = null;
                return "未能分配盘符。";
            }

            return null;
        }

        public static string HideLetter(MediaRecordDto record)
        {
            var live = FindLive(record);
            if (live == null || !HasLetter(live))
            {
                return null;
            }

            var letter = live.DriveLetter;
            DiskIsolate.HideFromExplorer(live);
            return letter;
        }

        private static LiveDiskDto FindLive(MediaRecordDto record)
        {
            if (record == null)
            {
                return null;
            }

            return DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
        }

        private static bool HasLetter(LiveDiskDto live)
        {
            return live != null
                && !string.IsNullOrEmpty(live.DriveLetter)
                && Directory.Exists(live.DriveLetter.TrimEnd('\\') + "\\");
        }

        public static string Unbind(SqliteDb db, long mediaId, string letter)
        {
            MediaStore.Delete(db, mediaId);
            VolumeMarkerStore.Remove(letter);
            return "已解绑。";
        }
    }
}
