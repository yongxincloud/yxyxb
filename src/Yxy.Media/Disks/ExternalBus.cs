using Yxy.Media.Native;

namespace Yxy.Media.Disks
{
    public static class ExternalBus
    {
        public static bool IsAllowed(string bus)
        {
            return bus == "USB" || bus == "1394" || bus == "SD";
        }

        public static string Resolve(byte ioctlBus, string pnp, string interfaceType, string extra, string ancestors)
        {
            var named = NativeIo.BusName(ioctlBus);
            if (IsAllowed(named))
            {
                return named;
            }

            var blob = ((pnp ?? "") + " " + (interfaceType ?? "") + " " + (extra ?? "") + " " + (ancestors ?? "")).ToUpperInvariant();
            if (LooksUsb(blob))
            {
                return "USB";
            }

            if (blob.Contains("1394") || blob.Contains("FIREWIRE"))
            {
                return "1394";
            }

            if (LooksSd(blob))
            {
                return "SD";
            }

            return named;
        }

        public static bool LooksUsb(string blob)
        {
            if (string.IsNullOrEmpty(blob))
            {
                return false;
            }

            var text = blob.ToUpperInvariant();
            if (text.Contains("USBSTOR") || text.Contains("USBUASP") || text.Contains("UASSTOR") || text.Contains("UASP"))
            {
                return true;
            }

            if (text.Contains("EXTERNAL HARD DISK") || text.Contains("REMOVABLE MEDIA") || text.Contains("REMOVABLE DISK"))
            {
                return true;
            }

            return text.Contains("USB\\") || text.Contains("USB ") || text.Contains(" USB") || text == "USB"
                || text.StartsWith("USB") || text.Contains("\\USB");
        }

        private static bool LooksSd(string blob)
        {
            return blob.Contains("SDBUS") || blob.Contains("\\SD") || blob.Contains(" SD") || blob.Contains("MMC");
        }
    }
}
