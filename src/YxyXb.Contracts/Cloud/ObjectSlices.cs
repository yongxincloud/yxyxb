namespace YxyXb.Contracts.Cloud
{
    public sealed class ObjectSlicePlan
    {
        public bool Simple { get; set; }

        public long PartSize { get; set; }

        public int PartCount { get; set; }
    }

    public static class ObjectSlices
    {
        public const long MinPartBytes = 5L * 1024 * 1024;

        public const int MaxPartCount = 10000;

        public static ObjectSlicePlan Plan(long length)
        {
            if (length < 0)
            {
                length = 0;
            }

            if (length <= MinPartBytes)
            {
                return new ObjectSlicePlan
                {
                    Simple = true,
                    PartSize = length,
                    PartCount = length == 0 ? 0 : 1
                };
            }

            var size = MinPartBytes;
            var count = (length + size - 1) / size;
            if (count > MaxPartCount)
            {
                size = (length + MaxPartCount - 1) / MaxPartCount;
                count = (length + size - 1) / size;
            }

            return new ObjectSlicePlan
            {
                Simple = false,
                PartSize = size,
                PartCount = (int)count
            };
        }
    }
}
