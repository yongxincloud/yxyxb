using System;
using System.Globalization;
using System.IO;
using YxyXb.Core.IO;

namespace YxyXb.Core.Backup
{
    public sealed class FileRestoreResult
    {
        public string OutputDirectory { get; set; }

        public int FileCount { get; set; }

        public long Bytes { get; set; }
    }

    public static class FileRestoreService
    {
        public static FileRestoreResult Restore(string inputDirectory, string outputParent, string password)
        {
            if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
            {
                throw new DirectoryNotFoundException("分卷目录不存在：" + inputDirectory);
            }

            if (string.IsNullOrWhiteSpace(outputParent) || !Directory.Exists(outputParent))
            {
                throw new DirectoryNotFoundException("恢复目录不存在：" + outputParent);
            }

            var work = Path.Combine(Path.GetTempPath(), "yxy-assemble-" + Guid.NewGuid().ToString("N"));
            var output = CreateUniqueTimestampDirectory(outputParent);
            try
            {
                var assembled = PackageSplitter.Assemble(inputDirectory, work);
                ZipPacker.ExtractAllSafe(assembled, password, output);
                var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
                long bytes = 0;
                for (var i = 0; i < files.Length; i++)
                {
                    bytes += new FileInfo(files[i]).Length;
                }

                return new FileRestoreResult
                {
                    OutputDirectory = output,
                    FileCount = files.Length,
                    Bytes = bytes
                };
            }
            catch
            {
                TryDeleteDirectory(output);
                throw;
            }
            finally
            {
                TryDeleteDirectory(work);
            }
        }

        public static string CreateUniqueTimestampDirectory(string parent)
        {
            var baseName = "restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(parent, baseName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return path;
            }

            for (var i = 1; i < 1000; i++)
            {
                path = Path.Combine(parent, baseName + "-" + i.ToString("00"));
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    return path;
                }
            }

            throw new InvalidOperationException("无法创建恢复目录。");
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
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
