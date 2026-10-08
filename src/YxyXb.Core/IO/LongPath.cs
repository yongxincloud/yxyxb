using System;
using System.IO;

namespace YxyXb.Core.IO
{
    public static class LongPath
    {
        public static string Qualify(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                return path;
            }

            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return @"\\?\UNC\" + path.TrimStart('\\');
            }

            var full = Path.GetFullPath(path);
            if (full.Length >= 240)
            {
                return @"\\?\" + full;
            }

            return full;
        }

        public static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "unnamed";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars).Trim();
        }
    }
}
