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

namespace YxyXb.Mssql
{
    internal sealed class MssqlBackupTaskState : IBackupRunTask
    {
        private readonly SqliteDb _db;
        private readonly MssqlTaskDto _task;

        public MssqlBackupTaskState(SqliteDb db, MssqlTaskDto task)
        {
            _db = db;
            _task = task;
        }

        public string LogType
        {
            get { return "mssql"; }
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
            MssqlTaskStore.SavePending(_db, _task.Id, json);
            _task.PendingJson = json;
        }

        public void MarkResult(bool ok, DateTime utcNow)
        {
            MssqlTaskStore.MarkResult(_db, _task, ok, utcNow);
        }

        public void WriteLog(string mode, string result, string message)
        {
            LogStore.Add(_db, LogType, mode, result, _task.Name, message);
        }
    }

    internal sealed class MssqlBackupSource : IBackupSourceAdapter
    {
        private readonly SqliteDb _db;
        private readonly MssqlTaskDto _task;
        private MssqlInstanceDto _instance;
        private AppSettingsDto _settings;
        private string _backupRoot;
        private string[] _names;
        private BackupRunStats _stats;
        private PendingDecision _decision;

        public MssqlBackupSource(SqliteDb db, MssqlTaskDto task)
        {
            _db = db;
            _task = task;
        }

        public BackupSourceInspection Inspect(PendingRecord pending, DateTime utcNow, StringBuilder log)
        {
            _instance = MssqlInstanceStore.GetById(_db, _task.InstanceId);
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

                    var bakFile = Path.Combine(work, SafeFile(dbName) + ".bak");
                    if (File.Exists(bakFile))
                    {
                        File.Delete(bakFile);
                    }

                    BackupToWorkFile(_instance, dbName, bakFile, _backupRoot);
                    var flags = _settings.NameFlags == 0 ? NameParts.DefaultFlags : _settings.NameFlags;
                    var parts = NameParts.FromFlags(flags);
                    var folder = BackupNaming.MssqlFolder(_backupRoot, _settings.SoftwareId, _task.Name, _instance.Name, parts);
                    var zipPath = Path.Combine(folder, BackupNaming.MssqlZipName(_settings.SoftwareId, _task.Name, dbName, utcNow, parts));
                    try
                    {
                        ZipPacker.Pack(
                            new List<SourceFile> { new SourceFile { FullPath = bakFile, RelativePath = Path.GetFileName(bakFile) } },
                            zipPath,
                            _settings.ZipPassword,
                            _settings.ZipLevel);
                    }
                    catch
                    {
                        PendingFiles.TryDeleteZip(zipPath);
                        throw;
                    }

                    TryDelete(bakFile);
                    var zipBytes = new FileInfo(zipPath).Length;
                    log.Append(" ").Append(dbName).Append("→").Append(Path.GetFileName(zipPath));
                    log.Append(string.IsNullOrEmpty(_settings.ZipPassword) ? "。" : "，已用压缩密码。");
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

        private static IList<string> ResolveDatabases(MssqlInstanceDto instance, MssqlTaskDto task)
        {
            return DatabaseScope.Pick(
                MssqlConnect.ListUserDatabases(instance),
                task.DbScope,
                task.SelectedDbs,
                MssqlConnect.IsSystemDb);
        }

        private static void BackupToWorkFile(MssqlInstanceDto instance, string dbName, string bakFile, string bakRoot)
        {
            Exception first;
            try
            {
                MssqlConnect.BackupDatabase(instance, dbName, bakFile);
                return;
            }
            catch (Exception ex)
            {
                first = ex;
            }

            string staging = null;
            try
            {
                var sqlDir = MssqlConnect.ReadDefaultBackupDirectory(instance);
                if (string.IsNullOrWhiteSpace(sqlDir))
                {
                    throw new InvalidOperationException("SQL Server 没有可用的默认备份目录。");
                }

                Directory.CreateDirectory(sqlDir);
                staging = Path.Combine(sqlDir, Path.GetFileNameWithoutExtension(bakFile) + "-" + Guid.NewGuid().ToString("N") + ".bak");
                MssqlConnect.BackupDatabase(instance, dbName, staging);
                if (!File.Exists(staging) || new FileInfo(staging).Length == 0)
                {
                    throw new InvalidOperationException("SQL Server 未生成备份文件。");
                }

                File.Copy(staging, bakFile, true);
            }
            catch (Exception second)
            {
                throw new InvalidOperationException(
                    "未能写入备份目录 " + bakRoot + "。SQL Server 服务需要能在该目录创建文件。请给 SQL Server 服务帐号这个文件夹的修改权限后重试。"
                    + first.Message + " " + second.Message);
            }
            finally
            {
                TryDelete(staging);
            }
        }

        private static string PrepareWorkDir(string backupRoot)
        {
            var dirs = new[]
            {
                Path.Combine(backupRoot, "_mssql_work"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "用心云循备", "mssql-work")
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

    internal sealed class MssqlBackupDelivery : IBackupDelivery
    {
        private readonly SqliteDb _db;
        private readonly string _taskName;

        public MssqlBackupDelivery(SqliteDb db, string taskName)
        {
            _db = db;
            _taskName = taskName;
        }

        public bool Upload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, StringBuilder log, Action persist)
        {
            return PendingFiles.UploadItem(_db, item, doNetwork, doFtp, doCloud, provider, "mssql", _taskName, log, persist);
        }
    }

    internal sealed class MssqlBackupNotification : IBackupNotification
    {
        private readonly SqliteDb _db;
        private readonly bool _doEmail;
        private readonly string _taskName;

        public MssqlBackupNotification(SqliteDb db, bool doEmail, string taskName)
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
