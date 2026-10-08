using System;

namespace YxyXb.Contracts.Dto
{
    public sealed class ScheduledItemDto
    {
        public string Kind { get; set; }

        public long Id { get; set; }

        public string Name { get; set; }

        public bool Enabled { get; set; }

        public DateTime? NextRunUtc { get; set; }
    }
}
