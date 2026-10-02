using System;
using System.Text;
using System.Threading;
using Yxy.Contracts.Dto;
using Yxy.Media.Native;

namespace Yxy.Media.Disks
{
    public static class DiskIsolate
    {
        public static void Isolate(LiveDiskDto disk, int level)
        {
            if (disk == null)
            {
                throw new InvalidOperationException("无磁盘可隔离。");
            }

            if (level < 1)
            {
                level = 3;
            }

            Exception last = null;
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    RunLevel(disk, level);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(800);
                }
            }

            if (level >= 3)
            {
                try
                {
                    RunLevel(disk, 2);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            throw last ?? new InvalidOperationException("隔离失败。");
        }

        private static void RunLevel(LiveDiskDto disk, int level)
        {
            var letter = disk.DriveLetter;
            if (level >= 2 && !string.IsNullOrEmpty(letter))
            {
                TryVolumeOffline(letter);
            }

            if (!string.IsNullOrEmpty(letter))
            {
                DiskMount.RemoveLetter(letter);
                disk.DriveLetter = null;
            }

            if (level >= 3)
            {
                DiskOffline(disk.DiskNumber);
                disk.Online = false;
            }

            if (level >= 3)
            {
                TryEject(disk.PnpDeviceId);
            }

            if (level >= 4)
            {
                Eject(disk.PnpDeviceId);
            }
        }

        public static void FlushLockDismount(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return;
            }

            using (var handle = NativeIo.Open(@"\\.\" + letter.TrimEnd('\\'), true))
            {
                if (handle.IsInvalid)
                {
                    throw new InvalidOperationException("无法打开卷做刷新。");
                }

                NativeIo.FlushFileBuffers(handle);
                int returned;
                NativeIo.DeviceIoControl(handle, NativeIo.FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero);
                NativeIo.DeviceIoControl(handle, NativeIo.FsctlDismountVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero);
            }
        }

        private static void TryVolumeOffline(string letter)
        {
            try
            {
                VolumeOffline(letter);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void VolumeOffline(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return;
            }

            using (var handle = NativeIo.Open(@"\\.\" + letter.TrimEnd('\\'), true))
            {
                if (handle.IsInvalid || !NativeIo.Ioctl(handle, NativeIo.IoctlVolumeOffline, null, null))
                {
                    throw new InvalidOperationException("卷离线失败。");
                }
            }
        }

        public static void HideFromExplorer(LiveDiskDto disk)
        {
            if (disk == null)
            {
                throw new InvalidOperationException("无磁盘可离线。");
            }

            var letter = disk.DriveLetter;
            if (string.IsNullOrEmpty(letter))
            {
                DiskMount.OnlineVolume(disk.VolumeGuid, false);
                return;
            }

            VolumeOffline(letter);
            DiskMount.RemoveLetter(letter);
            disk.DriveLetter = null;
            DiskMount.OnlineVolume(disk.VolumeGuid, false);
        }

        public static void BringOnline(LiveDiskDto disk, string volumeGuid)
        {
            if (disk == null)
            {
                throw new InvalidOperationException("无磁盘可上线。");
            }

            try
            {
                using (var handle = NativeIo.Open(@"\\.\PhysicalDrive" + disk.DiskNumber, true))
                {
                    if (!handle.IsInvalid)
                    {
                        var buf = new byte[24];
                        BitConverter.GetBytes(24).CopyTo(buf, 0);
                        buf[4] = 1;
                        BitConverter.GetBytes(0UL).CopyTo(buf, 8);
                        BitConverter.GetBytes(NativeIo.DiskAttributeOffline).CopyTo(buf, 16);
                        NativeIo.Ioctl(handle, NativeIo.IoctlDiskSetDiskAttributes, buf, null);
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }

            if (string.IsNullOrEmpty(volumeGuid))
            {
                return;
            }

            disk.VolumeGuid = volumeGuid;
            DiskMount.OnlineVolume(volumeGuid, true);
        }

        private static void DiskOffline(int diskNumber)
        {
            using (var handle = NativeIo.Open(@"\\.\PhysicalDrive" + diskNumber, true))
            {
                if (handle.IsInvalid)
                {
                    throw new InvalidOperationException("无法打开磁盘做离线。");
                }

                var buf = new byte[24];
                BitConverter.GetBytes(24).CopyTo(buf, 0);
                buf[4] = 1;
                BitConverter.GetBytes(NativeIo.DiskAttributeOffline).CopyTo(buf, 8);
                BitConverter.GetBytes(NativeIo.DiskAttributeOffline).CopyTo(buf, 16);
                NativeIo.Ioctl(handle, NativeIo.IoctlStorageEjectMedia, null, null);
                if (!NativeIo.Ioctl(handle, NativeIo.IoctlDiskSetDiskAttributes, buf, null))
                {
                    throw new InvalidOperationException("整盘离线失败，错误 " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }
            }
        }

        private static void TryEject(string pnpId)
        {
            try
            {
                if (!string.IsNullOrEmpty(pnpId))
                {
                    Eject(pnpId);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void Eject(string pnpId)
        {
            if (string.IsNullOrEmpty(pnpId))
            {
                throw new InvalidOperationException("无 PnP 标识，无法弹出。");
            }

            int devInst;
            if (NativeIo.CM_Locate_DevNode(out devInst, pnpId, 0) != 0)
            {
                throw new InvalidOperationException("定位设备失败。");
            }

            for (var i = 0; i < 6; i++)
            {
                int veto;
                var name = new StringBuilder(256);
                if (NativeIo.CM_Request_Device_Eject(devInst, out veto, name, name.Capacity, 0) == 0)
                {
                    return;
                }

                int parent;
                if (NativeIo.CM_Get_Parent(out parent, devInst, 0) != 0)
                {
                    break;
                }

                devInst = parent;
            }

            throw new InvalidOperationException("PnP 弹出失败。");
        }
    }
}
