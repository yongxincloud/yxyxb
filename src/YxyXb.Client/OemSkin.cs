using System;
using System.IO;
using YxyXb.Core;
using YxyXb.Core.Security;

namespace YxyXb.Client
{
    public sealed class OemSkin
    {
        public string ProductName { get; set; }
        public string Company { get; set; }
        public string Phone { get; set; }
        public string Icon { get; set; }

        public static OemSkin Current { get; private set; }

        public static OemSkin Load()
        {
            if (Current != null)
            {
                return Current;
            }

            Current = Read();
            return Current;
        }

        public string DisplayName()
        {
            var name = ProductName == null ? "" : ProductName.Trim();
            return name.Length == 0 ? AppPaths.ProductDisplayName : name;
        }

        public string IconPath()
        {
            var file = Icon == null ? "" : Icon.Trim();
            if (file.Length == 0 || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            {
                return "";
            }

            var path = Path.Combine(AppPaths.InstallDirectory, file);
            return File.Exists(path) ? path : "";
        }

        private static OemSkin Read()
        {
            var path = Path.Combine(AppPaths.InstallDirectory, "oem.json");
            if (!File.Exists(path))
            {
                return new OemSkin();
            }

            try
            {
                var skin = JsonUtil.FromJson<OemSkin>(File.ReadAllText(path)) ?? new OemSkin();
                return skin;
            }
            catch (Exception)
            {
                return new OemSkin();
            }
        }
    }
}
