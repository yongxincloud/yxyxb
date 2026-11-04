using System;
using System.Collections.Generic;
using System.IO;
using YxyXb.Core.IO;

namespace YxyXb.Core.Backup
{
    public sealed class SourceFile
    {
        public string FullPath { get; set; }

        public string RelativePath { get; set; }
    }

    public sealed class SourceScanResult
    {
        public IList<SourceFile> Files { get; set; }

        public IList<string> SkippedPaths { get; set; }

        public IList<string> DirectoryErrors { get; set; }

        public BackupRunStats Stats { get; set; }
    }

    public static class SourceWalker
    {
        public static IList<SourceFile> WalkLocal(string root, IList<string> skipped)
        {
            var scan = ScanLocal(root);
            if (skipped != null)
            {
                foreach (var path in scan.SkippedPaths)
                {
                    skipped.Add(path);
                }

                foreach (var path in scan.DirectoryErrors)
                {
                    skipped.Add(path);
                }
            }

            return scan.Files;
        }

        public static SourceScanResult ScanLocal(string root)
        {
            var list = new List<SourceFile>();
            var skipped = new List<string>();
            var directoryErrors = new List<string>();
            var stats = new BackupRunStats();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(LongPath.Qualify(root)))
            {
                throw new DirectoryNotFoundException("源目录不存在：" + root);
            }

            var fullRoot = Path.GetFullPath(root);
            Walk(fullRoot, fullRoot, list, skipped, directoryErrors, stats);
            stats.PackedFiles = list.Count;
            stats.SkippedFiles = skipped.Count;
            return new SourceScanResult
            {
                Files = list,
                SkippedPaths = skipped,
                DirectoryErrors = directoryErrors,
                Stats = stats
            };
        }

        private static void Walk(
            string root,
            string current,
            IList<SourceFile> list,
            IList<string> skipped,
            IList<string> directoryErrors,
            BackupRunStats stats)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(LongPath.Qualify(current));
            }
            catch (UnauthorizedAccessException)
            {
                directoryErrors.Add(current);
                return;
            }
            catch (IOException)
            {
                directoryErrors.Add(current);
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                stats.DiscoveredFiles++;
                var file = files[i];
                var display = file.StartsWith(@"\\?\", StringComparison.Ordinal) ? file.Substring(4) : file;
                if (display.StartsWith(@"UNC\", StringComparison.Ordinal))
                {
                    display = @"\\" + display.Substring(4);
                }

                try
                {
                    using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                    }

                    list.Add(new SourceFile
                    {
                        FullPath = file,
                        RelativePath = Rel(root, display)
                    });
                    stats.SourceBytes += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                    skipped.Add(display);
                }
                catch (UnauthorizedAccessException)
                {
                    skipped.Add(display);
                }
            }

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(LongPath.Qualify(current));
            }
            catch (UnauthorizedAccessException)
            {
                directoryErrors.Add(current);
                return;
            }
            catch (IOException)
            {
                directoryErrors.Add(current);
                return;
            }

            for (var i = 0; i < dirs.Length; i++)
            {
                Walk(root, dirs[i], list, skipped, directoryErrors, stats);
            }
        }

        private static string Rel(string root, string full)
        {
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && full.Length > prefix.Length)
            {
                return full.Substring(prefix.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return Path.GetFileName(full);
        }
    }
}
