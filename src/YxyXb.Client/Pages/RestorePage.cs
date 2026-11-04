using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Backup;
using YxyXb.Core.Runtime;
using YxyXb.Core.Store;
using YxyXb.Mssql;
using YxyXb.MySql;

namespace YxyXb.Client.Pages
{
    public sealed class RestorePage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly ExclusiveBgJob _job = new ExclusiveBgJob();
        private ComboBox _mssqlInstances;
        private ComboBox _mysqlInstances;
        private TextBox _fileInput;
        private TextBox _fileOutput;
        private TextBox _filePwd;
        private TextBlock _fileStatus;
        private Button _fileRun;
        private TextBox _mssqlFile;
        private TextBox _mysqlFile;
        private TextBox _mssqlTarget;
        private TextBox _mysqlTarget;
        private TextBox _mssqlPwd;
        private TextBox _mysqlPwd;
        private CheckBox _mssqlReplace;
        private CheckBox _mysqlReplace;
        private TextBlock _mssqlStatus;
        private TextBlock _mysqlStatus;
        private Button _mssqlRun;
        private Button _mysqlRun;

        public RestorePage(HostBootstrap boot)
        {
            _boot = boot;
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "文件", Content = BuildFile() });
            tabs.Items.Add(new TabItem { Header = "MSSQL", Content = BuildMssql() });
            tabs.Items.Add(new TabItem { Header = "MySQL", Content = BuildMysql() });
            tabs.Items.Add(new TabItem { Header = "Oracle（后续）", IsEnabled = false });
            tabs.Items.Add(new TabItem { Header = "达梦（后续）", IsEnabled = false });
            tabs.Items.Add(new TabItem { Header = "PostgreSQL（后续）", IsEnabled = false });
            Content = tabs;
            Loaded += delegate { ReloadInstances(); PrefillPassword(); };
        }

        private UIElement BuildFile()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock
            {
                Text = "把同一份备份的全部分卷和 manifest 清单下载到同一个文件夹，再选择恢复目录。程序会校验、组装并解压到新的时间戳子目录。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            _fileInput = new TextBox { Height = 28 };
            panel.Children.Add(Labeled("分卷下载目录", FolderRow(_fileInput)));
            _fileOutput = new TextBox { Height = 28 };
            panel.Children.Add(Labeled("恢复到", FolderRow(_fileOutput)));
            _filePwd = new TextBox { Height = 28 };
            panel.Children.Add(Labeled("压缩密码（可选）", _filePwd));
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            _fileRun = Btn("开始恢复", delegate { RunFileRestore(); });
            bar.Children.Add(_fileRun);
            panel.Children.Add(bar);
            _fileStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(_fileStatus);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        }

        private UIElement BuildMssql()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var hint = new TextBlock
            {
                Text = "选择备份产生的 .zip（内含 .bak）或直接选 .bak。目标实例须能读到解压文件；本机会把文件放到公共目录再恢复。覆盖已有库前请确认。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);

            var form = new StackPanel();
            _mssqlInstances = Combo("目标实例");
            form.Children.Add(Labeled("目标实例", _mssqlInstances));
            _mssqlFile = new TextBox { Height = 28 };
            form.Children.Add(Labeled("备份文件", FileRow(_mssqlFile, "Zip/Bak|*.zip;*.bak|Bak|*.bak|Zip|*.zip", delegate
            {
                if (string.IsNullOrWhiteSpace(_mssqlTarget.Text) && !string.IsNullOrWhiteSpace(_mssqlFile.Text))
                {
                    _mssqlTarget.Text = GuessDbName(_mssqlFile.Text);
                }
            })));
            _mssqlTarget = new TextBox { Height = 28 };
            form.Children.Add(Labeled("目标库名", _mssqlTarget));
            _mssqlPwd = new TextBox { Height = 28 };
            form.Children.Add(Labeled("压缩密码（可选）", _mssqlPwd));
            _mssqlReplace = new CheckBox { Content = "覆盖已存在的同名库", Margin = new Thickness(0, 4, 0, 8) };
            form.Children.Add(_mssqlReplace);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            _mssqlRun = Btn("开始恢复", delegate { RunMssql(); });
            bar.Children.Add(_mssqlRun);
            bar.Children.Add(Btn("刷新实例", delegate { ReloadInstances(); }));
            form.Children.Add(bar);
            _mssqlStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
            form.Children.Add(_mssqlStatus);
            panel.Children.Add(form);
            return panel;
        }

        private UIElement BuildMysql()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var hint = new TextBlock
            {
                Text = "选择备份产生的 .zip（内含 .sql）或直接选 .sql。需本机有 mysql.exe（与 mysqldump 同目录即可）。勾选覆盖会先删再建目标库。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);

            var form = new StackPanel();
            _mysqlInstances = Combo("目标实例");
            form.Children.Add(Labeled("目标实例", _mysqlInstances));
            _mysqlFile = new TextBox { Height = 28 };
            form.Children.Add(Labeled("备份文件", FileRow(_mysqlFile, "Zip/Sql|*.zip;*.sql|Sql|*.sql|Zip|*.zip", delegate
            {
                if (string.IsNullOrWhiteSpace(_mysqlTarget.Text) && !string.IsNullOrWhiteSpace(_mysqlFile.Text))
                {
                    _mysqlTarget.Text = GuessDbName(_mysqlFile.Text);
                }
            })));
            _mysqlTarget = new TextBox { Height = 28 };
            form.Children.Add(Labeled("目标库名", _mysqlTarget));
            _mysqlPwd = new TextBox { Height = 28 };
            form.Children.Add(Labeled("压缩密码（可选）", _mysqlPwd));
            _mysqlReplace = new CheckBox { Content = "覆盖已存在的同名库", Margin = new Thickness(0, 4, 0, 8) };
            form.Children.Add(_mysqlReplace);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            _mysqlRun = Btn("开始恢复", delegate { RunMysql(); });
            bar.Children.Add(_mysqlRun);
            bar.Children.Add(Btn("刷新实例", delegate { ReloadInstances(); }));
            form.Children.Add(bar);
            _mysqlStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
            form.Children.Add(_mysqlStatus);
            panel.Children.Add(form);
            return panel;
        }

        private void PrefillPassword()
        {
            try
            {
                var pwd = SettingsStore.Get(_boot.Db).ZipPassword ?? "";
                if (_mssqlPwd != null && string.IsNullOrEmpty(_mssqlPwd.Text)) _mssqlPwd.Text = pwd;
                if (_mysqlPwd != null && string.IsNullOrEmpty(_mysqlPwd.Text)) _mysqlPwd.Text = pwd;
                if (_filePwd != null && string.IsNullOrEmpty(_filePwd.Text)) _filePwd.Text = pwd;
            }
            catch
            {
            }
        }

        private void RunFileRestore()
        {
            var input = (_fileInput.Text ?? "").Trim();
            var output = (_fileOutput.Text ?? "").Trim();
            if (!Directory.Exists(input))
            {
                MessageBox.Show(Window.GetWindow(this), "请选择包含全部分卷和清单的目录。", "文件恢复");
                return;
            }

            if (!Directory.Exists(output))
            {
                MessageBox.Show(Window.GetWindow(this), "请选择已经存在的恢复目录。", "文件恢复");
                return;
            }

            _fileRun.IsEnabled = false;
            _fileStatus.Text = "正在校验并组装分卷…";
            var password = _filePwd.Text ?? "";
            if (!_job.TryStart(
                delegate
                {
                    var result = FileRestoreService.Restore(input, output, password);
                    return "已恢复 " + result.FileCount + " 个文件到：\n" + result.OutputDirectory;
                },
                delegate(string result, Exception error)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _fileRun.IsEnabled = true;
                        if (error != null)
                        {
                            _fileStatus.Text = "失败：" + error.Message;
                            MessageBox.Show(Window.GetWindow(this), error.Message, "文件恢复");
                            return;
                        }

                        _fileStatus.Text = result;
                        MessageBox.Show(Window.GetWindow(this), _fileStatus.Text, "文件恢复");
                    }));
                }))
            {
                _fileRun.IsEnabled = true;
                MessageBox.Show(Window.GetWindow(this), "已有恢复任务在执行。", "恢复");
            }
        }

        private void ReloadInstances()
        {
            FillCombo(_mssqlInstances, MssqlInstanceStore.List(_boot.Db));
            FillCombo(_mysqlInstances, MysqlInstanceStore.List(_boot.Db));
        }

        private void RunMssql()
        {
            var instance = SelectedMssql();
            if (instance == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先在「实例」里添加并选择 MSSQL。", "恢复");
                return;
            }

            var path = (_mssqlFile.Text ?? "").Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                MessageBox.Show(Window.GetWindow(this), "请选择有效的备份文件。", "恢复");
                return;
            }

            var target = (_mssqlTarget.Text ?? "").Trim();
            if (target.Length == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "请填写目标库名。", "恢复");
                return;
            }

            if (_mssqlReplace.IsChecked == true)
            {
                if (MessageBox.Show(Window.GetWindow(this),
                    "将覆盖库「" + target + "」，原数据不可恢复。确定？",
                    "恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var pwd = _mssqlPwd.Text ?? "";
            var replace = _mssqlReplace.IsChecked == true;
            _mssqlRun.IsEnabled = false;
            _mssqlStatus.Text = "正在恢复…";
            if (!_job.TryStart(
                delegate { return MssqlRestore.Restore(instance, path, pwd, target, replace); },
                delegate(string result, Exception error)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _mssqlRun.IsEnabled = true;
                        if (error != null)
                        {
                            _mssqlStatus.Text = "失败：" + error.Message;
                            MessageBox.Show(Window.GetWindow(this), error.Message, "恢复");
                            return;
                        }

                        _mssqlStatus.Text = result;
                        MessageBox.Show(Window.GetWindow(this), result, "恢复");
                    }));
                }))
            {
                _mssqlRun.IsEnabled = true;
                MessageBox.Show(Window.GetWindow(this), "已有恢复任务在执行。", "恢复");
            }
        }

        private void RunMysql()
        {
            var instance = SelectedMysql();
            if (instance == null)
            {
                MessageBox.Show(Window.GetWindow(this), "请先在「实例」里添加并选择 MySQL。", "恢复");
                return;
            }

            var path = (_mysqlFile.Text ?? "").Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                MessageBox.Show(Window.GetWindow(this), "请选择有效的备份文件。", "恢复");
                return;
            }

            var target = (_mysqlTarget.Text ?? "").Trim();
            if (target.Length == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "请填写目标库名。", "恢复");
                return;
            }

            if (_mysqlReplace.IsChecked == true)
            {
                if (MessageBox.Show(Window.GetWindow(this),
                    "将删除并重建库「" + target + "」。确定？",
                    "恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var pwd = _mysqlPwd.Text ?? "";
            var replace = _mysqlReplace.IsChecked == true;
            _mysqlRun.IsEnabled = false;
            _mysqlStatus.Text = "正在恢复…";
            if (!_job.TryStart(
                delegate { return MysqlRestore.Restore(instance, path, pwd, target, replace); },
                delegate(string result, Exception error)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _mysqlRun.IsEnabled = true;
                        if (error != null)
                        {
                            _mysqlStatus.Text = "失败：" + error.Message;
                            MessageBox.Show(Window.GetWindow(this), error.Message, "恢复");
                            return;
                        }

                        _mysqlStatus.Text = result;
                        MessageBox.Show(Window.GetWindow(this), result, "恢复");
                    }));
                }))
            {
                _mysqlRun.IsEnabled = true;
                MessageBox.Show(Window.GetWindow(this), "已有恢复任务在执行。", "恢复");
            }
        }

        private MssqlInstanceDto SelectedMssql()
        {
            return _mssqlInstances == null ? null : _mssqlInstances.SelectedItem as MssqlInstanceDto;
        }

        private MysqlInstanceDto SelectedMysql()
        {
            return _mysqlInstances == null ? null : _mysqlInstances.SelectedItem as MysqlInstanceDto;
        }

        private static string GuessDbName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path ?? "") ?? "";
            // 备份 zip 名常含任务/时间戳，尽量取末段库名较难；先给文件名供用户改。
            return name;
        }

        private static void FillCombo(ComboBox box, System.Collections.IEnumerable items)
        {
            if (box == null) return;
            var list = new List<object>();
            foreach (var item in items) list.Add(item);
            box.ItemsSource = null;
            box.DisplayMemberPath = "Name";
            box.ItemsSource = list;
            if (list.Count > 0) box.SelectedIndex = 0;
        }

        private static ComboBox Combo(string name)
        {
            return new ComboBox { Height = 28, MinWidth = 280 };
        }

        private static UIElement Labeled(string title, UIElement body)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            stack.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 0, 0, 4) });
            stack.Children.Add(body);
            return stack;
        }

        private static UIElement FileRow(TextBox box, string filter, Action afterPick)
        {
            var row = new DockPanel();
            var browse = Btn("浏览…", delegate
            {
                var dlg = new OpenFileDialog { Filter = filter, CheckFileExists = true };
                if (dlg.ShowDialog() == true)
                {
                    box.Text = dlg.FileName;
                    if (afterPick != null) afterPick();
                }
            });
            DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse);
            row.Children.Add(box);
            return row;
        }

        private static UIElement FolderRow(TextBox box)
        {
            var row = new DockPanel();
            var browse = Btn("选择…", delegate
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        box.Text = dialog.SelectedPath;
                    }
                }
            });
            DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse);
            row.Children.Add(box);
            return row;
        }

        private static Button Btn(string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Height = 28, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 0, 10, 0) };
            button.Click += click;
            return button;
        }
    }
}
