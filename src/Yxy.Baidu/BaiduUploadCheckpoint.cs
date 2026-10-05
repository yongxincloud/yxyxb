using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Yxy.Baidu
{
    public sealed class BaiduUploadCheckpoint
    {
        public string LocalPath { get; set; }

        public long Size { get; set; }

        public int BlockSize { get; set; }

        public string[] BlockMd5 { get; set; }

        public string RemotePath { get; set; }

        public string UploadId { get; set; }

        public int[] DonePartSeq { get; set; }

        public string UpdatedUtc { get; set; }

        public static string FilePath(string dataDir, string localPath)
        {
            var root = Path.Combine(dataDir ?? "", "baidu-upload");
            Directory.CreateDirectory(root);
            return Path.Combine(root, HashPath(localPath) + ".json");
        }

        public static BaiduUploadCheckpoint TryLoad(string dataDir, string localPath, long size, IList<string> md5s)
        {
            var path = FilePath(dataDir, localPath);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cp = new JavaScriptSerializer().Deserialize<BaiduUploadCheckpoint>(json);
                if (cp == null)
                {
                    return null;
                }

                if (!string.Equals(NormalizePath(cp.LocalPath), NormalizePath(localPath), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (cp.Size != size || cp.BlockSize != BaiduSlices.BlockSize)
                {
                    return null;
                }

                if (!Md5Equal(cp.BlockMd5, md5s))
                {
                    return null;
                }

                if (cp.DonePartSeq == null)
                {
                    cp.DonePartSeq = new int[0];
                }

                return cp;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(string dataDir, BaiduUploadCheckpoint cp)
        {
            if (cp == null)
            {
                throw new ArgumentNullException("cp");
            }

            var path = FilePath(dataDir, cp.LocalPath);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            cp.UpdatedUtc = DateTime.UtcNow.ToString("o");
            if (cp.DonePartSeq == null)
            {
                cp.DonePartSeq = new int[0];
            }

            File.WriteAllText(path, new JavaScriptSerializer().Serialize(cp), new UTF8Encoding(false));
        }

        public static void Delete(string dataDir, string localPath)
        {
            var path = FilePath(dataDir, localPath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void MarkDone(int partSeq)
        {
            var set = new SortedSet<int>();
            if (DonePartSeq != null)
            {
                foreach (var n in DonePartSeq)
                {
                    set.Add(n);
                }
            }

            set.Add(partSeq);
            var arr = new int[set.Count];
            set.CopyTo(arr);
            DonePartSeq = arr;
        }

        public bool IsComplete(int partCount)
        {
            if (partCount <= 0)
            {
                return false;
            }

            if (DonePartSeq == null || DonePartSeq.Length != partCount)
            {
                return false;
            }

            var set = new HashSet<int>(DonePartSeq);
            for (var i = 0; i < partCount; i++)
            {
                if (!set.Contains(i))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Md5Equal(string[] stored, IList<string> expect)
        {
            if (stored == null || expect == null || stored.Length != expect.Count)
            {
                return false;
            }

            for (var i = 0; i < stored.Length; i++)
            {
                if (!string.Equals(stored[i], expect[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path.Trim();
            }
        }

        private static string HashPath(string localPath)
        {
            var norm = NormalizePath(localPath).ToLowerInvariant();
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(norm));
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
