using System.Windows;
using System.Windows.Controls;
using Yxy.Contracts.Dto;
using Yxy.Core.Runtime;
using Yxy.Core.Store;

namespace Yxy.Client.Pages
{
    public sealed class LogsPage : UserControl
    {
        public LogsPage(HostBootstrap boot)
        {
            var tabs = new TabControl();
            tabs.Items.Add(MakeTab("文件备份", boot, "file"));
            tabs.Items.Add(MakeTab("数据库备份", boot, "mssql"));
            tabs.Items.Add(MakeTab("离线介质", boot, "media"));
            tabs.Items.Add(MakeTab("自动删除", boot, "purge"));
            Content = tabs;
        }

        private static TabItem MakeTab(string header, HostBootstrap boot, string type)
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                Margin = new Thickness(0, 8, 0, 0)
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "时间", Binding = new System.Windows.Data.Binding("CreatedLocal"), Width = 150 });
            grid.Columns.Add(new DataGridTextColumn { Header = "任务", Binding = new System.Windows.Data.Binding("TaskName"), Width = 140 });
            grid.Columns.Add(new DataGridTextColumn { Header = "结果", Binding = new System.Windows.Data.Binding("Result"), Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "模式", Binding = new System.Windows.Data.Binding("Mode"), Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "说明", Binding = new System.Windows.Data.Binding("Message"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            grid.ItemsSource = Wrap(LogStore.List(boot.Db, type, 300));
            return new TabItem { Header = header, Content = grid };
        }

        private static System.Collections.Generic.List<LogRow> Wrap(System.Collections.Generic.IList<LogEntryDto> list)
        {
            var rows = new System.Collections.Generic.List<LogRow>();
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                rows.Add(new LogRow
                {
                    CreatedLocal = item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    TaskName = item.TaskName,
                    Result = item.Result == "ok" ? "成功" : "失败",
                    Mode = item.Mode == "auto" ? "自动" : "手动",
                    Message = item.Message
                });
            }

            return rows;
        }

        private sealed class LogRow
        {
            public string CreatedLocal { get; set; }

            public string TaskName { get; set; }

            public string Result { get; set; }

            public string Mode { get; set; }

            public string Message { get; set; }
        }
    }
}
