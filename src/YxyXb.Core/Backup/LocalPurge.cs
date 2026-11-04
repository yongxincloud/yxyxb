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
            try
            {
                var units = Directory.GetDirectories(bakRoot, "*.split-*", SearchOption.AllDirectories);
                Array.Sort(units, delegate(string a, string b)
                {
                    return b.Length.CompareTo(a.Length);
                });
                for (var i = 0; i < units.Length; i++)
                {
                    if (BackupUnitPurge.IsExpiredUnit(units[i], cutoff)
                        && BackupUnitPurge.DeleteLocalUnit(units[i]))
                    {
                        n++;
                    }
                }
            }
            catch (IOException)
            {
            }

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
                if (path.IndexOf(".split-", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
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
