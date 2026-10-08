using System;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;

namespace YxyXb.MySql
{
    public static class MysqlRestore
    {
        public static string Restore(MysqlInstanceDto instance, string packagePath, string zipPassword, string targetDb, bool replaceExisting)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                throw new FileNotFoundException("找不到备份文件。", packagePath);
            }

            var work = Path.Combine(Path.GetTempPath(), "yxy-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string sqlFile = null;
            try
            {
                var ext = Path.GetExtension(packagePath) ?? "";
                if (string.Equals(ext, ".sql", StringComparison.OrdinalIgnoreCase))
                {
                    sqlFile = packagePath;
                }
                else if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    sqlFile = ZipPacker.ExtractFirst(packagePath, zipPassword, work, ".sql");
                }
                else
                {
                    throw new InvalidOperationException("请选择 .zip 或 .sql 文件。");
                }

                if (string.IsNullOrWhiteSpace(targetDb))
                {
                    targetDb = Path.GetFileNameWithoutExtension(sqlFile);
                }

                MysqlConnect.RestoreDatabase(instance, sqlFile, targetDb, replaceExisting);
                return "已恢复到库「" + targetDb.Trim() + "」。";
            }
            finally
            {
                TryClean(work, sqlFile == packagePath ? null : sqlFile);
            }
        }

        private static void TryClean(string work, string extracted)
        {
            try
            {
                if (!string.IsNullOrEmpty(extracted) && File.Exists(extracted)) File.Delete(extracted);
            }
            catch (IOException)
            {
            }

            try
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
