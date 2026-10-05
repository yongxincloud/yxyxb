namespace Yxy.Baidu
{
    public static class BaiduUploadTimeout
    {
        public static int Seconds(int maxKbps, int blockBytes)
        {
            if (maxKbps <= 0)
            {
                return 600;
            }

            if (blockBytes <= 0)
            {
                return 120;
            }

            var bytesPerSec = maxKbps * 1024L;
            var need = (int)((blockBytes + bytesPerSec - 1) / bytesPerSec) * 2 + 30;
            return need < 120 ? 120 : need;
        }
    }
}
