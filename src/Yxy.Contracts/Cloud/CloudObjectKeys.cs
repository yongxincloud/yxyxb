using System.IO;

namespace Yxy.Contracts.Cloud
{
    public static class CloudObjectKeys
    {
        public static string ProductPrefix(string softwareId)
        {
            return Safe(string.IsNullOrWhiteSpace(softwareId) ? "用心云备份" : softwareId) + "/";
        }

        public static string ForZip(string softwareId, string kind, string taskName, string fileName)
        {
            return ProductPrefix(softwareId)
                + Safe(string.IsNullOrEmpty(kind) ? "file" : kind) + "/"
                + Safe(string.IsNullOrEmpty(taskName) ? "task" : taskName) + "/"
                + Path.GetFileName(fileName);
        }

        private static string Safe(string text)
        {
            var chars = Path.GetInvalidFileNameChars();
            var value = text ?? "";
            for (var i = 0; i < chars.Length; i++)
            {
                value = value.Replace(chars[i], '_');
            }

            return value.Replace('\\', '_').Replace('/', '_');
        }
    }
}
