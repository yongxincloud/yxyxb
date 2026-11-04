using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using YxyXb.Core.IO;

namespace YxyXb.Core.Backup
{
    public sealed class SplitPackageResult
    {
        public string BackupId { get; set; }

        public string PackageDirectory { get; set; }

        public string ManifestPath { get; set; }

        public string[] PartPaths { get; set; }

        public long ArchiveBytes { get; set; }
    }

    public static class PackageSplitter
    {
        private sealed class Range
        {
            public int Index;
            public long Offset;
            public long Length;
        }

        public static SplitPackageResult Split(
            string fullZipPath,
            string taskName,
            long limitBytes,
            BackupRunStats stats,
            Func<string> backupIdFactory)
        {
            if (string.IsNullOrWhiteSpace(fullZipPath) || !File.Exists(fullZipPath))
            {
                throw new FileNotFoundException("找不到完整备份包。", fullZipPath);
            }

            var archiveBytes = new FileInfo(fullZipPath).Length;
            if (archiveBytes <= limitBytes)
            {
                return null;
            }

            var payloadLimit = limitBytes - PackageLimits.ManifestReserveBytes;
            if (payloadLimit <= 0)
            {
                throw new InvalidOperationException("分卷大小过小。");
            }

            var backupId = backupIdFactory == null
                ? PackageManifestCodec.NewBackupId(DateTime.UtcNow)
                : backupIdFactory();
            var packageName = Path.GetFileName(fullZipPath);
            var baseName = Path.GetFileNameWithoutExtension(packageName);
            var directory = Path.Combine(
                Path.GetDirectoryName(fullZipPath) ?? "",
                baseName + ".split-" + Safe(backupId));
            Directory.CreateDirectory(directory);

            var ranges = PlanRanges(archiveBytes, payloadLimit);
            var manifest = new BackupPackageManifest
            {
                FormatVersion = 1,
                BackupId = backupId,
                PackageName = packageName,
                TaskName = taskName ?? "",
                CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                PartSizeLimitBytes = limitBytes,
                ArchiveBytes = archiveBytes,
                ArchiveSha256 = PackageManifestCodec.FileSha256(fullZipPath),
                SourceFileCount = stats == null ? 0 : stats.DiscoveredFiles,
                PackedFileCount = stats == null ? 0 : stats.PackedFiles,
                SkippedFileCount = stats == null ? 0 : stats.SkippedFiles,
                SourceBytes = stats == null ? 0 : stats.SourceBytes,
                Parts = new PackagePartManifest[ranges.Count]
            };

            for (var i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                var fileName = baseName + ".part" + (i + 1).ToString("00") + ".zip";
                manifest.Parts[i] = new PackagePartManifest
                {
                    Index = i + 1,
                    FileName = fileName,
                    PayloadEntryName = "payload/" + baseName + ".part" + (i + 1).ToString("00") + ".bin",
                    PayloadBytes = range.Length,
                    PayloadSha256 = HashRange(fullZipPath, range.Offset, range.Length),
                    WrappedBytes = 0,
                    WrappedSha256 = ""
                };
            }

            var embeddedJson = PackageManifestCodec.ToJson(manifest);
            var partPaths = new string[ranges.Count];
            try
            {
                for (var i = 0; i < ranges.Count; i++)
                {
                    var part = manifest.Parts[i];
                    var path = Path.Combine(directory, part.FileName);
                    WritePart(fullZipPath, ranges[i], path, part.PayloadEntryName, embeddedJson);
                    part.WrappedBytes = new FileInfo(path).Length;
                    part.WrappedSha256 = PackageManifestCodec.FileSha256(path);
                    partPaths[i] = path;
                }

                var manifestPath = Path.Combine(directory, baseName + ".manifest.json");
                File.WriteAllText(LongPath.Qualify(manifestPath), PackageManifestCodec.ToJson(manifest), Encoding.UTF8);
                VerifyUnit(manifest, directory);
                return new SplitPackageResult
                {
                    BackupId = backupId,
                    PackageDirectory = directory,
                    ManifestPath = manifestPath,
                    PartPaths = partPaths,
                    ArchiveBytes = archiveBytes
                };
            }
            catch
            {
                TryDeleteDirectory(directory);
                throw;
            }
        }

