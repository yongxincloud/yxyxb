using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace YxyXb.Core.Backup
{
    public static class SliceLog
    {
        private static readonly object Gate = new object();

        public static void Write(string directory, string localPath, string message)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName(localPath));
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + (message ?? "") + Environment.NewLine;
            lock (Gate)
            {
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
        }

        public static string ReadRecent(string dataDir, int maxChars)
        {
            if (maxChars < 200)
            {
                maxChars = 200;
            }

            var files = new List<string>();
            Collect(files, Path.Combine(dataDir ?? "", "baidu-upload"));
            Collect(files, Path.Combine(dataDir ?? "", "qiniu-upload"));
            if (files.Count == 0)
            {
                return "";
            }

            files.Sort(delegate(string a, string b)
            {
                return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
            });

            var sb = new StringBuilder();
            var take = files.Count < 6 ? files.Count : 6;
            for (var i = 0; i < take; i++)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                var folder = Path.GetFileName(Path.GetDirectoryName(files[i]) ?? "");
                var cloud = folder.IndexOf("qiniu", StringComparison.OrdinalIgnoreCase) >= 0 ? "七牛" : "百度";
                sb.AppendLine("---- " + cloud + " " + Path.GetFileName(files[i]) + " ----");
                try
                {
                    sb.Append(File.ReadAllText(files[i], new UTF8Encoding(false)));
                }
                catch (IOException)
                {
                }
            }

            var text = sb.ToString();
            if (text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(text.Length - maxChars);
        }

        private static void Collect(List<string> files, string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }

            try
            {
                files.AddRange(Directory.GetFiles(dir, "*.log"));
            }
            catch (IOException)
            {
            }
        }

        private static string FileName(string localPath)
        {
            var name = Path.GetFileName(localPath);
            if (string.IsNullOrEmpty(name))
            {
                name = "upload";
            }

            var invalid = Path.GetInvalidFileNameChars();
            for (var i = 0; i < invalid.Length; i++)
            {
                name = name.Replace(invalid[i], '_');
            }

            var hash = Hash(localPath);
            var shortHash = hash.Length > 12 ? hash.Substring(0, 12) : hash;
            return name + "-" + shortHash + ".log";
        }

        private static string Hash(string localPath)
        {
            var norm = localPath ?? "";
            try
            {
                if (!string.IsNullOrEmpty(localPath))
                {
                    norm = Path.GetFullPath(localPath);
                }
            }
            catch (Exception)
            {
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(norm.ToLowerInvariant()));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
