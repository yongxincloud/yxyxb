namespace YxyXb.Core
{
    public static class CloudSpeed
    {
        public static int UploadKbps(int upKbps, int downKbps)
        {
            if (upKbps > 0)
            {
                return upKbps;
            }

            if (downKbps > 0)
            {
                return downKbps;
            }

            return 0;
        }

        public static int DownloadKbps(int upKbps, int downKbps)
        {
            if (downKbps > 0)
            {
                return downKbps;
            }

            return 0;
        }
    }
}
