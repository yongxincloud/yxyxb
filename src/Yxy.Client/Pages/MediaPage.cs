using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yxy.Client.Dialogs;
using Yxy.Contracts.Dto;
using Yxy.Core.Runtime;
using Yxy.Core.Store;
using Yxy.Media;
using Yxy.Media.Disks;

namespace Yxy.Client.Pages
{
    public sealed class MediaPage : UserControl
    {
        private readonly HostBootstrap _boot;
        private DataGrid _liveGrid;
        private DataGrid _regGrid;
        private TextBlock _regHint;
        private DataGrid _taskGrid;
        private DataGrid _statusGrid;

        public MediaPage(HostBootstrap boot)
        {
            _boot = boot;
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "登记介质", Content = BuildRegister() });
            tabs.Items.Add(new TabItem { Header = "介质任务", Content = BuildTasks() });
            tabs.Items.Add(new TabItem { Header = "状态", Content = BuildStatus() });
            Content = tabs;
            Loaded += delegate { ReloadAll(); };
        }

        private UIElement BuildRegister()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("刷新接入盘", delegate { ReloadLive(); }));
            bar.Children.Add(Btn("登记选中", delegate { RegisterSelected(); }));
            bar.Children.Add(Btn("解绑登记", delegate { UnbindSelected(); }));
            bar.Children.Add(Btn("确认盘完好", delegate { ClearTamper(); }));
            bar.Children.Add(Btn("标记疑似篡改", delegate { MarkTamper(); }));
            bar.Children.Add(Btn("在线/离线", delegate { ToggleLetter(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);

            var split = new Grid();
            split.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            split.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            split.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            split.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            split.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 140 });

            var liveTitle = LabelOf("接入盘（USB / 移动 SSD，身份=序列号/签名/容量。双击登记）");
            _liveGrid = GridOf();
            _liveGrid.Columns.Add(Col("身份", "IdentityText", 260));
            _liveGrid.Columns.Add(Col("总线", "BusType", 70));
            _liveGrid.Columns.Add(Col("剩余空间", "FreeSpaceText", 90));
            _liveGrid.Columns.Add(Col("状态", "StatusText", 130));
            _liveGrid.MouseDoubleClick += LiveGrid_OnDoubleClick;

            var regTitle = LabelOf("已登记（解绑 / 确认盘完好请点下面这一行）");
            _regHint = LabelOf("暂无登记。选中上方接入盘后点「登记选中」。");
            _regGrid = GridOf();
            _regGrid.MinHeight = 140;
            _regGrid.Columns.Add(Col("身份", "IdentityText", 260));
            _regGrid.Columns.Add(Col("盘符", "LetterText", 60));
            _regGrid.Columns.Add(Col("容量", "CapacityGb", 60));
            _regGrid.Columns.Add(Col("剩余空间", "FreeSpaceText", 90));
            _regGrid.Columns.Add(Col("疑似篡改", "TamperText", 80));
            _regGrid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };

            Grid.SetRow(liveTitle, 0);
            Grid.SetRow(_liveGrid, 1);
            Grid.SetRow(regTitle, 2);
            Grid.SetRow(_regHint, 3);
            Grid.SetRow(_regGrid, 4);
            split.Children.Add(liveTitle);
            split.Children.Add(_liveGrid);
            split.Children.Add(regTitle);
            split.Children.Add(_regHint);
            split.Children.Add(_regGrid);
            panel.Children.Add(split);
            return panel;
        }

        private UIElement BuildTasks()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("添加", delegate { AddTask(); }));
            bar.Children.Add(Btn("修改", delegate { EditSelectedTask(); }));
            bar.Children.Add(Btn("删除", delegate { DeleteSelectedTask(); }));
            bar.Children.Add(Btn("立即执行", delegate { RunSelectedNow(); }));
            bar.Children.Add(Btn("刷新", delegate { ReloadTasks(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            var hint = new TextBlock
            {
                Text = "立即执行后弹窗会写成功/失败。也可看「已执行」次数、「上次执行」，或左侧「日志 → 离线介质」。隔离不会格式化；重新插入或磁盘管理「联机」即可。若 Windows 提示格式化，点取消。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);
            _taskGrid = GridOf();
            _taskGrid.Columns.Add(Col("启用", "EnabledText", 50));
            _taskGrid.Columns.Add(Col("名称", "Name", 120));
            _taskGrid.Columns.Add(Col("扫描源", "SourcePath", 180));
            _taskGrid.Columns.Add(Col("开始/离线", "WindowText", 120));
            _taskGrid.Columns.Add(Col("周期", "CycleText", 90));
            _taskGrid.Columns.Add(Col("已执行", "RunCount", 70));
            _taskGrid.Columns.Add(Col("上次执行", "LastRunLocal", 140));
            _taskGrid.Columns.Add(Col("下次执行", "NextRunLocal", 140));
            _taskGrid.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EditSelectedTask();
            };
            panel.Children.Add(_taskGrid);
            return panel;
        }

        private UIElement BuildStatus()
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            panel.Children.Add(new TextBlock
            {
                Text = "已登记 / 已挂载 / 已隔离 / 异常。界面不使用「物理断开」。",
                Margin = new Thickness(0, 0, 0, 8)
            });
            _statusGrid = GridOf();
            _statusGrid.Columns.Add(Col("身份", "Identity", 260));
            _statusGrid.Columns.Add(Col("剩余空间", "FreeSpace", 90));
            _statusGrid.Columns.Add(Col("状态", "Status", 160));
            panel.Children.Add(_statusGrid);
            return panel;
        }

        private void ReloadAll()
        {
            ReloadLive();
            try
            {
                var deviceId = _boot.Device == null ? null : _boot.Device.DeviceId;
                MediaService.RecoverAttached(_boot.Db, deviceId);
            }
            catch (Exception)
            {
            }

            ReloadReg();
            ReloadTasks();
            ReloadStatus();
        }

        private void ReloadLive()
        {
            try
            {
                _liveGrid.ItemsSource = null;
                _liveGrid.ItemsSource = DiskEnumerator.ListRemovable();
            }
            catch (Exception ex)
            {
                Alert("刷新接入盘失败：" + ex.Message);
            }
        }

        private void ReloadReg()
        {
            try
            {
                var list = MediaStore.List(_boot.Db);
                FillRegisteredFreeSpace(list);
                _regGrid.ItemsSource = null;
                _regGrid.ItemsSource = list;
                _regHint.Text = list.Count == 0
                    ? "暂无登记。选中上方接入盘后点「登记选中」。"
                    : "已登记 " + list.Count + " 台。";
            }
            catch (Exception ex)
            {
                _regHint.Text = "读取已登记失败：" + ex.Message;
            }
        }

        private void ReloadTasks()
        {
            _taskGrid.ItemsSource = null;
            _taskGrid.ItemsSource = MediaTaskStore.List(_boot.Db);
        }

        private void ReloadStatus()
        {
            var rows = new List<StatusRow>();
            var registered = MediaStore.List(_boot.Db);
            for (var i = 0; i < registered.Count; i++)
            {
                var item = registered[i];
                var found = DiskEnumerator.Find(item.SerialNumber, item.DiskSignature, item.CapacityGb);
                var free = found == null ? "未接入" : found.FreeSpaceText;
                string status;
                if (item.TamperPending)
                {
                    status = "异常（疑似篡改）";
                }
                else if (found == null)
                {
                    status = "已隔离/未接入";
                }
                else if (!string.IsNullOrEmpty(found.DriveLetter))
                {
                    status = "已挂载";
                }
                else
                {
                    status = "已登记";
                }

                rows.Add(new StatusRow { Identity = item.IdentityText, FreeSpace = free, Status = status });
            }

            _statusGrid.ItemsSource = rows;
        }

        private void LiveGrid_OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_liveGrid.SelectedItem is LiveDiskDto)
            {
                RegisterSelected();
            }
        }

        private void RegisterSelected()
        {
            var disk = _liveGrid.SelectedItem as LiveDiskDto;
            if (disk == null)
            {
                Alert("请先在「接入盘」里点选一行。");
                return;
            }

            SaveDisk(disk, false);
        }

        private void SaveDisk(LiveDiskDto disk, bool takeOver)
        {
            try
            {
                var settings = SettingsStore.Get(_boot.Db);
                var deviceId = _boot.Device == null ? null : _boot.Device.DeviceId;
                var result = MediaService.Register(_boot.Db, disk, deviceId, settings.SoftwareId, settings.BakPath, takeOver);
                if (result != null && result.StartsWith(MediaService.ForeignPrefix, StringComparison.Ordinal))
                {
                    var msg = result.Substring(MediaService.ForeignPrefix.Length) + "\n是否在本机重新登记并写入下方列表？";
                    if (MessageBox.Show(Window.GetWindow(this), msg, "登记", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        SaveDisk(disk, true);
                    }

                    return;
                }

                ReloadAll();
                if (result == null)
                {
                    Alert("登记成功，已出现在下方「已登记」。");
                    return;
                }

                Alert(result);
            }
            catch (Exception ex)
            {
                ReloadAll();
                Alert("登记失败：" + ex.Message);
            }
        }

        private void UnbindSelected()
        {
            var item = SelectedRegistered();
            if (item == null)
            {
                Alert("请先在下面「已登记」里点选一行，或选中已登记的接入盘。");
                return;
            }

            var live = DiskEnumerator.Find(item.SerialNumber, item.DiskSignature, item.CapacityGb);
            var hint = MediaService.UnbindHint(live == null ? null : live.DriveLetter);
            if (MessageBox.Show(Window.GetWindow(this), hint, "解绑", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var message = MediaService.Unbind(_boot.Db, item.Id, live == null ? null : live.DriveLetter);
                ReloadAll();
                Alert(message);
            }
            catch (Exception ex)
            {
                Alert("解绑失败：" + ex.Message);
            }
        }

        private void ClearTamper()
        {
            var item = SelectedRegistered();
            if (item == null)
            {
                Alert("请先在「已登记」里点选一行。");
                return;
            }

            if (!item.TamperPending)
            {
                Alert("当前无篡改标记。可先点「标记疑似篡改」再点「确认盘完好」做验证。");
                return;
            }

            MediaStore.SetTamper(_boot.Db, item.Id, false);
            ReloadAll();
            Alert("已确认盘完好，恢复清理。");
        }

        private void ToggleLetter()
        {
            var item = SelectedRegistered();
            if (item == null)
            {
                Alert("请先在「已登记」里点选一行。");
                return;
            }

            try
            {
                var message = MediaService.ToggleLetter(_boot.Db, item);
                ReloadAll();
                Alert(message);
            }
            catch (Exception ex)
            {
                ReloadAll();
                Alert("切换失败：" + ex.Message);
            }
        }

        private void MarkTamper()
        {
            var item = SelectedRegistered();
            if (item == null)
            {
                Alert("请先在「已登记」里点选一行。");
                return;
            }

            MediaStore.SetTamper(_boot.Db, item.Id, true);
            ReloadAll();
            Alert("已标记疑似篡改。现在可以点「确认盘完好」清除。");
        }

        private MediaRecordDto SelectedRegistered()
        {
            var item = _regGrid.SelectedItem as MediaRecordDto;
            if (item != null)
            {
                return item;
            }

            var disk = _liveGrid.SelectedItem as LiveDiskDto;
            if (disk == null)
            {
                return null;
            }

            return MediaStore.Find(_boot.Db, disk.SerialNumber, disk.DiskSignature, disk.CapacityGb);
        }

        private void RunSelectedNow()
        {
            var task = _taskGrid.SelectedItem as MediaTaskDto;
            if (task == null)
            {
                Alert("请先在「介质任务」里点选一行。");
                return;
            }

            try
            {
                var result = MediaRunner.ExecuteNow(_boot.Db, task.Id);
                ReloadAll();
                Alert(result);
            }
            catch (Exception ex)
            {
                ReloadAll();
                Alert("立即执行失败：" + ex.Message);
            }
        }

        private void AddTask()
        {
            OpenTaskDialog(null);
        }

        private void EditSelectedTask()
        {
            var task = _taskGrid.SelectedItem as MediaTaskDto;
            if (task == null)
            {
                Alert("请先点选一条介质任务，或双击该行。");
                return;
            }

            OpenTaskDialog(task);
        }

        private void DeleteSelectedTask()
        {
            var task = _taskGrid.SelectedItem as MediaTaskDto;
            if (task == null)
            {
                Alert("请先点选一条介质任务。");
                return;
            }

            if (MessageBox.Show(Window.GetWindow(this), "确定删除介质任务「" + task.Name + "」？已产生的备份包和日志会保留。", "删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            MediaTaskStore.Delete(_boot.Db, task.Id);
            ReloadTasks();
        }

        private void OpenTaskDialog(MediaTaskDto existing)
        {
            var media = MediaStore.List(_boot.Db);
            if (media.Count == 0)
            {
                Alert("请先登记介质。");
                return;
            }

            var settings = SettingsStore.Get(_boot.Db);
            var dialog = new MediaTaskDialog(settings.BakPath, media, existing);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true && dialog.Task != null)
            {
                if (dialog.Task.Id > 0)
                {
                    MediaTaskStore.Update(_boot.Db, dialog.Task);
                }
                else
                {
                    MediaTaskStore.Insert(_boot.Db, dialog.Task);
                }

                ReloadTasks();
            }
        }

        private static void FillRegisteredFreeSpace(IList<MediaRecordDto> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var found = DiskEnumerator.Find(item.SerialNumber, item.DiskSignature, item.CapacityGb);
                item.FreeSpaceText = found == null ? "未接入" : found.FreeSpaceText;
                item.LetterText = found == null || string.IsNullOrEmpty(found.DriveLetter) ? "无" : found.DriveLetter;
            }
        }

        private void Alert(string text)
        {
            MessageBox.Show(Window.GetWindow(this), text, "离线介质");
        }

        private Button Btn(string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Height = 28, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 0, 10, 0) };
            button.Click += click;
            return button;
        }

        private static TextBlock LabelOf(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 4) };
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
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private static DataGridTextColumn Col(string header, string path, double width)
        {
            return new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(path), Width = width };
        }

        private sealed class StatusRow
        {
            public string Identity { get; set; }

            public string FreeSpace { get; set; }

            public string Status { get; set; }
        }
    }
}
