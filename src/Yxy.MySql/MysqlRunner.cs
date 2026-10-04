using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Yxy.Contracts.Dto;
using Yxy.Core;
using Yxy.Core.Backup;
using Yxy.Core.Data;
using Yxy.Core.Store;
using Yxy.Core.Transfer;

namespace Yxy.MySql
{
    public static class MysqlRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = MysqlTaskStore.GetDue(db, DateTime.UtcNow);
            var n = 0;
            for (var i = 0; i < due.Count; i++)
            {
                ExecuteOne(db, due[i], "auto");
                n++;
            }

            return n;
        }

        public static string ExecuteNow(SqliteDb db, long taskId)
        {
            var task = MysqlTaskStore.GetById(db, taskId);
            if (task == null)
            {
                return "任务不存在。";
            }

            ExecuteOne(db, task, "manual");
            var entry = LogStore.LatestForTask(db, "mysql", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 数据库备份」查看。";
            }

            return (entry.Result == "ok" ? "执行成功。" : "执行失败。") + " " + entry.Message;
        }

        public static void ExecuteOne(SqliteDb db, MysqlTaskDto task, string mode)
        {
            var now = DateTime.UtcNow;
            var log = new StringBuilder();
            var ok = false;
            var zips = new List<string>();
            try
            {
                var instance = MysqlInstanceStore.GetById(db, task.InstanceId);
                if (instance == null)
                {
                    throw new InvalidOperationException("实例不存在。");
                }

                var settings = SettingsStore.Get(db);
                var bakRoot = string.IsNullOrEmpty(settings.BakPath) ? AppPaths.BakDirectory : settings.BakPath;
                Directory.CreateDirectory(bakRoot);
                var dbs = ResolveDatabases(instance, task);
                if (dbs.Count == 0)
                {
                    throw new InvalidOperationException("没有可备份的用户库。");
                }

                log.Append("库 ").Append(dbs.Count).Append(" 个。");
                var work = PrepareWorkDir(bakRoot);
                for (var i = 0; i < dbs.Count; i++)
                {
                    var dbName = dbs[i];
                    var sqlFile = Path.Combine(work, SafeFile(dbName) + ".sql");
                    if (File.Exists(sqlFile)) File.Delete(sqlFile);
                    MysqlConnect.DumpDatabase(instance, dbName, sqlFile);
                    var flags = settings.NameFlags == 0 ? NameParts.DefaultFlags : settings.NameFlags;
                    var parts = NameParts.FromFlags(flags);
                    var folder = BackupNaming.MssqlFolder(bakRoot, settings.SoftwareId, task.Name, instance.Name, parts);
                    var zipPath = Path.Combine(folder, BackupNaming.MssqlZipName(settings.SoftwareId, task.Name, dbName, now, parts));
                    ZipPacker.Pack(new List<SourceFile> { new SourceFile { FullPath = sqlFile, RelativePath = Path.GetFileName(sqlFile) } }, zipPath, settings.ZipPassword, settings.ZipLevel);
                    zips.Add(zipPath);
                    TryDelete(sqlFile);
                    log.Append(" ").Append(dbName).Append("→").Append(Path.GetFileName(zipPath));
                }

                var distOk = true;
                for (var i = 0; i < zips.Count; i++)
                {
                    if (!DistChannel.UncAndFtp(db, task.DoNetwork, task.DoFtp, zips[i], log)) distOk = false;
                    if (!DistChannel.Cloud(db, task.DoCloud, task.CloudProvider, "mysql", task.Name, zips[i], log)) distOk = false;
                }

                if (!DistChannel.Finish(db, task.DoEmail, task.DoDeleteNow, task.Name, zips, distOk, log))
                {
                    throw new InvalidOperationException(log.ToString());
                }

                ok = true;
            }
            catch (Exception ex)
            {
                log.Append(" 失败：").Append(ex.Message);
                if (MailUtil.ShouldSend(SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp"), task.DoEmail, false))
                {
                    try
                    {
                        MailUtil.SendResult(SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp"), task.Name, false, log.ToString());
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                MysqlTaskStore.MarkResult(db, task, ok, now);
                LogStore.Add(db, "mysql", string.IsNullOrEmpty(mode) ? "auto" : mode, ok ? "ok" : "error", task.Name, log.ToString());
            }
        }

        private static IList<string> ResolveDatabases(MysqlInstanceDto instance, MysqlTaskDto task)
        {
            var all = MysqlConnect.ListUserDatabases(instance);
            if (task.DbScope != "selected")
            {
                return all;
            }

            var picked = new List<string>();
            var parts = (task.SelectedDbs ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (MysqlConnect.IsSystemDb(name)) continue;
                for (var j = 0; j < all.Count; j++)
                {
                    if (string.Equals(all[j], name, StringComparison.OrdinalIgnoreCase))
                    {
                        picked.Add(all[j]);
                        break;
                    }
                }
            }

            return picked;
        }

        private static string PrepareWorkDir(string bakRoot)
        {
            var dir = Path.Combine(bakRoot, "_mysql_work");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string SafeFile(string name)
        {
            var chars = Path.GetInvalidFileNameChars();
            var text = name ?? "db";
            for (var i = 0; i < chars.Length; i++) text = text.Replace(chars[i], '_');
            return text;
        }

        private static void TryDelete(string path)
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
