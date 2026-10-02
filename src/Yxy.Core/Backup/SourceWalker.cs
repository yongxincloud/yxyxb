using System;
using System.Collections.Generic;
using System.IO;
using Yxy.Core.IO;

namespace Yxy.Core.Backup
{
    public sealed class SourceFile
    {
        public string FullPath { get; set; }

        public string RelativePath { get; set; }
    }

    public static class SourceWalker
    {
        public static IList<SourceFile> WalkLocal(string root, IList<string> skipped)
        {
            var list = new List<SourceFile>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(LongPath.Qualify(root)))
            {
                throw new DirectoryNotFoundException("源目录不存在：" + root);
            }

            var fullRoot = Path.GetFullPath(root);
            Walk(fullRoot, fullRoot, list, skipped);
            return list;
        }

        private static void Walk(string root, string current, IList<SourceFile> list, IList<string> skipped)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(LongPath.Qualify(current));
            }
            catch (UnauthorizedAccessException)
            {
                skipped.Add(current);
                return;
            }
            catch (IOException)
            {
                skipped.Add(current);
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
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
                skipped.Add(current);
                return;
            }
            catch (IOException)
            {
                skipped.Add(current);
                return;
            }

            for (var i = 0; i < dirs.Length; i++)
            {
                Walk(root, dirs[i], list, skipped);
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
