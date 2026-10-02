using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using Yxy.Contracts.Dto;
using Yxy.Media.Native;

namespace Yxy.Media.Disks
{
    public static class DiskEnumerator
    {
        public static IList<LiveDiskDto> ListRemovable()
        {
            var list = new List<LiveDiskDto>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Index, SerialNumber, Size, PNPDeviceID, DeviceID, InterfaceType, Caption, Model, MediaType FROM Win32_DiskDrive"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject drive in results)
                    {
                        using (drive)
                        {
                            var item = FromWmi(drive);
                            if (item != null)
                            {
                                list.Add(item);
                            }
                        }
                    }
                }
            }
            catch (ManagementException)
            {
            }

            return list;
        }

        public static LiveDiskDto Find(string serial, string signature, int capacityGb)
        {
            var all = ListRemovable();
            for (var i = 0; i < all.Count; i++)
            {
                if (SameDisk(all[i], serial, signature, capacityGb))
                {
                    return all[i];
                }
            }

            return null;
        }

        public static bool SameDisk(LiveDiskDto disk, string serial, string signature, int capacityGb)
        {
            if (disk == null)
            {
                return false;
            }

            if (!string.Equals(disk.SerialNumber, serial, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!SameSignature(disk.DiskSignature, signature))
            {
                return false;
            }

            if (capacityGb > 0 && disk.CapacityGb > 0 && disk.CapacityGb != capacityGb)
            {
                return false;
            }

            return true;
        }

        private static bool SameSignature(string live, string stored)
        {
            if (string.Equals(live, stored, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsFallbackSignature(live) || IsFallbackSignature(stored);
        }

        private static bool IsFallbackSignature(string signature)
        {
            return !string.IsNullOrEmpty(signature)
                && (signature.StartsWith("wmi-", StringComparison.OrdinalIgnoreCase)
                    || signature.StartsWith("sig", StringComparison.OrdinalIgnoreCase));
        }

        private static LiveDiskDto FromWmi(ManagementObject drive)
        {
            var index = Convert.ToInt32(drive["Index"]);
            string serial;
            string signature;
            byte bus;
            long size;
            if (!ReadPhysical(index, out serial, out signature, out bus, out size))
            {
                serial = Trim(drive["SerialNumber"]);
                signature = "wmi-" + index;
                bus = 0;
            }

            if (string.IsNullOrEmpty(serial))
            {
                serial = Trim(drive["SerialNumber"]);
            }

            if (size <= 0)
            {
                size = drive["Size"] == null ? 0 : Convert.ToInt64(drive["Size"]);
            }

            var pnp = Trim(drive["PNPDeviceID"]);
            var extra = Trim(drive["Caption"]) + " " + Trim(drive["Model"]) + " " + Trim(drive["MediaType"]);
            var busName = ExternalBus.Resolve(bus, pnp, Trim(drive["InterfaceType"]), extra, NativeIo.AncestorIds(pnp));
            if (!ExternalBus.IsAllowed(busName))
            {
                return null;
            }

            var letter = FindLetter(index);
            var system = IsSystemDisk(index, letter);
            var item = new LiveDiskDto
            {
                DiskNumber = index,
                SerialNumber = string.IsNullOrEmpty(serial) ? "disk" + index : serial,
                DiskSignature = string.IsNullOrEmpty(signature) ? "sig" + index : signature,
                CapacityGb = size <= 0 ? 0 : (int)Math.Max(1, size / (1024L * 1024L * 1024L)),
                BusType = busName,
                DriveLetter = letter,
                VolumeGuid = string.IsNullOrEmpty(letter) ? null : NativeIo.VolumeName(letter + "\\"),
                IsSystem = system,
                Online = true,
                PnpDeviceId = Trim(drive["PNPDeviceID"])
            };
            FillFreeSpace(item);
            return item;
        }

        private static bool ReadPhysical(int index, out string serial, out string signature, out byte bus, out long size)
        {
            serial = null;
            signature = null;
            bus = 0;
            size = 0;
            using (var handle = NativeIo.Open(@"\\.\PhysicalDrive" + index, false))
            {
                if (handle.IsInvalid)
                {
                    return false;
                }

                var query = new byte[12];
                query[0] = 0;
                var buffer = new byte[1024];
                if (NativeIo.Ioctl(handle, NativeIo.IoctlStorageQueryProperty, query, buffer))
                {
                    bus = buffer[28];
                    var serialOffset = BitConverter.ToInt32(buffer, 24);
                    if (serialOffset > 0 && serialOffset < buffer.Length)
                    {
                        var end = serialOffset;
                        while (end < buffer.Length && buffer[end] != 0)
                        {
                            end++;
                        }

                        serial = Trim(System.Text.Encoding.ASCII.GetString(buffer, serialOffset, end - serialOffset));
                    }
                }

                var layout = new byte[4096];
                if (NativeIo.Ioctl(handle, NativeIo.IoctlDiskGetDriveLayoutEx, null, layout))
                {
                    var style = BitConverter.ToInt32(layout, 0);
                    if (style == 0)
                    {
                        signature = BitConverter.ToUInt32(layout, 8).ToString("X8");
                    }
                    else
                    {
                        var guid = new byte[16];
                        Buffer.BlockCopy(layout, 8, guid, 0, 16);
                        signature = new Guid(guid).ToString("N");
                    }
                }

                var length = new byte[8];
                if (NativeIo.Ioctl(handle, NativeIo.IoctlDiskGetLengthInfo, null, length))
                {
                    size = BitConverter.ToInt64(length, 0);
                }
            }

            return true;
        }

        private static string FindLetter(int diskIndex)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='\\\\.\\PHYSICALDRIVE" + diskIndex + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition"))
                using (var parts = searcher.Get())
                {
                    foreach (ManagementObject part in parts)
                    {
                        using (part)
                        using (var ls = new ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + part["DeviceID"] + "'} WHERE AssocClass = Win32_LogicalDiskToPartition"))
                        using (var disks = ls.Get())
                        {
                            foreach (ManagementObject logical in disks)
                            {
                                using (logical)
                                {
                                    var id = Convert.ToString(logical["DeviceID"]);
                                    if (!string.IsNullOrEmpty(id))
                                    {
                                        return id.TrimEnd('\\').ToUpperInvariant();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (ManagementException)
            {
            }

            return null;
        }

        private static bool IsSystemDisk(int diskIndex, string letter)
        {
            var sys = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
            if (!string.IsNullOrEmpty(letter) && !string.IsNullOrEmpty(sys)
                && string.Equals(letter.TrimEnd('\\') + "\\", sys, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var boot = FindLetter(0);
                if (diskIndex == 0 && !string.IsNullOrEmpty(boot))
                {
                    return true;
                }
            }
            catch (IOException)
            {
            }

            return false;
        }

        public static void FillFreeSpace(LiveDiskDto disk)
        {
            if (disk == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(disk.DriveLetter))
            {
                disk.FreeBytes = 0;
                disk.FreeSpaceText = "未挂载";
                return;
            }

            try
            {
                var info = new DriveInfo(disk.DriveLetter.TrimEnd('\\') + "\\");
                if (!info.IsReady)
                {
                    disk.FreeSpaceText = "未就绪";
                    return;
                }

                disk.FreeBytes = info.AvailableFreeSpace;
                disk.FreeSpaceText = FormatFree(info.AvailableFreeSpace);
            }
            catch (IOException)
            {
                disk.FreeSpaceText = "-";
            }
            catch (UnauthorizedAccessException)
            {
                disk.FreeSpaceText = "-";
            }
        }

        public static string FormatFree(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
            {
                return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + " GB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("0") + " MB";
        }

        private static string Trim(object value)
        {
            return value == null ? "" : Convert.ToString(value).Replace("\0", "").Trim();
        }
    }
}
