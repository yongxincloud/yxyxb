namespace Yxy.Contracts.Dto
{
    public sealed class HomeCountsDto
    {
        public int FileTaskCount { get; set; }

        public int FileExecutedCount { get; set; }

        public int MssqlTaskCount { get; set; }

        public int MssqlExecutedCount { get; set; }

        public int MysqlTaskCount { get; set; }

        public int MysqlExecutedCount { get; set; }

        public int TodayErrorCount { get; set; }

        public int AutoLogCount { get; set; }

        public int ManualLogCount { get; set; }
    }
}
