using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using Yxy.Core.IO;

namespace Yxy.Core.Backup
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
