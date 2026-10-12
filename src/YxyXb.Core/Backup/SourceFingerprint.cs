using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace YxyXb.Core.Backup
{
    public sealed class FingerprintFile
    {
        public string RelativePath { get; set; }

        public long Length { get; set; }

        public long LastWriteUtcTicks { get; set; }
    }

    public static class SourceFingerprint
    {
        public static string Compute(IList<FingerprintFile> files)
        {
            var rows = new List<string>();
            if (files != null)
            {
                for (var i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var rel = file == null || file.RelativePath == null ? "" : file.RelativePath;
                    var length = file == null ? 0 : file.Length;
                    var ticks = file == null ? 0 : file.LastWriteUtcTicks;
                    rows.Add(rel + "|" + length.ToString(CultureInfo.InvariantCulture) + "|" + ticks.ToString(CultureInfo.InvariantCulture));
                }
            }

            rows.Sort(StringComparer.Ordinal);
            return Sha256Hex(string.Join("\n", rows.ToArray()));
        }

        public static string PasswordHash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "";
            }

            return Sha256Hex(password);
        }

        private static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
