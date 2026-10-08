using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using YxyXb.Core.IO;

namespace YxyXb.Core.Backup
{
    public static class ZipPacker
    {
        public static void Pack(IList<SourceFile> files, string destZip, string password, int level)
        {
            if (files == null)
            {
                throw new ArgumentNullException("files");
            }

            var dir = Path.GetDirectoryName(destZip);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var part = destZip + ".yxypart";
            if (File.Exists(part))
            {
                File.Delete(part);
            }

            if (level < 0)
            {
                level = 0;
            }

            if (level > 9)
            {
                level = 9;
            }

            var usePassword = !string.IsNullOrEmpty(password);
            using (var stream = File.Create(LongPath.Qualify(part)))
            using (var zip = new ZipOutputStream(stream))
            {
                zip.SetLevel(level);
                if (usePassword)
                {
                    zip.Password = password;
                    zip.ZipCryptoEncoding = Encoding.Default;
                }

                var buffer = new byte[81920];
                for (var i = 0; i < files.Count; i++)
                {
                    var item = files[i];
                    var entryName = ZipEntry.CleanName(item.RelativePath.Replace('\\', '/'));
                    var entry = new ZipEntry(entryName);
                    entry.DateTime = File.GetLastWriteTime(item.FullPath);
                    if (usePassword)
                    {
                        long size;
                        long crc;
                        ReadCrc(item.FullPath, buffer, out size, out crc);
                        entry.Size = size;
                        entry.Crc = crc;
                    }

                    zip.PutNextEntry(entry);
                    using (var input = File.OpenRead(item.FullPath))
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            zip.Write(buffer, 0, read);
                        }
                    }

                    zip.CloseEntry();
                }

                zip.Finish();
            }

            if (File.Exists(destZip))
            {
                File.Delete(destZip);
            }

            File.Move(part, destZip);
        }

        /// <summary>解压 zip 中第一个匹配扩展名的文件到目录，返回解压后的完整路径。</summary>
        public static string ExtractFirst(string zipPath, string password, string destDir, params string[] extensions)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException("找不到备份包。", zipPath);
            }

            if (string.IsNullOrEmpty(destDir))
            {
                throw new ArgumentException("destDir");
            }

            Directory.CreateDirectory(destDir);
            var exts = NormalizeExts(extensions);
            using (var stream = File.OpenRead(LongPath.Qualify(zipPath)))
            using (var zip = new ZipInputStream(stream))
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zip.Password = password;
                }

                ZipEntry entry;
                while ((entry = zip.GetNextEntry()) != null)
                {
                    if (!entry.IsFile) continue;
                    var name = Path.GetFileName(entry.Name.Replace('/', '\\'));
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!MatchExt(name, exts)) continue;

                    var dest = Path.Combine(destDir, SafeFileName(name));
                    if (File.Exists(dest)) File.Delete(dest);
                    using (var output = File.Create(LongPath.Qualify(dest)))
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                        }
                    }

                    if (new FileInfo(dest).Length == 0)
                    {
                        throw new InvalidOperationException("解压结果为空，请检查压缩密码是否正确。");
                    }

                    return dest;
                }
            }

            throw new InvalidOperationException("压缩包内没有找到可用的库备份文件（.bak / .sql）。");
        }

        private static string[] NormalizeExts(string[] extensions)
        {
            if (extensions == null || extensions.Length == 0)
            {
                return new[] { ".bak", ".sql" };
            }

            var list = new List<string>();
            for (var i = 0; i < extensions.Length; i++)
            {
                var e = (extensions[i] ?? "").Trim();
                if (e.Length == 0) continue;
                if (e[0] != '.') e = "." + e;
                list.Add(e.ToLowerInvariant());
            }

            return list.Count == 0 ? new[] { ".bak", ".sql" } : list.ToArray();
        }

        private static bool MatchExt(string name, string[] exts)
        {
            var ext = Path.GetExtension(name) ?? "";
            for (var i = 0; i < exts.Length; i++)
            {
                if (string.Equals(ext, exts[i], StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static string SafeFileName(string name)
        {
            var chars = Path.GetInvalidFileNameChars();
            var text = name ?? "file";
            for (var i = 0; i < chars.Length; i++)
            {
                text = text.Replace(chars[i], '_');
            }

            return text;
        }

        private static void ReadCrc(string path, byte[] buffer, out long size, out long crc)
        {
            var checksum = new Crc32();
            size = 0;
            using (var input = File.OpenRead(LongPath.Qualify(path)))
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    checksum.Update(new ArraySegment<byte>(buffer, 0, read));
                    size += read;
                }
            }

            crc = checksum.Value;
        }
    }
}
