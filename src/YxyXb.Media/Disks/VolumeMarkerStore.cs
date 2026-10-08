using System;
using System.IO;
using YxyXb.Core;
using YxyXb.Core.Security;

namespace YxyXb.Media.Disks
{
    public sealed class VolumeMarker
    {
        public string Serial { get; set; }

        public string Signature { get; set; }

        public int CapacityGb { get; set; }

        public string DeviceId { get; set; }

        public string RegisteredUtc { get; set; }

        public string SoftwareId { get; set; }
    }

    public static class VolumeMarkerStore
    {
        public static string MarkerPath(string root)
        {
            return Path.Combine(root.TrimEnd('\\'), ".yxyxb", "volume.marker.json");
        }

        public static string AltMarkerPath(string root)
        {
            return Path.Combine(root.TrimEnd('\\'), AppPaths.ProductPathName, ".yxyxb", "volume.marker.json");
        }

        public static VolumeMarker Read(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var paths = new[] { MarkerPath(root), AltMarkerPath(root) };
            for (var i = 0; i < paths.Length; i++)
            {
                try
                {
                    if (File.Exists(paths[i]))
                    {
                        return JsonUtil.FromJson<VolumeMarker>(File.ReadAllText(paths[i]));
                    }
                }
                catch (IOException)
                {
                }
            }

            return null;
        }

        public static string Write(string root, VolumeMarker marker)
        {
            var json = JsonUtil.ToJson(marker);
            var ok = false;
            Exception last = null;
            var paths = new[] { MarkerPath(root), AltMarkerPath(root) };
            for (var i = 0; i < paths.Length; i++)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(paths[i]));
                    File.WriteAllText(paths[i], json);
                    ok = true;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            return ok ? null : ("无法写入标记：" + (last == null ? "拒绝访问" : last.Message));
        }

        public static void Remove(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return;
            }

            var root = letter.TrimEnd('\\') + "\\";
            TryDelete(MarkerPath(root));
            TryDelete(AltMarkerPath(root));
        }

        public static string CheckForeign(VolumeMarker marker, string thisDeviceId)
        {
            if (marker == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(marker.DeviceId)
                && !string.Equals(marker.DeviceId, thisDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return "该盘已在其他设备登记，拒绝登记。";
            }

            return null;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
