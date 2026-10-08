using System;
using System.IO;
using System.Text;

namespace YxyXb.Baidu
{
    public static class BaiduUploadLog
    {
        private static readonly object Gate = new object();

        public static string PathFor(string dataDir, string localPath)
        {
            var json = BaiduUploadCheckpoint.FilePath(dataDir, localPath);
            var dir = Path.GetDirectoryName(json) ?? "";
            var hash = Path.GetFileNameWithoutExtension(json) ?? "file";
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

            var shortHash = hash.Length > 12 ? hash.Substring(0, 12) : hash;
            return Path.Combine(dir, name + "-" + shortHash + ".log");
        }

        public static void Write(string dataDir, string localPath, string message)
        {
            var path = PathFor(dataDir, localPath);
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

            var dir = Path.Combine(dataDir ?? "", "baidu-upload");
            if (!Directory.Exists(dir))
            {
                return "";
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.log");
            }
            catch (IOException)
            {
                return "";
            }

            Array.Sort(files, delegate(string a, string b)
            {
                return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
            });

            var sb = new StringBuilder();
            var take = files.Length < 3 ? files.Length : 3;
            for (var i = 0; i < take; i++)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("---- " + Path.GetFileName(files[i]) + " ----");
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
    }
}
