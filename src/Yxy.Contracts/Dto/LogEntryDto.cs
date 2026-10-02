using System;

namespace Yxy.Contracts.Dto
{
    public sealed class LogEntryDto
    {
        public long Id { get; set; }

        public string Type { get; set; }

        public string Mode { get; set; }

        public string Result { get; set; }

        public string Message { get; set; }

        public string TaskName { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
