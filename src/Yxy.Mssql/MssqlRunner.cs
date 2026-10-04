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

namespace Yxy.Mssql
{
    public static class MssqlRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = MssqlTaskStore.GetDue(db, DateTime.UtcNow);
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
            var task = MssqlTaskStore.GetById(db, taskId);
            if (task == null)
            {
                return "任务不存在。";
            }

            ExecuteOne(db, task, "manual");
            var entry = LogStore.LatestForTask(db, "mssql", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 数据库备份」查看。";
            }

            return (entry.Result == "ok" ? "执行成功。" : "执行失败。") + " " + entry.Message;
        }

        public static void ExecuteOne(SqliteDb db, MssqlTaskDto task, string mode)
        {
            var now = DateTime.UtcNow;
            var log = new StringBuilder();
            var ok = false;
            var zips = new List<string>();
            try
            {
                var instance = MssqlInstanceStore.GetById(db, task.InstanceId);
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
                    var bakFile = Path.Combine(work, SafeFile(dbName) + ".bak");
                    if (File.Exists(bakFile))
                    {
                        File.Delete(bakFile);
                    }

                    MssqlConnect.BackupDatabase(instance, dbName, bakFile);
                    var flags = settings.NameFlags == 0 ? NameParts.DefaultFlags : settings.NameFlags;
                    var parts = NameParts.FromFlags(flags);
                    var folder = BackupNaming.MssqlFolder(bakRoot, settings.SoftwareId, task.Name, instance.Name, parts);
                    var zipPath = Path.Combine(folder, BackupNaming.MssqlZipName(settings.SoftwareId, task.Name, dbName, now, parts));
                    var files = new List<SourceFile>
                    {
                        new SourceFile { FullPath = bakFile, RelativePath = Path.GetFileName(bakFile) }
                    };
                    ZipPacker.Pack(files, zipPath, settings.ZipPassword, settings.ZipLevel);
                    zips.Add(zipPath);
                    TryDelete(bakFile);
                    log.Append(" ").Append(dbName).Append("→").Append(Path.GetFileName(zipPath));
                    log.Append(string.IsNullOrEmpty(settings.ZipPassword) ? "。" : "，已用压缩密码。");
                }

                var distOk = true;
                for (var i = 0; i < zips.Count; i++)
                {
                    if (!DistChannel.UncAndFtp(db, task.DoNetwork, task.DoFtp, zips[i], log))
                    {
                        distOk = false;
                    }

                    if (!DistChannel.Cloud(db, task.DoCloud, task.CloudProvider, "mssql", task.Name, zips[i], log))
                    {
                        distOk = false;
                    }
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
                MssqlTaskStore.MarkResult(db, task, ok, now);
                LogStore.Add(db, "mssql", string.IsNullOrEmpty(mode) ? "auto" : mode, ok ? "ok" : "error", task.Name, log.ToString());
            }
        }

        private static IList<string> ResolveDatabases(MssqlInstanceDto instance, MssqlTaskDto task)
        {
            var all = MssqlConnect.ListUserDatabases(instance);
            if (task.DbScope != "selected")
            {
                return all;
            }

            var picked = new List<string>();
            var parts = (task.SelectedDbs ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (MssqlConnect.IsSystemDb(name))
                {
                    continue;
                }

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
            var dirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "用心云备份", "mssql-work"),
                Path.Combine(bakRoot, "_mssql_work")
            };
            Exception last = null;
            for (var i = 0; i < dirs.Length; i++)
            {
                try
                {
                    Directory.CreateDirectory(dirs[i]);
                    var probe = Path.Combine(dirs[i], "probe.tmp");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return dirs[i];
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            throw new InvalidOperationException("无法创建 MSSQL 工作目录：" + (last == null ? "" : last.Message));
        }

        private static string SafeFile(string name)
        {
            var chars = Path.GetInvalidFileNameChars();
            var text = name ?? "db";
            for (var i = 0; i < chars.Length; i++)
            {
                text = text.Replace(chars[i], '_');
            }

            return text;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
