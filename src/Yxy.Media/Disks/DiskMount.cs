using System;
using System.IO;
using System.Text;
using System.Threading;
using Yxy.Contracts.Dto;
using Yxy.Media.Native;

namespace Yxy.Media.Disks
{
    public static class DiskMount
    {
        public static string VolumeOnDisk(int diskNumber)
        {
            var name = new StringBuilder(128);
            var find = NativeIo.FindFirstVolume(name, name.Capacity);
            if (find == IntPtr.Zero || find == new IntPtr(-1))
            {
                return null;
            }

            try
            {
                do
                {
                    var volume = name.ToString();
                    if (VolumeOnThisDisk(volume, diskNumber))
                    {
                        return volume;
                    }
                }
                while (NativeIo.FindNextVolume(find, name, name.Capacity));
            }
            finally
            {
                NativeIo.FindVolumeClose(find);
            }

            return null;
        }

        private static bool VolumeOnThisDisk(string volume, int diskNumber)
        {
            var path = volume.TrimEnd('\\');
            using (var handle = NativeIo.Open(path, false))
            {
                if (handle.IsInvalid)
                {
                    return false;
                }

                var buffer = new byte[64];
                if (!NativeIo.Ioctl(handle, NativeIo.IoctlVolumeGetVolumeDiskExtents, null, buffer))
                {
                    return false;
                }

                return BitConverter.ToInt32(buffer, 8) == diskNumber;
            }
        }

        public static string EnsureLetter(LiveDiskDto disk)
        {
            if (disk == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(disk.DriveLetter) && Directory.Exists(disk.DriveLetter + "\\"))
            {
                return disk.DriveLetter;
            }

            var volume = disk.VolumeGuid;
            if (string.IsNullOrEmpty(volume))
            {
                return disk.DriveLetter;
            }

            if (!volume.EndsWith("\\", StringComparison.Ordinal))
            {
                volume = volume + "\\";
            }

            using (AutoPlaySuppress.Hold())
            {
                var already = ExistingLetter(volume);
                if (!string.IsNullOrEmpty(already))
                {
                    OnlineLetter(already);
                    disk.DriveLetter = already;
                    disk.VolumeGuid = volume;
                    HoldShell(already);
                    return already;
                }

                var letter = NextFreeLetter();
                if (letter == null)
                {
                    throw new InvalidOperationException("没有可用盘符。");
                }

                letter = NormalizeLetter(letter);
                OnlineVolume(volume, true);

                if (!NativeIo.SetVolumeMountPoint(letter + "\\", volume))
                {
                    Thread.Sleep(600);
                    OnlineVolume(volume, true);
                    if (!NativeIo.SetVolumeMountPoint(letter + "\\", volume))
                    {
                        throw new InvalidOperationException("挂载失败，错误 " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                    }
                }

                OnlineLetter(letter);
                disk.DriveLetter = letter;
                disk.VolumeGuid = volume;
                HoldShell(letter);
                return letter;
            }
        }

        private static void HoldShell(string letter)
        {
            var root = NormalizeLetter(letter).TrimEnd('\\') + "\\";
            for (var i = 0; i < 12 && !Directory.Exists(root); i++)
            {
                Thread.Sleep(500);
            }

            Thread.Sleep(2000);
        }

        public static void OnlineVolume(string volumeGuid, bool online)
        {
            if (string.IsNullOrEmpty(volumeGuid))
            {
                return;
            }

            var path = volumeGuid.TrimEnd('\\');
            using (var handle = NativeIo.Open(path, true))
            {
                if (handle.IsInvalid)
                {
                    return;
                }

                var code = online ? NativeIo.IoctlVolumeOnline : NativeIo.IoctlVolumeOffline;
                NativeIo.Ioctl(handle, code, null, null);
            }
        }

        private static void OnlineLetter(string letter)
        {
            var name = NormalizeLetter(letter);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            using (var handle = NativeIo.Open(@"\\.\" + name.TrimEnd('\\'), true))
            {
                if (!handle.IsInvalid)
                {
                    NativeIo.Ioctl(handle, NativeIo.IoctlVolumeOnline, null, null);
                }
            }
        }

        public static string MountedLetter(string volume)
        {
            if (string.IsNullOrEmpty(volume))
            {
                return null;
            }

            if (!volume.EndsWith("\\", StringComparison.Ordinal))
            {
                volume = volume + "\\";
            }

            return ExistingLetter(volume);
        }

        private static string ExistingLetter(string volume)
        {
            var buffer = new char[512];
            int returned;
            if (!NativeIo.GetVolumePathNamesForVolumeName(volume, buffer, buffer.Length, out returned) || returned <= 1)
            {
                return null;
            }

            var text = new string(buffer, 0, Math.Min(returned, buffer.Length));
            var parts = text.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length >= 2 && part[1] == ':')
                {
                    return part.Substring(0, 2);
                }
            }

            return null;
        }

        public static void RemoveLetter(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return;
            }

            using (AutoPlaySuppress.Hold())
            {
                NativeIo.DeleteVolumeMountPoint(NormalizeLetter(letter) + "\\");
                Thread.Sleep(1500);
            }
        }

        private static string NormalizeLetter(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return letter;
            }

            var text = letter.Trim().TrimEnd('\\');
            if (text.Length == 1)
            {
                text = text + ":";
            }

            return text;
        }

        private static string NextFreeLetter()
        {
            var used = new bool[26];
            var drives = DriveInfo.GetDrives();
            for (var i = 0; i < drives.Length; i++)
            {
                var name = drives[i].Name;
                if (!string.IsNullOrEmpty(name))
                {
                    var c = char.ToUpperInvariant(name[0]);
                    if (c >= 'A' && c <= 'Z')
                    {
                        used[c - 'A'] = true;
                    }
                }
            }

            for (var i = 3; i < 26; i++)
            {
                if (!used[i])
                {
                    return ((char)('A' + i)).ToString() + ":";
                }
            }

            return null;
        }
    }
}
