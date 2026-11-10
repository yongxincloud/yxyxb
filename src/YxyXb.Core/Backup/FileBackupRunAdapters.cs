using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;
using YxyXb.Core.Transfer;

namespace YxyXb.Core.Backup
{
    internal sealed class FileBackupTaskState : IBackupRunTask
    {
        private readonly SqliteDb _db;
        private readonly FileTaskDto _task;

        public FileBackupTaskState(SqliteDb db, FileTaskDto task)
        {
            _db = db;
            _task = task;
        }

        public string LogType
        {
            get { return "file"; }
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
            FileTaskStore.SavePending(_db, _task.Id, json);
            _task.PendingJson = json;
        }

        public void MarkResult(bool ok, DateTime utcNow)
        {
            FileTaskStore.MarkResult(_db, _task, ok, utcNow);
        }

        public void WriteLog(string mode, string result, string message)
        {
            LogStore.Add(_db, LogType, mode, result, _task.Name, message);
        }
    }

    internal sealed class FileBackupSource : IBackupSourceAdapter
    {
        private readonly SqliteDb _db;
        private readonly FileTaskDto _task;
        private string _tempRoot;
        private string _backupRoot;
        private AppSettingsDto _settings;
        private IList<SourceFile> _files;
        private BackupRunStats _stats;
        private string _fingerprint;
        private string _passwordHash;

        public FileBackupSource(SqliteDb db, FileTaskDto task)
        {
            _db = db;
            _task = task;
        }

        public BackupSourceInspection Inspect(PendingRecord pending, DateTime utcNow, StringBuilder log)
        {
            _settings = SettingsStore.Get(_db);
            _backupRoot = AppPaths.BackupDirectoryOrThrow(_settings.BakPath);
            Directory.CreateDirectory(_backupRoot);

            var error = FileTaskValidator.Validate(_task, _backupRoot);
            if (error != null && _task.SourceKind == "local")
            {
                throw new InvalidOperationException(error);
            }

            var sourceRoot = PrepareSource(_db, _task, log, out _tempRoot);
            if (BackupNaming.IsLoop(sourceRoot, _backupRoot))
            {
                throw new InvalidOperationException("源路径不能回环到备份目录。");
            }

            var scan = SourceWalker.ScanLocal(sourceRoot);
            _files = scan.Files;
            _stats = scan.Stats;
            log.Append("发现 ").Append(_stats.DiscoveredFiles)
                .Append("，打包 ").Append(_stats.PackedFiles)
                .Append("，跳过 ").Append(_stats.SkippedFiles).Append("。");
            for (var i = 0; i < scan.DirectoryErrors.Count; i++)
            {
                log.Append(" 目录无法读取：").Append(scan.DirectoryErrors[i]).Append("。");
            }

            var fingerprintFiles = new List<FingerprintFile>();
            for (var i = 0; i < _files.Count; i++)
            {
                var item = _files[i];
                var info = new FileInfo(item.FullPath);
                fingerprintFiles.Add(new FingerprintFile
                {
                    RelativePath = item.RelativePath,
                    Length = info.Length,
                    LastWriteUtcTicks = File.GetLastWriteTimeUtc(item.FullPath).Ticks
                });
            }

            _passwordHash = SourceFingerprint.PasswordHash(_settings.ZipPassword);
            _fingerprint = SourceFingerprint.Compute(fingerprintFiles);
            var decision = PendingBackup.DecideFile(pending, new FileSnapshot
            {
                SourceKind = _task.SourceKind ?? "local",
                SourcePath = _task.SourcePath ?? "",
                Fingerprint = _fingerprint,
                ZipLevel = _settings.ZipLevel,
                PasswordSha256 = _passwordHash
            }, File.Exists);

            if (decision.Reuse)
            {
                TryDeleteDir(_tempRoot);
                _tempRoot = null;
            }

            return new BackupSourceInspection
            {
                Decision = decision,
                Stats = _stats,
                TrackPending = _task.DoNetwork || _task.DoFtp || _task.DoCloud
            };
        }

