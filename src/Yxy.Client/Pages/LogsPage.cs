using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Yxy.Baidu;
using Yxy.Contracts.Dto;
using Yxy.Core;
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
            tabs.Items.Add(MakeTab("MSSQL", boot, "mssql"));
            tabs.Items.Add(MakeTab("MySQL", boot, "mysql"));
            tabs.Items.Add(MakeTab("离线介质", boot, "media"));
            tabs.Items.Add(MakeTab("自动删除", boot, "purge"));
            tabs.Items.Add(MakeTab("百度分片", boot, "baidu"));
            Content = tabs;
        }

        private static TabItem MakeTab(string header, HostBootstrap boot, string type)
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Margin = new Thickness(0, 8, 0, 8)
            };
            ScrollViewer.SetVerticalScrollBarVisibility(grid, ScrollBarVisibility.Visible);
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
            grid.Columns.Add(new DataGridTextColumn { Header = "时间", Binding = new Binding("CreatedLocal"), Width = 150 });
            grid.Columns.Add(new DataGridTextColumn { Header = "任务", Binding = new Binding("TaskName"), Width = 140 });
            grid.Columns.Add(new DataGridTextColumn { Header = "结果", Binding = new Binding("Result"), Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "模式", Binding = new Binding("Mode"), Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "说明", Binding = new Binding("Preview"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            var detail = ScrollableMessage.CreateBox();
            var extra = type == "baidu" ? " 文件：" + AppPaths.BaiduUploadDirectory : "";
            var hint = new TextBlock
            {
                Text = "点选一行后，完整说明在下方滚动查看。进行中自动刷新。" + extra,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            grid.SelectionChanged += delegate
            {
                var row = grid.SelectedItem as LogRow;
                detail.Text = row == null ? "" : (row.Message ?? "");
            };

            Action reload = delegate
            {
                var keep = grid.SelectedIndex;
                var rows = Wrap(LogStore.List(boot.Db, type, 300));
                grid.ItemsSource = rows;
                if (rows.Count == 0)
                {
                    detail.Text = "";
                }
                else if (keep >= 0 && keep < rows.Count)
                {
                    grid.SelectedIndex = keep;
                }
                else
                {
                    grid.SelectedIndex = 0;
                }
            };
            reload();

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var refresh = new Button
            {
                Content = "刷新",
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            refresh.Click += delegate { reload(); };
            var clear = new Button
            {
                Content = "清空日志",
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0)
            };
            clear.Click += delegate
            {
                var owner = Window.GetWindow(grid);
                if (MessageBox.Show(owner, "确定清空「" + header + "」全部日志？备份包不会删除。", "清空日志", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                LogStore.DeleteByType(boot.Db, type);
                reload();
            };
            bar.Children.Add(refresh);
            bar.Children.Add(clear);

            TextBox fileBox = null;
            if (type == "baidu")
            {
                fileBox = ScrollableMessage.CreateBox();
                fileBox.MinHeight = 100;
            }

            var root = new DockPanel { LastChildFill = true, Margin = new Thickness(8) };
            var bottom = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(bottom, Dock.Bottom);
            DockPanel.SetDock(hint, Dock.Top);
            bottom.Height = type == "baidu" ? 280 : 180;
            bottom.Children.Add(hint);
            if (fileBox != null)
            {
                DockPanel.SetDock(fileBox, Dock.Top);
                bottom.Children.Add(fileBox);
            }
            bottom.Children.Add(detail);
            DockPanel.SetDock(bar, Dock.Top);
            root.Children.Add(bottom);
            root.Children.Add(bar);
            root.Children.Add(grid);

            if (type == "baidu")
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                Action pulse = delegate
                {
                    if (!root.IsVisible)
                    {
                        return;
                    }

                    reload();
                    try
                    {
                        var text = BaiduUploadLog.ReadRecent(AppPaths.DataDirectory, 16000);
                        fileBox.Text = string.IsNullOrEmpty(text) ? "尚无分片日志文件。" : text;
                    }
                    catch (Exception ex)
                    {
                        fileBox.Text = ex.Message;
                    }
                };
                timer.Tick += delegate { pulse(); };
                root.Loaded += delegate { timer.Start(); pulse(); };
                root.Unloaded += delegate { timer.Stop(); };
            }

            return new TabItem { Header = header, Content = root };
        }

        private static List<LogRow> Wrap(IList<LogEntryDto> list)
        {
            var rows = new List<LogRow>();
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var message = item.Message ?? "";
                rows.Add(new LogRow
                {
                    CreatedLocal = item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    TaskName = item.TaskName,
                    Result = item.Result == "ok" ? "成功" : "失败",
                    Mode = item.Mode == "auto" ? "自动" : "手动",
                    Message = message,
                    Preview = Preview(message)
                });
            }

            return rows;
        }

        private static string Preview(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }

            var text = message.Replace('\r', ' ').Replace('\n', ' ');
            return text.Length <= 80 ? text : text.Substring(0, 80) + "…";
        }

        private sealed class LogRow
        {
            public string CreatedLocal { get; set; }

            public string TaskName { get; set; }

            public string Result { get; set; }

            public string Mode { get; set; }

            public string Message { get; set; }

            public string Preview { get; set; }
        }
    }
}
