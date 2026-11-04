namespace YxyXb.Contracts.Cloud
{
    public sealed class CloudObjectInfo
    {
        public long Size { get; set; }

        public string Checksum { get; set; }

        public string ChecksumKind { get; set; }
    }
}

