using System;
using System.IO;

namespace YxyXb.Core.Backup
{
    public static class LocalPurge
    {
        public static int DeleteOldZips(string bakRoot, int days, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(bakRoot) || !Directory.Exists(bakRoot) || days < 1)
            {
                return 0;
            }

            var cutoff = utcNow.AddDays(-days);
            var n = 0;
            string[] files;
            try
            {
                files = Directory.GetFiles(bakRoot, "*.zip", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return 0;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var path = files[i];
                if (path.IndexOf("_mssql_work", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                DateTime mtime;
                try
                {
                    mtime = File.GetLastWriteTimeUtc(path);
                }
                catch (IOException)
                {
                    continue;
                }

                if (mtime >= cutoff)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    n++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return n;
        }
    }
}
