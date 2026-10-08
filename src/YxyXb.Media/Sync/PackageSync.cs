using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using YxyXb.Core.IO;

namespace YxyXb.Media.Sync
{
    public sealed class FileStamp
    {
        public string Rel { get; set; }

        public long Size { get; set; }

        public string Mtime { get; set; }

        public string Sha256 { get; set; }
    }

    public sealed class VersionManifest
    {
        public string Time { get; set; }

        public List<FileStamp> Files { get; set; }

        public int Copied { get; set; }

        public int Skipped { get; set; }

        public long Bytes { get; set; }
    }

    public static class PackageSync
    {
        public static IList<string> ListBakZips(string bakRoot)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(bakRoot) || !Directory.Exists(bakRoot))
            {
                return list;
            }

            var files = Directory.GetFiles(bakRoot, "*.zip", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".yxypart", StringComparison.OrdinalIgnoreCase)
                    || files[i].IndexOf(".yxypart", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                list.Add(files[i]);
            }

            return list;
        }

        public static string RelFromBak(string bakRoot, string full)
        {
            var prefix = Path.GetFullPath(bakRoot).TrimEnd(Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(full);
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length)
            {
                return path.Substring(prefix.Length + 1);
            }

            return Path.GetFileName(full);
        }

        public static FileStamp Stamp(string full, string rel, bool hash)
        {
            var info = new FileInfo(LongPath.Qualify(full));
            var item = new FileStamp
            {
                Rel = rel.Replace('/', '\\'),
                Size = info.Length,
                Mtime = info.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture)
            };
            if (hash)
            {
                item.Sha256 = Hash(full);
            }

            return item;
        }

        public static bool Same(FileStamp a, FileStamp b, bool hash)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (!string.Equals(Norm(a.Rel), Norm(b.Rel), StringComparison.OrdinalIgnoreCase)
                || a.Size != b.Size
                || !string.Equals(a.Mtime, b.Mtime, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (hash && !string.IsNullOrEmpty(a.Sha256) && !string.IsNullOrEmpty(b.Sha256))
            {
                return string.Equals(a.Sha256, b.Sha256, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        public static void CopyPart(string source, string dest, string conflict)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (File.Exists(dest))
            {
                if (string.Equals(conflict, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                File.Delete(dest);
            }

            var part = dest + ".yxypart";
            if (File.Exists(part))
            {
                File.Delete(part);
            }

            File.Copy(LongPath.Qualify(source), LongPath.Qualify(part), true);
            File.Move(LongPath.Qualify(part), LongPath.Qualify(dest));
        }

        public static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(LongPath.Qualify(path)))
            {
                var raw = sha.ComputeHash(stream);
                var sb = new StringBuilder(raw.Length * 2);
                for (var i = 0; i < raw.Length; i++)
                {
                    sb.Append(raw[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        private static string Norm(string rel)
        {
            return (rel ?? "").Replace('/', '\\').TrimStart('\\');
        }
    }
}
