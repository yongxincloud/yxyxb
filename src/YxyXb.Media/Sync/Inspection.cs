using System;
using System.IO;
using YxyXb.Core.Security;

namespace YxyXb.Media.Sync
{
    public static class Inspection
    {
        public static string LatestPath(string versionsRoot)
        {
            if (!Directory.Exists(versionsRoot))
            {
                return null;
            }

            var files = Directory.GetFiles(versionsRoot, "*.json");
            if (files.Length == 0)
            {
                return null;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files[files.Length - 1];
        }

        public static bool LooksTampered(string filesRoot, string versionsRoot, bool useHash)
        {
            var latest = LatestPath(versionsRoot);
            if (latest == null)
            {
                return false;
            }

            var manifest = JsonUtil.FromJson<VersionManifest>(File.ReadAllText(latest));
            if (manifest == null || manifest.Files == null)
            {
                return false;
            }

            for (var i = 0; i < manifest.Files.Count; i++)
            {
                var item = manifest.Files[i];
                if (item == null || string.IsNullOrEmpty(item.Rel))
                {
                    continue;
                }

                var path = Path.Combine(filesRoot, item.Rel);
                if (!File.Exists(path))
                {
                    return true;
                }

                var now = PackageSync.Stamp(path, item.Rel, useHash && !string.IsNullOrEmpty(item.Sha256));
                if (!PackageSync.Same(item, now, useHash && !string.IsNullOrEmpty(item.Sha256)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
