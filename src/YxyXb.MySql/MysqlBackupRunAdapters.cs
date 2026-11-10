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
    internal sealed class MysqlBackupTaskState : IBackupRunTask
    {
        private readonly SqliteDb _db;
        private readonly MysqlTaskDto _task;

        public MysqlBackupTaskState(SqliteDb db, MysqlTaskDto task)
        {
            _db = db;
            _task = task;
        }

        public string LogType
        {
            get { return "mysql"; }
        }

        public string TaskName
        {
            get { return _task.Name; }
        }

        public string LoadPendingJson()
        {
            return _task.PendingJson;
        }

        public void SavePendingJson(string json)
        {
            MysqlTaskStore.SavePending(_db, _task.Id, json);
            _task.PendingJson = json;
        }

        public void MarkResult(bool ok, DateTime utcNow)
        {
            MysqlTaskStore.MarkResult(_db, _task, ok, utcNow);
        }

        public void WriteLog(string mode, string result, string message)
        {
            LogStore.Add(_db, LogType, mode, result, _task.Name, message);
        }
    }

    internal sealed class MysqlBackupSource : IBackupSourceAdapter
    {
        private readonly SqliteDb _db;
        private readonly MysqlTaskDto _task;
        private MysqlInstanceDto _instance;
        private AppSettingsDto _settings;
        private string _backupRoot;
        private string[] _names;
        private BackupRunStats _stats;
        private PendingDecision _decision;

        public MysqlBackupSource(SqliteDb db, MysqlTaskDto task)
        {
            _db = db;
            _task = task;
        }

        public BackupSourceInspection Inspect(PendingRecord pending, DateTime utcNow, StringBuilder log)
        {
            _instance = MysqlInstanceStore.GetById(_db, _task.InstanceId);
            if (_instance == null)
            {
                throw new InvalidOperationException("实例不存在。");
            }

            _settings = SettingsStore.Get(_db);
            _backupRoot = AppPaths.BackupDirectoryOrThrow(_settings.BakPath);
            Directory.CreateDirectory(_backupRoot);
            log.Append("备份目录 ").Append(_backupRoot).Append("。");

            var databases = ResolveDatabases(_instance, _task);
            if (databases.Count == 0)
            {
                throw new InvalidOperationException("没有可备份的用户库。");
            }

            _stats = new BackupRunStats { DiscoveredFiles = databases.Count };
            log.Append("数据库 ").Append(databases.Count).Append(" 个。");
            _names = new string[databases.Count];
            for (var i = 0; i < databases.Count; i++)
            {
                _names[i] = databases[i];
            }

            Array.Sort(_names, StringComparer.Ordinal);
            var passwordHash = SourceFingerprint.PasswordHash(_settings.ZipPassword);
            _decision = PendingBackup.DecideDatabase(
                pending,
                new DatabaseSnapshot
                {
                    InstanceId = _task.InstanceId,
                    DbNames = _names,
                    ZipLevel = _settings.ZipLevel,
                    PasswordSha256 = passwordHash
                },
                utcNow,
                File.Exists);

            if (_decision.Reuse)
            {
                for (var i = 0; i < _names.Length; i++)
                {
                    if (FindItem(pending, _names[i]) == null)
                    {
                        _decision = new PendingDecision
                        {
                            Reuse = false,
                            ResetCycle = false,
                            Reason = "缺少数据库包",
                            Replace = new PendingItem[0],
                            Keep = _decision.Keep
                        };
                        break;
                    }
                }
            }

            if (_decision.Reuse)
            {
                _stats.PackedFiles = _decision.Keep == null ? 0 : _decision.Keep.Length;
            }

            return new BackupSourceInspection
            {
                Decision = _decision,
                Stats = _stats,
                TrackPending = _task.DoNetwork || _task.DoFtp || _task.DoCloud
            };
        }

        public BackupPackageSet Prepare(PendingRecord record, PendingDecision decision, DateTime utcNow, StringBuilder log)
        {
            if (decision.ResetCycle)
            {
                record = new PendingRecord
                {
                    InstanceId = _task.InstanceId,
                    DbNames = _names,
                    CycleUntilUtc = PendingBackup.CycleUntil(_task.CycleKind, _task.CycleInterval, utcNow),
                    Items = new PendingItem[0]
                };
            }

            record = record ?? new PendingRecord { Items = new PendingItem[0] };
            record.InstanceId = _task.InstanceId;
            record.DbNames = _names;
            var packages = new List<PendingItem>();
            string work = null;
            for (var i = 0; i < _names.Length; i++)
            {
                var dbName = _names[i];
                var existing = FindItem(record, dbName);
                if (existing != null && PendingFiles.PackageExists(existing, File.Exists))
                {
                    log.Append(" 沿用未传完的包 ").Append(PendingPath(existing));
                    packages.Add(existing);
                    _stats.PackedFiles++;
                    continue;
                }

                try
                {
                    if (work == null)
                    {
                        work = PrepareWorkDir(_backupRoot);
                    }

                    var sqlFile = Path.Combine(work, SafeFile(dbName) + ".sql");
                    if (File.Exists(sqlFile))
                    {
                        File.Delete(sqlFile);
                    }

                    try
                    {
                        MysqlConnect.DumpDatabase(_instance, dbName, sqlFile);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("未能写入备份目录 " + _backupRoot + "。" + ex.Message);
                    }

                    var flags = _settings.NameFlags == 0 ? NameParts.DefaultFlags : _settings.NameFlags;
                    var parts = NameParts.FromFlags(flags);
                    var folder = BackupNaming.MssqlFolder(_backupRoot, _settings.SoftwareId, _task.Name, _instance.Name, parts);
                    var zipPath = Path.Combine(folder, BackupNaming.MssqlZipName(_settings.SoftwareId, _task.Name, dbName, utcNow, parts));
                    try
                    {
                        ZipPacker.Pack(
                            new List<SourceFile> { new SourceFile { FullPath = sqlFile, RelativePath = Path.GetFileName(sqlFile) } },
                            zipPath,
                            _settings.ZipPassword,
                            _settings.ZipLevel);
                    }
                    catch
                    {
                        PendingFiles.TryDeleteZip(zipPath);
                        throw;
                    }

                    TryDelete(sqlFile);
                    var zipBytes = new FileInfo(zipPath).Length;
                    log.Append(" ").Append(dbName).Append("→").Append(Path.GetFileName(zipPath));
                    var packed = new PendingItem
                    {
                        Key = dbName,
                        ZipPath = zipPath,
                        Fingerprint = "",
                        ZipLevel = _settings.ZipLevel,
                        PasswordSha256 = SourceFingerprint.PasswordHash(_settings.ZipPassword),
                        ArchiveBytes = zipBytes
                    };
                    var split = PackageSplitter.Split(
                        zipPath,
                        _task.Name,
                        PackageLimits.Resolve(_settings.PartSizeBytes, _task.PartSizeBytes),
                        new BackupRunStats { DiscoveredFiles = 1, PackedFiles = 1, SourceBytes = zipBytes },
                        delegate { return PackageManifestCodec.NewBackupId(utcNow); });
                    if (split != null)
                    {
                        File.Delete(zipPath);
                        PendingFiles.SetSplitItem(packed, split, _stats);
                    }

                    _stats.PackedFiles++;
                    PendingBackup.Upsert(record, packed);
                    packages.Add(packed);
                }
                catch (Exception ex)
                {
                    _stats.SkippedFiles++;
                    log.Append(" ").Append(dbName).Append(" 导出失败：").Append(ex.Message).Append("。");
                }
            }

            record.RunStats = _stats;
            long totalArchiveBytes = 0;
            for (var i = 0; i < packages.Count; i++)
            {
                if (packages[i] != null && packages[i].ArchiveBytes > 0)
                {
                    totalArchiveBytes += packages[i].ArchiveBytes;
                }
            }

            return new BackupPackageSet
            {
                Record = record,
                Packages = packages.ToArray(),
                ArchiveBytes = totalArchiveBytes
            };
        }

        public void Dispose()
        {
        }

        private static PendingItem FindItem(PendingRecord record, string dbName)
        {
            if (record == null || record.Items == null)
            {
                return null;
            }

            for (var i = 0; i < record.Items.Length; i++)
            {
                if (string.Equals(record.Items[i].Key ?? "", dbName ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return record.Items[i];
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

        private static string PrepareWorkDir(string backupRoot)
        {
            var dir = Path.Combine(backupRoot, "_mysql_work");
            Directory.CreateDirectory(dir);
            return dir;
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

        private static string PendingPath(PendingItem item)
        {
            return item == null ? "" : Path.GetFileName(item.ZipPath ?? item.PackageDirectory ?? "");
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

    internal sealed class MysqlBackupDelivery : IBackupDelivery
    {
        private readonly SqliteDb _db;
        private readonly string _taskName;

        public MysqlBackupDelivery(SqliteDb db, string taskName)
        {
            _db = db;
            _taskName = taskName;
        }

        public bool Upload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, StringBuilder log, Action persist)
        {
            return PendingFiles.UploadItem(_db, item, doNetwork, doFtp, doCloud, provider, "mysql", _taskName, log, persist);
        }
    }

    internal sealed class MysqlBackupNotification : IBackupNotification
    {
        private readonly SqliteDb _db;
        private readonly bool _doEmail;
        private readonly string _taskName;

        public MysqlBackupNotification(SqliteDb db, bool doEmail, string taskName)
        {
            _db = db;
            _doEmail = doEmail;
            _taskName = taskName;
        }

        public bool ShouldSend(bool dataOk)
        {
            return MailUtil.ShouldSend(SettingsStore.GetXfer<SmtpSettingsDto>(_db, "smtp"), _doEmail, dataOk);
        }

        public void Send(bool dataOk, BackupRunStats stats, long archiveBytes, bool uploadEnabled, bool uploadOk, int packageCount, string detail)
        {
            MailUtil.SendResult(
                SettingsStore.GetXfer<SmtpSettingsDto>(_db, "smtp"),
                _taskName,
                dataOk,
                detail ?? "",
                SettingsStore.Get(_db).SoftwareId,
                BackupRunSummary.ToDatabaseMailStats(stats, archiveBytes, uploadEnabled, uploadOk, packageCount, uploadOk ? packageCount : 0));
        }
    }
}
