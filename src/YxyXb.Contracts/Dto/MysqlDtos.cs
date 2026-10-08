using System;

namespace YxyXb.Contracts.Dto
{
    public sealed class MysqlInstanceDto
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string Server { get; set; }

        public int Port { get; set; }

        public string Username { get; set; }

        public string PasswordProtected { get; set; }

        public int TimeoutSeconds { get; set; }

        public bool Enabled { get; set; }

        public string ServerText
        {
            get
            {
                var server = string.IsNullOrEmpty(Server) ? "localhost" : Server;
                var port = Port > 0 ? Port : 3306;
                return server + ":" + port;
            }
        }
    }

    public sealed class MysqlTaskDto
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public long InstanceId { get; set; }

        public bool Enabled { get; set; }

        public string DbScope { get; set; }

        public string SelectedDbs { get; set; }

        public int RetryMinutes { get; set; }

        public int SortOrder { get; set; }

        public string CycleKind { get; set; }

        public int CycleInterval { get; set; }

        public DateTime? NextRunUtc { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public int RunCount { get; set; }

        public bool DoNetwork { get; set; }

        public bool DoFtp { get; set; }

        public bool DoCloud { get; set; }

        public string CloudProvider { get; set; }

        public bool DoDeleteNow { get; set; }

        public bool DoEmail { get; set; }

        public string EnabledText
        {
            get { return Enabled ? "是" : "否"; }
        }

        public string ScopeText
        {
            get { return DbScope == "selected" ? "指定库" : "全部用户库"; }
        }

        public string CycleText
        {
            get { return FileTaskDto.CycleLabel(CycleKind) + " / " + CycleInterval; }
        }

        public string NextRunLocal
        {
            get
            {
                return NextRunUtc.HasValue
                    ? NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "";
            }
        }

        public string LastRunLocal
        {
            get
            {
                return LastRunUtc.HasValue
                    ? LastRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "未执行";
            }
        }
    }
}
