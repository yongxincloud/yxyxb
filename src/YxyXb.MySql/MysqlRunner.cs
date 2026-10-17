using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Store;
using YxyXb.Core.Transfer;

namespace YxyXb.MySql
{
    public static class MysqlRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = MysqlTaskStore.GetDue(db, DateTime.UtcNow);
            var n = 0;
            for (var i = 0; i < due.Count; i++)
            {
                if (!MysqlTaskStore.TryClaim(db, due[i].Id, DateTime.UtcNow))
                {
                    continue;
                }

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

            if (!MysqlTaskStore.TryClaim(db, task.Id, DateTime.UtcNow))
            {
                return "该任务正在执行，请稍候。";
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
                var bakRoot = AppPaths.BackupDirectoryOrThrow(settings.BakPath);
                Directory.CreateDirectory(bakRoot);
                log.Append("备份目录 ").Append(bakRoot).Append("。");
                var dbs = ResolveDatabases(instance, task);
                if (dbs.Count == 0)
                {
                    throw new InvalidOperationException("没有可备份的用户库。");
                }

                log.Append("库 ").Append(dbs.Count).Append(" 个。");
                var names = new string[dbs.Count];
                for (var i = 0; i < dbs.Count; i++)
                {
                    names[i] = dbs[i];
                }

                Array.Sort(names, StringComparer.Ordinal);
                var passwordHash = SourceFingerprint.PasswordHash(settings.ZipPassword);
                var snapshot = new DatabaseSnapshot
                {
                    InstanceId = task.InstanceId,
                    DbNames = names,
                    ZipLevel = settings.ZipLevel,
                    PasswordSha256 = passwordHash
                };
                var record = PendingBackup.Parse(task.PendingJson);
                var decision = PendingBackup.DecideDatabase(record, snapshot, now, File.Exists);
                var track = task.DoNetwork || task.DoFtp || task.DoCloud;
                if (!decision.Reuse)
                {
                    if (!PendingFiles.DeleteReplacements(decision.Replace, log))
                    {
                        throw new InvalidOperationException("删除未传完的包失败，已中止。");
                    }

                    record = PendingBackup.RemoveItems(record, decision.Replace);
                    if (decision.ResetCycle)
                    {
                        record = new PendingRecord
                        {
                            InstanceId = task.InstanceId,
                            DbNames = names,
                            CycleUntilUtc = PendingBackup.CycleUntil(task.CycleKind, task.CycleInterval, now),
                            Items = new PendingItem[0]
                        };
                    }

                    var removedJson = PendingBackup.ToJson(record);
                    MysqlTaskStore.SavePending(db, task.Id, removedJson);
                    task.PendingJson = removedJson;
                }

                string work = null;
                for (var i = 0; i < names.Length; i++)
                {
                    var dbName = names[i];
                    var existing = FindItem(record, dbName);
                    if (existing != null && File.Exists(existing.ZipPath))
                    {
                        log.Append(" 沿用未传完的包 ").Append(Path.GetFileName(existing.ZipPath));
                        zips.Add(existing.ZipPath);
                        continue;
                    }

                    if (work == null)
                    {
                        work = PrepareWorkDir(bakRoot);
                    }

                    var sqlFile = Path.Combine(work, SafeFile(dbName) + ".sql");
                    if (File.Exists(sqlFile)) File.Delete(sqlFile);
                    try
                    {
                        MysqlConnect.DumpDatabase(instance, dbName, sqlFile);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("未能写入备份目录 " + bakRoot + "。" + ex.Message);
                    }
                    var flags = settings.NameFlags == 0 ? NameParts.DefaultFlags : settings.NameFlags;
                    var parts = NameParts.FromFlags(flags);
                    var folder = BackupNaming.MssqlFolder(bakRoot, settings.SoftwareId, task.Name, instance.Name, parts);
                    var zipPath = Path.Combine(folder, BackupNaming.MssqlZipName(settings.SoftwareId, task.Name, dbName, now, parts));
                    try
                    {
                        ZipPacker.Pack(new List<SourceFile> { new SourceFile { FullPath = sqlFile, RelativePath = Path.GetFileName(sqlFile) } }, zipPath, settings.ZipPassword, settings.ZipLevel);
                    }
                    catch (Exception)
                    {
                        PendingFiles.TryDeleteZip(zipPath);
                        throw;
                    }

                    zips.Add(zipPath);
                    TryDelete(sqlFile);
                    log.Append(" ").Append(dbName).Append("→").Append(Path.GetFileName(zipPath));
                    var packed = new PendingItem
                    {
                        Key = dbName,
                        ZipPath = zipPath,
                        Fingerprint = "",
                        ZipLevel = settings.ZipLevel,
                        PasswordSha256 = passwordHash
                    };
                    PendingBackup.Upsert(record, packed);
                    if (track)
                    {
                        var json = PendingBackup.ToJson(record);
                        MysqlTaskStore.SavePending(db, task.Id, json);
                        task.PendingJson = json;
                    }
                }

                if (!track)
                {
                    MysqlTaskStore.SavePending(db, task.Id, null);
                    task.PendingJson = null;
                }

                var distOk = true;
                if (track)
                {
                    var items = record.Items ?? new PendingItem[0];
                    for (var i = 0; i < items.Length; i++)
                    {
                        if (!PendingFiles.UploadItem(
                            db,
                            items[i],
                            task.DoNetwork,
                            task.DoFtp,
                            task.DoCloud,
                            task.CloudProvider,
                            "mysql",
                            task.Name,
                            log,
                            delegate
                            {
                                var json = PendingBackup.ToJson(record);
                                MysqlTaskStore.SavePending(db, task.Id, json);
                                task.PendingJson = json;
                            }))
                        {
                            distOk = false;
                        }
                    }
                }

                if (!DistChannel.Finish(db, task.DoEmail, task.DoDeleteNow, task.Name, zips, distOk, log))
                {
                    throw new InvalidOperationException(log.ToString());
                }

                MysqlTaskStore.SavePending(db, task.Id, null);
                task.PendingJson = null;
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

        private static PendingItem FindItem(PendingRecord record, string dbName)
        {
            if (record == null || record.Items == null)
            {
                return null;
            }

            for (var i = 0; i < record.Items.Length; i++)
            {
                var item = record.Items[i];
                if (item != null && string.Equals(item.Key ?? "", dbName ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static IList<string> ResolveDatabases(MysqlInstanceDto instance, MysqlTaskDto task)
        {
            return DatabaseScope.Pick(
                MysqlConnect.ListUserDatabases(instance),
                task.DbScope,
                task.SelectedDbs,
                MysqlConnect.IsSystemDb);
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
