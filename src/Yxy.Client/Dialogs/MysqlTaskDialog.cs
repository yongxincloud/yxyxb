using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core.Store;

namespace Yxy.Client.Dialogs
{
    public sealed class MysqlTaskDialog : Window
    {
        private readonly long _editId;
        private readonly IList<MysqlInstanceDto> _instances;
        private readonly IList<string> _ready;
        private readonly TextBox _name = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly ComboBox _instance = new ComboBox { Margin = new Thickness(0, 0, 0, 8), DisplayMemberPath = "Name" };
        private readonly ComboBox _scope = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly TextBox _dbs = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly ComboBox _cycle = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly TextBox _interval = new TextBox { Text = "1", Margin = new Thickness(0, 0, 0, 8) };
        private readonly CheckBox _enabled = new CheckBox { Content = "启用", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
        private readonly CheckBox _network = new CheckBox { Content = "传到网络路径", Margin = new Thickness(0, 0, 12, 8) };
        private readonly CheckBox _ftp = new CheckBox { Content = "传到 FTP", Margin = new Thickness(0, 0, 12, 8) };
        private readonly CheckBox _cloud = new CheckBox { Content = "云上传", Margin = new Thickness(0, 0, 0, 8) };
        private readonly ComboBox _cmbCloud = new ComboBox { Margin = new Thickness(0, 0, 0, 8), Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly CheckBox _email = new CheckBox { Content = "完成后发邮件", Margin = new Thickness(0, 0, 0, 8) };

        public MysqlTaskDto Task { get; private set; }

        public MysqlTaskDialog(IList<MysqlInstanceDto> instances, MysqlTaskDto existing)
            : this(instances, existing, null)
        {
        }

        public MysqlTaskDialog(IList<MysqlInstanceDto> instances, MysqlTaskDto existing, IList<string> readyProviders)
        {
            Title = existing == null ? "添加 MySQL 任务" : "修改 MySQL 任务";
            Width = 460;
            Height = 580;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            _instances = instances ?? new List<MysqlInstanceDto>();
            _ready = readyProviders ?? new List<string>();
            _editId = existing == null ? 0 : existing.Id;
            _scope.Items.Add(new ComboBoxItem { Content = "全部用户库", Tag = "user" });
            _scope.Items.Add(new ComboBoxItem { Content = "指定库", Tag = "selected" });
            _scope.SelectedIndex = 0;
            _cycle.Items.Add(new ComboBoxItem { Content = "每天", Tag = "day" });
            _cycle.Items.Add(new ComboBoxItem { Content = "每小时", Tag = "hour" });
            _cycle.SelectedIndex = 0;
            foreach (var item in _instances) _instance.Items.Add(item);
            CloudPickerUi.Bind(_cloud, _cmbCloud, _ready);
            if (existing != null)
            {
                _name.Text = existing.Name;
                _dbs.Text = existing.SelectedDbs ?? "";
                _interval.Text = existing.CycleInterval.ToString();
                _enabled.IsChecked = existing.Enabled;
                _network.IsChecked = existing.DoNetwork;
                _ftp.IsChecked = existing.DoFtp;
                _cloud.IsChecked = existing.DoCloud && _ready.Count > 0;
                _email.IsChecked = existing.DoEmail;
                Select(_scope, existing.DbScope);
                Select(_cycle, existing.CycleKind);
                Select(_cmbCloud, existing.CloudProvider);
                for (var i = 0; i < _instance.Items.Count; i++)
                {
                    var inst = (MysqlInstanceDto)_instance.Items[i];
                    if (inst.Id == existing.InstanceId) _instance.SelectedIndex = i;
                }
            }
            else if (_instance.Items.Count > 0)
            {
                _instance.SelectedIndex = 0;
            }

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(Label("任务名称"));
            panel.Children.Add(_name);
            panel.Children.Add(Label("实例"));
            panel.Children.Add(_instance);
            panel.Children.Add(Label("范围"));
            panel.Children.Add(_scope);
            panel.Children.Add(Label("指定库，多个用逗号分隔"));
            panel.Children.Add(_dbs);
            panel.Children.Add(Label("周期"));
            panel.Children.Add(_cycle);
            panel.Children.Add(Label("间隔"));
            panel.Children.Add(_interval);
            panel.Children.Add(_enabled);
            var flags = new WrapPanel();
            flags.Children.Add(_network);
            flags.Children.Add(_ftp);
            flags.Children.Add(_email);
            panel.Children.Add(flags);
            panel.Children.Add(_cloud);
            panel.Children.Add(_cmbCloud);
            var ok = new Button { Content = "确定", Width = 80, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Left };
            ok.Click += delegate { Accept(); };
            panel.Children.Add(ok);
            Content = new ScrollViewer { Content = panel };
        }

        private void Accept()
        {
            int interval;
            if (!int.TryParse(_interval.Text, out interval))
            {
                MessageBox.Show(this, "间隔必须是数字。", "校验");
                return;
            }

            var inst = _instance.SelectedItem as MysqlInstanceDto;
            var task = new MysqlTaskDto
            {
                Id = _editId,
                Name = (_name.Text ?? "").Trim(),
                InstanceId = inst == null ? 0 : inst.Id,
                Enabled = _enabled.IsChecked == true,
                DbScope = TagOf(_scope, "user"),
                SelectedDbs = (_dbs.Text ?? "").Trim(),
                RetryMinutes = 5,
                CycleKind = TagOf(_cycle, "day"),
                CycleInterval = interval,
                NextRunUtc = DateTime.UtcNow,
                DoNetwork = _network.IsChecked == true,
                DoFtp = _ftp.IsChecked == true,
                DoCloud = _ready.Count > 0 && _cloud.IsChecked == true,
                CloudProvider = CloudChoice.Resolve(_ready, _cloud.IsChecked == true, TagOf(_cmbCloud, _ready.Count > 0 ? _ready[0] : "qiniu")),
                DoEmail = _email.IsChecked == true
            };
            var error = MysqlTaskStore.Validate(task);
            if (error != null)
            {
                MessageBox.Show(this, error, "校验");
                return;
            }

            Task = task;
            DialogResult = true;
        }

        private static void Select(ComboBox box, string tag)
        {
            for (var i = 0; i < box.Items.Count; i++)
            {
                var item = box.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(Convert.ToString(item.Tag), tag, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string TagOf(ComboBox box, string fallback)
        {
            var item = box.SelectedItem as ComboBoxItem;
            var tag = item == null ? null : Convert.ToString(item.Tag);
            return string.IsNullOrEmpty(tag) ? fallback : tag;
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(0, 0, 0, 4) };
        }
    }
}
