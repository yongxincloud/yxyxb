using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yxy.Client.Dialogs;
using Yxy.Contracts.Dto;
using Yxy.Core.Backup;
using Yxy.Core.Runtime;
using Yxy.Core.Store;
using Yxy.Mssql;
using Yxy.MySql;

namespace Yxy.Client.Pages
{
    public sealed class TasksPage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly DataGrid _fileGrid;
        private readonly DataGrid _mssqlGrid;
        private DataGrid _mysqlGrid;

        public TasksPage(HostBootstrap boot)
        {
            _boot = boot;
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "文件", Content = BuildFile(out _fileGrid) });
            tabs.Items.Add(new TabItem { Header = "MSSQL", Content = BuildMssql(out _mssqlGrid) });
            tabs.Items.Add(new TabItem { Header = "MySQL", Content = BuildMysql() });
            tabs.Items.Add(DisabledTab("Oracle"));
            tabs.Items.Add(DisabledTab("达梦"));
            tabs.Items.Add(DisabledTab("PostgreSQL"));
            Content = tabs;
            Loaded += delegate { Reload(); };
        }

        private UIElement BuildFile(out DataGrid grid)
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("添加", delegate { AddOrEditFile(null); }));
            bar.Children.Add(Btn("修改", delegate { EditSelectedFile(); }));
            bar.Children.Add(Btn("删除", delegate { DeleteSelectedFile(); }));
            bar.Children.Add(Btn("立即执行", delegate { RunFileNow(); }));
            bar.Children.Add(Btn("刷新", delegate { ReloadFile(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            var hint = new TextBlock
            {
                Text = "点选一条文件任务后可立即执行。结果看弹窗，也看「日志 → 文件备份」。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);
            grid = GridOf();
            grid.Columns.Add(TextCol("启用", "EnabledText", 50));
            grid.Columns.Add(TextCol("名称", "Name", 140));
            grid.Columns.Add(TextCol("备份类型", "SourceKindText", 80));
            grid.Columns.Add(TextCol("周期", "CycleText", 90));
            grid.Columns.Add(TextCol("已执行", "RunCount", 70));
            grid.Columns.Add(TextCol("下次执行", "NextRunLocal", 140));
            grid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EditSelectedFile();
            };
            panel.Children.Add(grid);
            return panel;
        }

        private UIElement BuildMssql(out DataGrid grid)
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("添加", delegate { EditMssql(null); }));
            bar.Children.Add(Btn("修改", delegate { EditMssql(SelectedMssql()); }));
            bar.Children.Add(Btn("删除", delegate { DeleteSelectedMssql(); }));
            bar.Children.Add(Btn("立即执行", delegate { RunMssqlNow(); }));
            bar.Children.Add(Btn("刷新", delegate { ReloadMssql(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            var hint = new TextBlock
            {
                Text = "先在「实例」里添加并测试 MSSQL。立即执行后看弹窗和「日志 → 数据库备份」。zip 落在 bak\\库备份，离线任务到任务开始时间后能扫到。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);
            grid = GridOf();
            grid.Columns.Add(TextCol("启用", "EnabledText", 50));
            grid.Columns.Add(TextCol("名称", "Name", 140));
            grid.Columns.Add(TextCol("范围", "ScopeText", 90));
            grid.Columns.Add(TextCol("周期", "CycleText", 90));
            grid.Columns.Add(TextCol("已执行", "RunCount", 70));
            grid.Columns.Add(TextCol("上次执行", "LastRunLocal", 140));
            grid.Columns.Add(TextCol("下次执行", "NextRunLocal", 140));
            grid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EditMssql(SelectedMssql());
            };
            panel.Children.Add(grid);
            return panel;
        }

        private void Reload()
        {
            ReloadFile();
            ReloadMssql();
            ReloadMysql();
        }

        private void ReloadFile()
        {
            _fileGrid.ItemsSource = null;
            _fileGrid.ItemsSource = FileTaskStore.List(_boot.Db);
        }

        private void ReloadMssql()
        {
            _mssqlGrid.ItemsSource = null;
            _mssqlGrid.ItemsSource = MssqlTaskStore.List(_boot.Db);
        }

        private FileTaskDto SelectedFile()
        {
            return _fileGrid.SelectedItem as FileTaskDto;
        }

        private void EditSelectedFile()
        {
            var existing = SelectedFile();
            if (existing == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先点选一条文件任务，或双击该行。", "文件任务");
                return;
            }

            AddOrEditFile(existing);
        }

        private void DeleteSelectedFile()
        {
            var existing = SelectedFile();
            if (existing == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先点选一条文件任务。", "文件任务");
                return;
            }

            if (MessageBox.Show(Window.GetWindow(this), "确定删除文件任务「" + existing.Name + "」？已产生的备份包和日志会保留。", "删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            FileTaskStore.Delete(_boot.Db, existing.Id);
            ReloadFile();
        }

        private void AddOrEditFile(FileTaskDto existing)
        {
            var settings = SettingsStore.Get(_boot.Db);
            var dialog = new FileTaskDialog(settings.BakPath, existing, CloudAccountStore.EnabledProviders(_boot.Db));
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true || dialog.Task == null)
            {
                return;
            }

            if (dialog.Task.Id > 0)
            {
                FileTaskStore.Update(_boot.Db, dialog.Task);
            }
            else
            {
                FileTaskStore.Insert(_boot.Db, dialog.Task);
            }

            ReloadFile();
        }

        private void RunFileNow()
        {
            var task = SelectedFile();
            if (task == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先点选一条文件任务。", "文件任务");
                return;
            }

            try
            {
                var result = FileBackupRunner.ExecuteNow(_boot.Db, task.Id);
                ReloadFile();
                MessageBox.Show(Window.GetWindow(this), result, "立即执行");
            }
            catch (Exception ex)
            {
                ReloadFile();
                MessageBox.Show(Window.GetWindow(this), "立即执行失败：" + ex.Message, "立即执行");
            }
        }

        private UIElement BuildMysql()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("添加", delegate { EditMysql(null); }));
            bar.Children.Add(Btn("修改", delegate { EditMysql(SelectedMysql()); }));
            bar.Children.Add(Btn("删除", delegate { DeleteMysql(); }));
            bar.Children.Add(Btn("立即执行", delegate { RunMysqlNow(); }));
            bar.Children.Add(Btn("刷新", delegate { ReloadMysql(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            var hint = new TextBlock
            {
                Text = "先在「实例」里添加并测试 MySQL。备份调用 mysqldump，zip 落在 bak\\库备份。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);
            _mysqlGrid = GridOf();
            _mysqlGrid.Columns.Add(TextCol("启用", "EnabledText", 50));
            _mysqlGrid.Columns.Add(TextCol("名称", "Name", 140));
            _mysqlGrid.Columns.Add(TextCol("范围", "ScopeText", 90));
            _mysqlGrid.Columns.Add(TextCol("周期", "CycleText", 90));
            _mysqlGrid.Columns.Add(TextCol("已执行", "RunCount", 70));
            _mysqlGrid.Columns.Add(TextCol("上次执行", "LastRunLocal", 140));
            _mysqlGrid.Columns.Add(TextCol("下次执行", "NextRunLocal", 140));
            _mysqlGrid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EditMysql(SelectedMysql());
            };
            panel.Children.Add(_mysqlGrid);
            return panel;
        }

        private void ReloadMysql()
        {
            _mysqlGrid.ItemsSource = null;
            _mysqlGrid.ItemsSource = MysqlTaskStore.List(_boot.Db);
        }

        private MysqlTaskDto SelectedMysql()
        {
            return _mysqlGrid.SelectedItem as MysqlTaskDto;
        }

        private void EditMysql(MysqlTaskDto existing)
        {
            var instances = MysqlInstanceStore.List(_boot.Db);
            if (instances.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "请先在「实例」添加 MySQL 实例。", "库任务");
                return;
            }

            var dialog = new MysqlTaskDialog(instances, existing, CloudAccountStore.EnabledProviders(_boot.Db)) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            if (existing == null) MysqlTaskStore.Insert(_boot.Db, dialog.Task);
            else MysqlTaskStore.Update(_boot.Db, dialog.Task);
            ReloadMysql();
        }

        private void DeleteMysql()
        {
            var existing = SelectedMysql();
            if (existing == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择任务。", "库任务");
                return;
            }

            if (MessageBox.Show(Window.GetWindow(this), "确定删除该任务？", "删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            MysqlTaskStore.Delete(_boot.Db, existing.Id);
            ReloadMysql();
        }

        private void RunMysqlNow()
        {
            var task = SelectedMysql();
            if (task == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择任务。", "库任务");
                return;
            }

            try
            {
                var result = MysqlRunner.ExecuteNow(_boot.Db, task.Id);
                ReloadMysql();
                MessageBox.Show(Window.GetWindow(this), result, "立即执行");
            }
            catch (Exception ex)
            {
                ReloadMysql();
                MessageBox.Show(Window.GetWindow(this), ex.Message, "立即执行");
            }
        }

        private MssqlTaskDto SelectedMssql()
        {
            return _mssqlGrid.SelectedItem as MssqlTaskDto;
        }

        private void EditMssql(MssqlTaskDto existing)
        {
            var instances = MssqlInstanceStore.List(_boot.Db);
            if (instances.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "请先在「实例」添加 MSSQL 实例。", "库任务");
                return;
            }

            var dialog = new MssqlTaskDialog(instances, existing, CloudAccountStore.EnabledProviders(_boot.Db));
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true || dialog.Task == null)
            {
                return;
            }

            if (dialog.Task.Id > 0)
            {
                MssqlTaskStore.Update(_boot.Db, dialog.Task);
            }
            else
            {
                MssqlTaskStore.Insert(_boot.Db, dialog.Task);
            }

            ReloadMssql();
        }

        private void DeleteSelectedMssql()
        {
            var existing = SelectedMssql();
            if (existing == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先点选一条库任务。", "库任务");
                return;
            }

            if (MessageBox.Show(Window.GetWindow(this), "确定删除库任务「" + existing.Name + "」？已产生的备份包和日志会保留。", "删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            MssqlTaskStore.Delete(_boot.Db, existing.Id);
            ReloadMssql();
        }

        private void RunMssqlNow()
        {
            var task = SelectedMssql();
            if (task == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择一条库任务。", "库任务");
                return;
            }

            try
            {
                var result = MssqlRunner.ExecuteNow(_boot.Db, task.Id);
                ReloadMssql();
                MessageBox.Show(Window.GetWindow(this), result, "立即执行");
            }
            catch (Exception ex)
            {
                ReloadMssql();
                MessageBox.Show(Window.GetWindow(this), "立即执行失败：" + ex.Message, "立即执行");
            }
        }

        private static Button Btn(string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Height = 28, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 0, 10, 0) };
            button.Click += click;
            return button;
        }

        private static DataGrid GridOf()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column
            };
        }

        private static DataGridTextColumn TextCol(string header, string path, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(path),
                Width = width
            };
        }

        private static TabItem DisabledTab(string title)
        {
            return new TabItem { Header = title + "（后续）", IsEnabled = false };
        }
    }
}
