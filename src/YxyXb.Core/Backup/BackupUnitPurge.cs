using System;
using System.Collections.Generic;
using System.IO;

namespace YxyXb.Core.Backup
{
    public static class BackupUnitPurge
    {
        public static bool IsExpiredUnit(string directory, DateTime olderThanUtc)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            try
            {
                var manifest = PackageSplitter.ScanUnit(directory);
                var files = new List<string>(Directory.GetFiles(directory, "*.manifest.json", SearchOption.TopDirectoryOnly));
                for (var i = 0; i < manifest.Parts.Length; i++)
                {
                    files.Add(Path.Combine(directory, manifest.Parts[i].FileName));
                }

                for (var i = 0; i < files.Count; i++)
                {
                    if (!File.Exists(files[i]) || File.GetLastWriteTimeUtc(files[i]) >= olderThanUtc)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool DeleteLocalUnit(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return true;
            }

            try
            {
                var manifest = PackageSplitter.ScanUnit(directory);
                for (var i = 0; i < manifest.Parts.Length; i++)
                {
                    var path = Path.Combine(directory, manifest.Parts[i].FileName);
                    if (File.Exists(path)) File.Delete(path);
                }

                var manifests = Directory.GetFiles(directory, "*.manifest.json", SearchOption.TopDirectoryOnly);
                for (var i = 0; i < manifests.Length; i++)
                {
                    File.Delete(manifests[i]);
                }

                Directory.Delete(directory, true);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}

