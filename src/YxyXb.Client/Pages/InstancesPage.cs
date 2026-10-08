using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YxyXb.Client.Dialogs;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Runtime;
using YxyXb.Core.Store;
using YxyXb.Mssql;
using YxyXb.MySql;

namespace YxyXb.Client.Pages
{
    public sealed class InstancesPage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly DataGrid _grid;
        private DataGrid _mysqlGrid;

        public InstancesPage(HostBootstrap boot)
        {
            _boot = boot;
            var tabs = new TabControl();
            var mssql = new TabItem { Header = "MSSQL" };
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("添加", delegate { Edit(null); }));
            bar.Children.Add(Btn("修改", delegate { Edit(Selected()); }));
            bar.Children.Add(Btn("删除", delegate { DeleteSelected(); }));
            bar.Children.Add(Btn("测试连接", delegate { TestSelected(); }));
            bar.Children.Add(Btn("刷新", delegate { Reload(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            var hint = new TextBlock
            {
                Text = "先添加本机或局域网 SQL Server 实例并测试连接。Oracle / MySQL / 达梦 / PostgreSQL 后续开通。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column
            };
            _grid.Columns.Add(Col("名称", "Name", 140));
            _grid.Columns.Add(Col("服务器", "ServerText", 180));
            _grid.Columns.Add(Col("验证", "AuthText", 90));
            _grid.Columns.Add(Col("用户", "Username", 100));
            _grid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                Edit(Selected());
            };
            panel.Children.Add(_grid);
            mssql.Content = panel;
            tabs.Items.Add(mssql);
            tabs.Items.Add(BuildMysql());
            tabs.Items.Add(new TabItem { Header = "Oracle（后续）", IsEnabled = false });
            tabs.Items.Add(new TabItem { Header = "达梦（后续）", IsEnabled = false });
            tabs.Items.Add(new TabItem { Header = "PostgreSQL（后续）", IsEnabled = false });
            Content = tabs;
            Loaded += delegate { Reload(); ReloadMysql(); };
        }

        private TabItem BuildMysql()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("添加", delegate { EditMysql(null); }));
            bar.Children.Add(Btn("修改", delegate { EditMysql(SelectedMysql()); }));
            bar.Children.Add(Btn("删除", delegate { DeleteMysql(); }));
            bar.Children.Add(Btn("测试连接", delegate { TestMysql(); }));
            bar.Children.Add(Btn("刷新", delegate { ReloadMysql(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            var hint = new TextBlock
            {
                Text = "添加 MySQL 实例并测试连接。备份使用 mysqldump.exe，可放在程序目录。系统库 information_schema、mysql、performance_schema、sys 不会备份。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);
            _mysqlGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column
            };
            _mysqlGrid.Columns.Add(Col("名称", "Name", 140));
            _mysqlGrid.Columns.Add(Col("服务器", "ServerText", 180));
            _mysqlGrid.Columns.Add(Col("用户", "Username", 100));
            _mysqlGrid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EditMysql(SelectedMysql());
            };
            panel.Children.Add(_mysqlGrid);
            return new TabItem { Header = "MySQL", Content = panel };
        }

        private void Reload()
        {
            _grid.ItemsSource = null;
            _grid.ItemsSource = MssqlInstanceStore.List(_boot.Db);
        }

        private MssqlInstanceDto Selected()
        {
            return _grid.SelectedItem as MssqlInstanceDto;
        }

        private void Edit(MssqlInstanceDto existing)
        {
            var dialog = new InstanceDialog(existing);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true || dialog.Instance == null)
            {
                return;
            }

            if (dialog.Instance.Id > 0)
            {
                MssqlInstanceStore.Update(_boot.Db, dialog.Instance, dialog.PasswordPlain, dialog.KeepPassword);
            }
            else
            {
                MssqlInstanceStore.Insert(_boot.Db, dialog.Instance, dialog.PasswordPlain);
            }

            Reload();
        }

        private void DeleteSelected()
        {
            var item = Selected();
            if (item == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择实例。", "实例");
                return;
            }

            if (MessageBox.Show(Window.GetWindow(this), "删除实例会同时删除其库任务。确定？", "删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            MssqlInstanceStore.Delete(_boot.Db, item.Id);
            Reload();
        }

        private void TestSelected()
        {
            var item = Selected();
            if (item == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择实例。", "实例");
                return;
            }

            MessageBox.Show(Window.GetWindow(this), MssqlConnect.Test(item), "测试连接");
        }

        private void ReloadMysql()
        {
            _mysqlGrid.ItemsSource = null;
            _mysqlGrid.ItemsSource = MysqlInstanceStore.List(_boot.Db);
        }

        private MysqlInstanceDto SelectedMysql()
        {
            return _mysqlGrid.SelectedItem as MysqlInstanceDto;
        }

        private void EditMysql(MysqlInstanceDto existing)
        {
            var dialog = new MysqlInstanceDialog(existing) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            if (existing == null) MysqlInstanceStore.Insert(_boot.Db, dialog.Instance, dialog.PasswordPlain);
            else MysqlInstanceStore.Update(_boot.Db, dialog.Instance, dialog.PasswordPlain, dialog.KeepPassword);
            ReloadMysql();
        }

        private void DeleteMysql()
        {
            var item = SelectedMysql();
            if (item == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择实例。", "实例");
                return;
            }

            if (MessageBox.Show(Window.GetWindow(this), "删除实例会同时删除其库任务。确定？", "删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            MysqlInstanceStore.Delete(_boot.Db, item.Id);
            ReloadMysql();
        }

        private void TestMysql()
        {
            var item = SelectedMysql();
            if (item == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先选择实例。", "实例");
                return;
            }

            MessageBox.Show(Window.GetWindow(this), MysqlConnect.Test(item), "测试连接");
        }

        private static Button Btn(string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Height = 28, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 0, 10, 0) };
            button.Click += click;
            return button;
        }

        private static DataGridTextColumn Col(string header, string path, double width)
        {
            return new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(path), Width = width };
        }
    }
}
