using System;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Media.Disks;

namespace YxyXb.Media
{
    internal enum MediaOfflineStatus
    {
        AlreadyOffline,
        FullyOffline,
        FallbackHidden,
        Refused,
        Failed
    }

    internal sealed class MediaOfflineResult
    {
        public MediaOfflineStatus Status { get; set; }
        public int Level { get; set; }
        public string Message { get; set; }
        public string Warning { get; set; }
        public bool Succeeded
        {
            get { return Status != MediaOfflineStatus.Failed; }
        }
    }

    internal interface IMediaDiskRuntime
    {
        LiveDiskDto Find(MediaRecordDto record);
        bool HasLetter(LiveDiskDto disk);
        void FlushLockDismount(string letter);
        string RefuseIsolate(LiveDiskDto disk, string bakPath, bool registered);
        void Isolate(LiveDiskDto disk, int level);
        string HideLetter(MediaRecordDto record);
    }

    internal sealed class SystemMediaDiskRuntime : IMediaDiskRuntime
    {
        public LiveDiskDto Find(MediaRecordDto record)
        {
            return record == null
                ? null
                : DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
        }

        public bool HasLetter(LiveDiskDto disk)
        {
            return disk != null
                && !string.IsNullOrEmpty(disk.DriveLetter)
                && Directory.Exists(disk.DriveLetter.TrimEnd('\\') + "\\");
        }

        public void FlushLockDismount(string letter)
        {
            DiskIsolate.FlushLockDismount(letter);
        }

        public string RefuseIsolate(LiveDiskDto disk, string bakPath, bool registered)
        {
            return MediaSafety.RefuseIsolate(disk, bakPath, registered);
        }

        public void Isolate(LiveDiskDto disk, int level)
        {
            DiskIsolate.Isolate(disk, level);
        }

        public string HideLetter(MediaRecordDto record)
        {
            return MediaService.HideLetter(record);
        }
    }

    internal sealed class MediaOfflineLifecycle
    {
        private readonly IMediaDiskRuntime _runtime;

        public MediaOfflineLifecycle(IMediaDiskRuntime runtime)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            _runtime = runtime;
        }

        public MediaOfflineResult Run(
            MediaRecordDto record,
            int requestedLevel,
            string bakPath,
            bool flushFirst)
        {
            var level = requestedLevel < 1 ? 2 : requestedLevel;
            if (record == null)
            {
                return new MediaOfflineResult
                {
                    Status = MediaOfflineStatus.Failed,
                    Level = level,
                    Message = "登记介质不存在。"
                };
            }

            var disk = _runtime.Find(record);
            if (!_runtime.HasLetter(disk))
            {
                return new MediaOfflineResult
                {
                    Status = MediaOfflineStatus.AlreadyOffline,
                    Level = level,
                    Message = "介质已不在线。"
                };
            }

            string warning = null;
            if (flushFirst)
            {
                try
                {
                    _runtime.FlushLockDismount(disk.DriveLetter);
                }
                catch (Exception ex)
                {
                    warning = "刷新未完成：" + ex.Message + "。";
                }
            }

            var refuse = _runtime.RefuseIsolate(disk, bakPath, true);
            if (refuse != null)
            {
                return new MediaOfflineResult
                {
                    Status = MediaOfflineStatus.Refused,
                    Level = level,
                    Warning = warning,
                    Message = refuse
                };
            }

            try
            {
                _runtime.Isolate(disk, level);
                return new MediaOfflineResult
                {
                    Status = MediaOfflineStatus.FullyOffline,
                    Level = level,
                    Warning = warning,
                    Message = "已离线 L" + level + "。"
                };
            }
            catch (Exception isolateError)
            {
                try
                {
                    _runtime.HideLetter(record);
                }
                catch (Exception hideError)
                {
                    return new MediaOfflineResult
                    {
                        Status = MediaOfflineStatus.Failed,
                        Level = level,
                        Warning = warning,
                        Message = "离线失败：" + isolateError.Message + "；摘盘符失败：" + hideError.Message
                    };
                }

                var again = _runtime.Find(record);
                if (!_runtime.HasLetter(again))
                {
                    return new MediaOfflineResult
                    {
                        Status = MediaOfflineStatus.FallbackHidden,
                        Level = level,
                        Warning = warning,
                        Message = "隔离未完成，已去掉盘符。"
                    };
                }

                return new MediaOfflineResult
                {
                    Status = MediaOfflineStatus.Failed,
                    Level = level,
                    Warning = warning,
                    Message = "离线失败：" + isolateError.Message
                };
            }
        }
    }
}
