using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Security;
using YxyXb.Media.Sync;

namespace YxyXb.Media
{
    internal sealed class MediaPackageRunResult
    {
        public string ZipPath { get; set; }
        public int Copied { get; set; }
        public int Skipped { get; set; }
        public long Bytes { get; set; }
        public bool TamperPending { get; set; }
        public bool LocalRemoved { get; set; }
        public int DeletedVersions { get; set; }
        public string ManifestPath { get; set; }
    }

    internal interface IMediaPackageRuntime
    {
        string Pack(MediaTaskDto task, string source, string bakDir, DateTime utc);
        bool FileExists(string path);
        void CreateDirectory(string path);
        FileStamp Stamp(string path, string relativePath, bool hash);
        void CopyPart(string source, string destination, string conflictMode);
        bool RemoveLocalAfterCopy(string zipPath, bool landed);
        void WriteAllText(string path, string text);
        bool LooksTampered(string filesRoot, string versionsRoot, bool useHash);
        int PurgeVersions(string filesRoot, string versionsRoot, int keepVersions, int keepDays);
    }

    internal sealed class FileSystemMediaPackageRuntime : IMediaPackageRuntime
    {
        private readonly AppSettingsDto _settings;

        public FileSystemMediaPackageRuntime(AppSettingsDto settings)
        {
            _settings = settings;
        }

        public string Pack(MediaTaskDto task, string source, string bakDir, DateTime utc)
        {
            var flags = _settings.NameFlags == 0 ? NameParts.DefaultFlags : _settings.NameFlags;
            return MediaPack.PackToBak(
                source,
                bakDir,
                _settings.SoftwareId,
                task.Name,
                _settings.ZipPassword,
                _settings.ZipLevel,
                utc,
                NameParts.FromFlags(flags));
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public FileStamp Stamp(string path, string relativePath, bool hash)
        {
            return PackageSync.Stamp(path, relativePath, hash);
        }

        public void CopyPart(string source, string destination, string conflictMode)
        {
            PackageSync.CopyPart(source, destination, conflictMode);
        }

        public bool RemoveLocalAfterCopy(string zipPath, bool landed)
        {
            return MediaPack.RemoveLocalAfterCopy(zipPath, landed);
        }

        public void WriteAllText(string path, string text)
        {
            File.WriteAllText(path, text);
        }

        public bool LooksTampered(string filesRoot, string versionsRoot, bool useHash)
        {
            return Inspection.LooksTampered(filesRoot, versionsRoot, useHash);
        }

        public int PurgeVersions(string filesRoot, string versionsRoot, int keepVersions, int keepDays)
        {
            return VersionPurge.Apply(filesRoot, versionsRoot, keepVersions, keepDays, true);
        }
    }

    internal sealed class MediaPackageLifecycle
    {
        private readonly IMediaPackageRuntime _runtime;

        public MediaPackageLifecycle(IMediaPackageRuntime runtime)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            _runtime = runtime;
        }

        public MediaPackageRunResult Run(
            MediaTaskDto task,
            string source,
            string bakDir,
            string filesRoot,
            string versionsRoot,
            DateTime nowUtc,
            bool alreadyTampered)
        {
            _runtime.CreateDirectory(filesRoot);
            _runtime.CreateDirectory(versionsRoot);
            var tampered = alreadyTampered || _runtime.LooksTampered(filesRoot, versionsRoot, task.UseSha256);

            var zipPath = _runtime.Pack(task, source, bakDir, nowUtc);
            var rel = PackageSync.RelFromBak(bakDir, zipPath);
            var destination = Path.Combine(filesRoot, rel);
            var sourceStamp = _runtime.Stamp(zipPath, rel, task.UseSha256);
            FileStamp destinationStamp = null;
            if (_runtime.FileExists(destination))
            {
                destinationStamp = _runtime.Stamp(destination, rel, task.UseSha256);
            }

            var result = new MediaPackageRunResult
            {
                ZipPath = zipPath,
                TamperPending = tampered
            };
            var stamps = new List<FileStamp>();
            var landed = false;
            if (destinationStamp != null && PackageSync.Same(sourceStamp, destinationStamp, task.UseSha256))
            {
                result.Skipped++;
                landed = true;
                stamps.Add(destinationStamp);
            }
            else
            {
                _runtime.CopyPart(zipPath, destination, task.ConflictMode);
                var after = _runtime.Stamp(destination, rel, task.UseSha256);
                if (after.Size != sourceStamp.Size)
                {
                    throw new InvalidOperationException("离线盘上的压缩包大小与本地不一致，本地压缩包保留。");
                }

                landed = true;
                stamps.Add(after);
                result.Copied++;
                result.Bytes += after.Size;
            }

            if (!landed)
            {
                throw new InvalidOperationException("离线盘未确认收到压缩包，本地压缩包保留。");
            }

            result.LocalRemoved = _runtime.RemoveLocalAfterCopy(zipPath, landed);
            var stampName = nowUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            result.ManifestPath = Path.Combine(versionsRoot, stampName + ".json");
            _runtime.WriteAllText(result.ManifestPath, JsonUtil.ToJson(new VersionManifest
            {
                Time = stampName,
                Files = stamps,
                Copied = result.Copied,
                Skipped = result.Skipped,
                Bytes = result.Bytes
            }));

            if (!result.TamperPending)
            {
                result.DeletedVersions = _runtime.PurgeVersions(
                    filesRoot,
                    versionsRoot,
                    task.RetainVersions,
                    task.RetainDays);
            }

            return result;
        }
    }
}