        public static string Assemble(string inputDirectory, string workDirectory)
        {
            var manifest = ScanUnit(inputDirectory);
            VerifyUnit(manifest, inputDirectory);
            Directory.CreateDirectory(workDirectory);
            var assembled = Path.Combine(workDirectory, "assembled-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (var output = File.Create(LongPath.Qualify(assembled)))
                {
                    for (var i = 0; i < manifest.Parts.Length; i++)
                    {
                        var part = manifest.Parts[i];
                        using (var zip = new ZipFile(LongPath.Qualify(Path.Combine(inputDirectory, part.FileName))))
                        {
                            var entry = zip.GetEntry(part.PayloadEntryName);
                            if (entry == null)
                            {
                                throw new InvalidOperationException("分卷缺少 payload：" + part.FileName);
                            }

                            using (var input = zip.GetInputStream(entry))
                            {
                                input.CopyTo(output);
                            }
                        }
                    }
                }

                if (new FileInfo(assembled).Length != manifest.ArchiveBytes)
                {
                    throw new InvalidOperationException("组装后的 ZIP 长度不一致。");
                }

                if (!string.Equals(PackageManifestCodec.FileSha256(assembled), manifest.ArchiveSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("组装后的 ZIP 哈希不一致。");
                }

                return assembled;
            }
            catch
            {
                TryDeleteFile(assembled);
                throw;
            }
        }

        public static BackupPackageManifest ScanUnit(string inputDirectory)
        {
            if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
            {
                throw new DirectoryNotFoundException("分卷目录不存在：" + inputDirectory);
            }

            BackupPackageManifest selected = null;
            var manifests = Directory.GetFiles(inputDirectory, "*.manifest.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < manifests.Length; i++)
            {
                var current = PackageManifestCodec.Parse(File.ReadAllText(manifests[i]));
                selected = PickSingle(selected, current);
            }

            if (selected == null)
            {
                var parts = Directory.GetFiles(inputDirectory, "*.part*.zip", SearchOption.TopDirectoryOnly);
                for (var i = 0; i < parts.Length; i++)
                {
                    var current = ReadEmbedded(parts[i]);
                    if (current != null)
                    {
                        selected = PickSingle(selected, current);
                    }
                }
            }

            if (selected == null)
            {
                throw new InvalidOperationException("目录中没有可识别的分卷清单。");
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < selected.Parts.Length; i++)
            {
                ids.Add(selected.Parts[i].FileName ?? "");
            }
            if (ids.Count != selected.Parts.Length)
            {
                throw new InvalidOperationException("分卷清单包含重复分卷。");
            }

            for (var i = 0; i < selected.Parts.Length; i++)
            {
                var expect = i + 1;
                if (selected.Parts[i].Index != expect)
                {
                    throw new InvalidOperationException("分卷序号不连续。");
                }

                if (!File.Exists(Path.Combine(inputDirectory, selected.Parts[i].FileName)))
                {
                    throw new InvalidOperationException("缺少分卷：" + selected.Parts[i].FileName);
                }
            }

            return selected;
        }

        private static BackupPackageManifest PickSingle(BackupPackageManifest current, BackupPackageManifest next)
        {
            if (current == null)
            {
                return next;
            }

            if (!string.Equals(current.BackupId ?? "", next.BackupId ?? "", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("目录中包含多组分卷备份。");
            }

            return current;
        }

        private static BackupPackageManifest ReadEmbedded(string partPath)
        {
            try
            {
                using (var zip = new ZipFile(LongPath.Qualify(partPath)))
                {
                    var entry = zip.GetEntry("manifest.json");
                    if (entry == null)
                    {
                        return null;
                    }

                    using (var input = zip.GetInputStream(entry))
                    using (var reader = new StreamReader(input, Encoding.UTF8))
                    {
                        return PackageManifestCodec.Parse(reader.ReadToEnd());
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void VerifyUnit(BackupPackageManifest manifest, string directory)
        {
            long payloadBytes = 0;
            for (var i = 0; i < manifest.Parts.Length; i++)
            {
                var part = manifest.Parts[i];
                var path = Path.Combine(directory, part.FileName);
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException("缺少分卷：" + part.FileName);
                }

                if (part.WrappedBytes > 0 && new FileInfo(path).Length != part.WrappedBytes)
                {
                    throw new InvalidOperationException("分卷长度不一致：" + part.FileName);
                }

                if (!string.IsNullOrEmpty(part.WrappedSha256)
                    && !string.Equals(PackageManifestCodec.FileSha256(path), part.WrappedSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("分卷哈希不一致：" + part.FileName);
                }

                payloadBytes += part.PayloadBytes;
            }

            if (payloadBytes != manifest.ArchiveBytes)
            {
                throw new InvalidOperationException("分卷 payload 总长度与完整 ZIP 不一致。");
            }
        }

        private static List<Range> PlanRanges(long total, long payloadLimit)
        {
            var ranges = new List<Range>();
            var offset = 0L;
            while (offset < total)
            {
                var length = Math.Min(payloadLimit, total - offset);
                ranges.Add(new Range
                {
                    Index = ranges.Count + 1,
                    Offset = offset,
                    Length = length
                });
                offset += length;
            }

            return ranges;
        }

        private static void WritePart(string fullZipPath, Range range, string destination, string entryName, string manifestJson)
        {
            var temp = destination + ".yxypart";
            TryDeleteFile(temp);
            using (var source = File.OpenRead(LongPath.Qualify(fullZipPath)))
            using (var output = File.Create(LongPath.Qualify(temp)))
            using (var zip = new ZipOutputStream(output))
            {
                zip.SetLevel(0);
                var payloadEntry = new ZipEntry(entryName)
                {
                    Size = range.Length,
                    CompressionMethod = CompressionMethod.Stored,
                    IsUnicodeText = true
                };
                zip.PutNextEntry(payloadEntry);
                source.Seek(range.Offset, SeekOrigin.Begin);
                CopyExact(source, zip, range.Length);
                zip.CloseEntry();

                var manifestBytes = Encoding.UTF8.GetBytes(manifestJson ?? "");
                var manifestEntry = new ZipEntry("manifest.json")
                {
                    Size = manifestBytes.Length,
                    CompressionMethod = CompressionMethod.Stored,
                    IsUnicodeText = true
                };
                zip.PutNextEntry(manifestEntry);
                zip.Write(manifestBytes, 0, manifestBytes.Length);
                zip.CloseEntry();
                zip.Finish();
            }

            TryDeleteFile(destination);
            File.Move(temp, destination);
        }

        private static void CopyExact(Stream input, Stream output, long count)
        {
            var buffer = new byte[81920];
            var remain = count;
            while (remain > 0)
            {
                var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remain));
                if (read <= 0)
                {
                    throw new InvalidOperationException("完整 ZIP 读取不完整。");
                }

                output.Write(buffer, 0, read);
                remain -= read;
            }
        }

        private static string HashRange(string path, long offset, long length)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var input = File.OpenRead(LongPath.Qualify(path)))
            {
                input.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[81920];
                var remain = length;
                while (remain > 0)
                {
                    var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remain));
                    if (read <= 0)
                    {
                        throw new InvalidOperationException("完整 ZIP 读取不完整。");
                    }

                    sha.TransformBlock(buffer, 0, read, null, 0);
                    remain -= read;
                }

                sha.TransformFinalBlock(new byte[0], 0, 0);
                return PackageManifestCodec.ToHex(sha.Hash);
            }
        }

        private static string Safe(string value)
        {
            var chars = Path.GetInvalidFileNameChars();
            var text = value ?? "backup";
            for (var i = 0; i < chars.Length; i++)
            {
                text = text.Replace(chars[i], '_');
            }

            return text;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
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