        public BackupPackageSet Prepare(PendingRecord record, PendingDecision decision, DateTime utcNow, StringBuilder log)
        {
            var flags = _settings.NameFlags == 0 ? NameParts.DefaultFlags : _settings.NameFlags;
            var parts = NameParts.FromFlags(flags);
            var folder = BackupNaming.FileFolder(_backupRoot, _settings.SoftwareId, _task.Name, parts);
            var zipPath = Path.Combine(folder, BackupNaming.FileZipName(_settings.SoftwareId, _task.Name, utcNow, parts));
            try
            {
                ZipPacker.Pack(_files, zipPath, _settings.ZipPassword, _settings.ZipLevel);
            }
            catch
            {
                PendingFiles.TryDeleteZip(zipPath);
                throw;
            }

            var archiveBytes = new FileInfo(zipPath).Length;
            log.Append(" 产出 ").Append(zipPath);
            log.Append(string.IsNullOrEmpty(_settings.ZipPassword) ? "。" : "，已用压缩密码。");
            if (decision.Replace != null && decision.Replace.Length > 0)
            {
                log.Append(" 重新打包：").Append(decision.Reason);
            }

            var packed = new PendingItem
            {
                Key = "file",
                ZipPath = zipPath,
                Fingerprint = _fingerprint,
                ZipLevel = _settings.ZipLevel,
                PasswordSha256 = _passwordHash,
                ArchiveBytes = archiveBytes
            };
            var limit = PackageLimits.Resolve(_settings.PartSizeBytes, _task.PartSizeBytes);
            var split = PackageSplitter.Split(
                zipPath,
                _task.Name,
                limit,
                _stats,
                delegate { return PackageManifestCodec.NewBackupId(utcNow); });
            if (split != null)
            {
                File.Delete(zipPath);
                PendingFiles.SetSplitItem(packed, split, _stats);
                log.Append(" 已生成分卷 ").Append(split.PartPaths.Length).Append(" 个。");
            }

            record = record ?? new PendingRecord();
            record.SourceKind = _task.SourceKind ?? "local";
            record.SourcePath = _task.SourcePath ?? "";
            record.CycleUntilUtc = null;
            record.RunStats = _stats;
            record.Items = new[] { packed };
            return new BackupPackageSet
            {
                Record = record,
                Packages = new[] { packed },
                ArchiveBytes = archiveBytes
            };
        }

        public void Dispose()
        {
            TryDeleteDir(_tempRoot);
            _tempRoot = null;
        }

        private static string PrepareSource(SqliteDb db, FileTaskDto task, StringBuilder log, out string tempRoot)
        {
            tempRoot = null;
            var kind = task.SourceKind ?? "local";
            if (kind == "unc")
            {
                var unc = SettingsStore.GetXfer<UncSettingsDto>(db, "unc");
                unc.Path = task.SourcePath;
                UncShare.Test(unc);
                return task.SourcePath;
            }

            if (kind == "ftp")
            {
                var ftp = SettingsStore.GetXfer<FtpSettingsDto>(db, "ftp");
                ftp.RemoteDir = task.SourcePath;
                tempRoot = Path.Combine(Path.GetTempPath(), "yxy-ftp-" + Guid.NewGuid().ToString("N"));
                var speed = SettingsStore.Get(db);
                var down = CloudSpeed.DownloadKbps(speed.UploadMinKbps, speed.UploadMaxKbps);
                FtpUtil.DownloadTree(ftp, task.SourcePath, tempRoot, down);
                log.Append("已下载 FTP 源。");
                return tempRoot;
            }

            return task.SourcePath;
        }

        private static void TryDeleteDir(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
        }
    }

    internal sealed class FileBackupDelivery : IBackupDelivery
    {
        private readonly SqliteDb _db;
        private readonly string _taskName;

        public FileBackupDelivery(SqliteDb db, string taskName)
        {
            _db = db;
            _taskName = taskName;
        }

        public bool Upload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, StringBuilder log, Action persist)
        {
            return PendingFiles.UploadItem(
                _db,
                item,
                doNetwork,
                doFtp,
                doCloud,
                provider,
                "file",
                _taskName,
                log,
                persist);
        }
    }

    internal sealed class FileBackupNotification : IBackupNotification
    {
        private readonly SqliteDb _db;
        private readonly bool _doEmail;
        private readonly string _taskName;

        public FileBackupNotification(SqliteDb db, bool doEmail, string taskName)
        {
            _db = db;
            _doEmail = doEmail;
            _taskName = taskName;
        }

        public bool ShouldSend(bool dataOk)
        {
            return MailUtil.ShouldSend(SettingsStore.GetXfer<SmtpSettingsDto>(_db, "smtp"), _doEmail, dataOk);
        }

        public void Send(
            bool dataOk,
            BackupRunStats stats,
            long archiveBytes,
            bool uploadEnabled,
            bool uploadOk,
            int packageCount,
            string detail)
        {
            MailUtil.SendResult(
                SettingsStore.GetXfer<SmtpSettingsDto>(_db, "smtp"),
                _taskName,
                dataOk,
                detail ?? "",
                SettingsStore.Get(_db).SoftwareId,
                BackupRunSummary.ToMailStats(
                    stats,
                    archiveBytes,
                    uploadEnabled,
                    uploadOk,
                    packageCount,
                    uploadOk ? packageCount : 0));
        }
    }

    public sealed class BackupPackageStore : IBackupPackageStore
    {
        public bool DeleteReplacements(PendingItem[] items, StringBuilder log)
        {
            return PendingFiles.DeleteReplacements(items, log);
        }

        public bool DeletePackage(PendingItem item)
        {
            return PendingFiles.TryDeletePackage(item);
        }
    }
}
