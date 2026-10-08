using System;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Backup;

namespace YxyXb.Mssql
{
    public static class MssqlRestore
    {
        public static string Restore(MssqlInstanceDto instance, string packagePath, string zipPassword, string targetDb, bool replaceExisting)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                throw new FileNotFoundException("找不到备份文件。", packagePath);
            }

            // SQL Server 服务需能读盘路径，优先放公共目录而非用户 Temp。
            var work = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "用心云循备",
                "mssql-restore");
            Directory.CreateDirectory(work);
            string bakFile = null;
            string staged = null;
            try
            {
                var ext = Path.GetExtension(packagePath) ?? "";
                if (string.Equals(ext, ".bak", StringComparison.OrdinalIgnoreCase))
                {
                    staged = Path.Combine(work, "restore-" + Guid.NewGuid().ToString("N") + ".bak");
                    File.Copy(packagePath, staged, true);
                    bakFile = staged;
                }
                else if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    bakFile = ZipPacker.ExtractFirst(packagePath, zipPassword, work, ".bak");
                    staged = bakFile;
                }
                else
                {
                    throw new InvalidOperationException("请选择 .zip 或 .bak 文件。");
                }

                if (string.IsNullOrWhiteSpace(targetDb))
                {
                    targetDb = Path.GetFileNameWithoutExtension(packagePath);
                    if (string.Equals(Path.GetExtension(packagePath), ".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDb = Path.GetFileNameWithoutExtension(bakFile);
                    }
                }

                MssqlConnect.RestoreDatabase(instance, bakFile, targetDb, replaceExisting);
                return "已恢复到库「" + targetDb.Trim() + "」。";
            }
            finally
            {
                TryClean(staged);
            }
        }

        private static void TryClean(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
        }

    }
}
