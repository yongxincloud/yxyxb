using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Yxy.Core.Security;

namespace Yxy.Media.Sync
{
    public static class VersionPurge
    {
        public static IList<string> KeepFiles(IList<VersionManifest> versions, int keepM, int keepDays, DateTime utcNow)
        {
            var sorted = new List<VersionManifest>();
            if (versions != null)
            {
                for (var i = 0; i < versions.Count; i++)
                {
                    if (versions[i] != null)
                    {
                        sorted.Add(versions[i]);
                    }
                }
            }

            sorted.Sort(CompareTimeDesc);
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var kept = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                var ageOk = AgeDays(sorted[i].Time, utcNow) <= keepDays;
                if (kept < keepM && ageOk)
                {
                    AddFiles(keep, sorted[i]);
                    kept++;
                }
            }

            return new List<string>(keep);
        }

        public static int Apply(string filesRoot, string versionsRoot, int keepM, int keepDays, bool allow)
        {
            if (!allow)
            {
                return 0;
            }

            Directory.CreateDirectory(versionsRoot);
            var manifests = new List<VersionManifest>();
            var map = new Dictionary<VersionManifest, string>();
            var names = Directory.Exists(versionsRoot)
                ? Directory.GetFiles(versionsRoot, "*.json")
                : new string[0];
            for (var i = 0; i < names.Length; i++)
            {
                var item = JsonUtil.FromJson<VersionManifest>(File.ReadAllText(names[i]));
                if (item != null)
                {
                    if (string.IsNullOrEmpty(item.Time))
                    {
                        item.Time = Path.GetFileNameWithoutExtension(names[i]);
                    }

                    manifests.Add(item);
                    map[item] = names[i];
                }
            }

            var keepFiles = new HashSet<string>(KeepFiles(manifests, keepM, keepDays, DateTime.UtcNow), StringComparer.OrdinalIgnoreCase);
            var keepSet = new HashSet<VersionManifest>();
            var sorted = new List<VersionManifest>(manifests);
            sorted.Sort(CompareTimeDesc);
            var kept = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                if (kept < keepM && AgeDays(sorted[i].Time, DateTime.UtcNow) <= keepDays)
                {
                    keepSet.Add(sorted[i]);
                    kept++;
                }
            }

            var deleted = 0;
            for (var i = 0; i < manifests.Count; i++)
            {
                if (keepSet.Contains(manifests[i]))
                {
                    continue;
                }

                File.Delete(map[manifests[i]]);
                deleted++;
            }

            if (Directory.Exists(filesRoot))
            {
                var zips = Directory.GetFiles(filesRoot, "*.zip", SearchOption.AllDirectories);
                for (var i = 0; i < zips.Length; i++)
                {
                    var rel = Rel(filesRoot, zips[i]);
                    if (!keepFiles.Contains(rel))
                    {
                        File.Delete(zips[i]);
                    }
                }
            }

            return deleted;
        }

        private static void AddFiles(HashSet<string> keep, VersionManifest manifest)
        {
            if (manifest.Files == null)
            {
                return;
            }

            for (var i = 0; i < manifest.Files.Count; i++)
            {
                if (manifest.Files[i] != null && !string.IsNullOrEmpty(manifest.Files[i].Rel))
                {
                    keep.Add(manifest.Files[i].Rel.Replace('/', '\\'));
                }
            }
        }

        private static int CompareTimeDesc(VersionManifest a, VersionManifest b)
        {
            return string.Compare(b.Time, a.Time, StringComparison.Ordinal);
        }

        private static double AgeDays(string time, DateTime utcNow)
        {
            DateTime local;
            if (DateTime.TryParseExact(time, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out local))
            {
                return (utcNow - local.ToUniversalTime()).TotalDays;
            }

            return 0;
        }

        private static string Rel(string root, string full)
        {
            var prefix = Path.GetFullPath(root).TrimEnd('\\');
            var path = Path.GetFullPath(full);
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length)
            {
                return path.Substring(prefix.Length + 1);
            }

            return Path.GetFileName(full);
        }
    }
}
