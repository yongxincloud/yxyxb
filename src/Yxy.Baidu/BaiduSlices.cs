using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Yxy.Baidu
{
    public static class BaiduSlices
    {
        public const int BlockSize = 1 * 1024 * 1024;

        public static IList<string> Md5HexList(string localPath)
        {
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException("本地包不存在。", localPath);
            }

            var list = new List<string>();
            using (var md5 = MD5.Create())
            using (var input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buf = new byte[BlockSize];
                int n;
                while ((n = ReadFull(input, buf)) > 0)
                {
                    var hash = md5.ComputeHash(buf, 0, n);
                    list.Add(ToHex(hash));
                }
            }

            return list;
        }

        private static int ReadFull(Stream input, byte[] buf)
        {
            var got = 0;
            while (got < buf.Length)
            {
                var n = input.Read(buf, got, buf.Length - got);
                if (n <= 0)
                {
                    break;
                }

                got += n;
            }

            return got;
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
