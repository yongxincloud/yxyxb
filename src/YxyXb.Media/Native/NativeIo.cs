using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace YxyXb.Media.Native
{
    internal static class NativeIo
    {
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareRead = 1;
        public const uint FileShareWrite = 2;
        public const uint OpenExisting = 3;
        public const uint IoctlStorageQueryProperty = 0x002D1400;
        public const uint IoctlDiskGetLengthInfo = 0x0007405C;
        public const uint IoctlDiskGetDriveLayoutEx = 0x00070050;
        public const uint IoctlDiskSetDiskAttributes = 0x0007C0F0;
        public const uint IoctlStorageEjectMedia = 0x002D4808;
        public const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;
        public const uint IoctlVolumeOffline = 0x0056C00C;
        public const uint IoctlVolumeOnline = 0x0056C008;
        public const uint FsctlLockVolume = 0x00090018;
        public const uint FsctlDismountVolume = 0x00090020;
        public const ulong DiskAttributeOffline = 1;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr inBuffer, int inSize, IntPtr outBuffer, int outSize, out int returned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FlushFileBuffers(SafeFileHandle file);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder name, int length);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetVolumeMountPoint(string mountPoint, string volumeName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool DeleteVolumeMountPoint(string mountPoint);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetVolumePathNamesForVolumeName(string volumeName, [Out] char[] buffer, int length, out int returned);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstVolume(StringBuilder volumeName, int bufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool FindNextVolume(IntPtr find, StringBuilder volumeName, int bufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindVolumeClose(IntPtr find);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Request_Device_Eject(int devInst, out int vetoType, StringBuilder vetoName, int nameLen, int flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Locate_DevNode(out int devInst, string deviceId, int flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Parent(out int parentInst, int devInst, int flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Device_ID(int devInst, StringBuilder buffer, int bufferLen, int flags);

        public static string AncestorIds(string pnpId)
        {
            if (string.IsNullOrEmpty(pnpId))
            {
                return "";
            }

            int inst;
            if (CM_Locate_DevNode(out inst, pnpId, 0) != 0)
            {
                return "";
            }

            var text = new StringBuilder();
            for (var i = 0; i < 10; i++)
            {
                int parent;
                if (CM_Get_Parent(out parent, inst, 0) != 0)
                {
                    break;
                }

                var buffer = new StringBuilder(512);
                if (CM_Get_Device_ID(parent, buffer, buffer.Capacity, 0) == 0)
                {
                    text.Append(' ').Append(buffer);
                }

                inst = parent;
            }

            return text.ToString();
        }

        public static SafeFileHandle Open(string path, bool write)
        {
            var access = write ? GenericRead | GenericWrite : GenericRead;
            return CreateFile(path, access, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        }

        public static bool Ioctl(SafeFileHandle handle, uint code, byte[] input, byte[] output)
        {
            var inPtr = IntPtr.Zero;
            var outPtr = IntPtr.Zero;
            try
            {
                var inSize = input == null ? 0 : input.Length;
                var outSize = output == null ? 0 : output.Length;
                if (inSize > 0)
                {
                    inPtr = Marshal.AllocHGlobal(inSize);
                    Marshal.Copy(input, 0, inPtr, inSize);
                }

                if (outSize > 0)
                {
                    outPtr = Marshal.AllocHGlobal(outSize);
                }

                int returned;
                if (!DeviceIoControl(handle, code, inPtr, inSize, outPtr, outSize, out returned, IntPtr.Zero))
                {
                    return false;
                }

                if (output != null && returned > 0)
                {
                    Marshal.Copy(outPtr, output, 0, Math.Min(returned, output.Length));
                }

                return true;
            }
            finally
            {
                if (inPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(inPtr);
                }

                if (outPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(outPtr);
                }
            }
        }

        public static string BusName(byte bus)
        {
            switch (bus)
            {
                case 4:
                    return "1394";
                case 7:
                    return "USB";
                case 12:
                    return "SD";
                case 13:
                    return "SD";
                default:
                    return "OTHER";
            }
        }

        public static bool IsAllowedBus(string bus)
        {
            return Disks.ExternalBus.IsAllowed(bus);
        }

        public static string VolumeName(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            if (!root.EndsWith("\\", StringComparison.Ordinal))
            {
                root = root + "\\";
            }

            var sb = new StringBuilder(128);
            if (!GetVolumeNameForVolumeMountPoint(root, sb, sb.Capacity))
            {
                return null;
            }

            return sb.ToString();
        }
    }
}
