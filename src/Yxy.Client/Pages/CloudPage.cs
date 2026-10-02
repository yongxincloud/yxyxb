using System;
using System.Windows;
using System.Windows.Controls;
using Yxy.Cloud;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core.Runtime;
using Yxy.Core.Store;

namespace Yxy.Client.Pages
{
    public sealed class CloudPage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly ListBox _list;
        private readonly TextBlock _hint;
        private readonly StackPanel _form;
        private readonly CheckBox _enabled;
        private readonly TextBox _bucket;
        private readonly TextBox _ak;
        private readonly PasswordBox _sk;
        private readonly ComboBox _zone;
        private readonly TextBox _minKbps;
        private readonly TextBox _maxKbps;
        private readonly TextBlock _masked;
        private string _provider = CloudProviders.Qiniu;

        public CloudPage(HostBootstrap boot)
        {
            _boot = boot;
            var root = new DockPanel { Margin = new Thickness(8) };
            var title = new TextBlock
            {
                Text = "云凭据只在本页配置。任务里只勾选服务商，不写 AK/SK。腾讯云 / 天翼云本期不可保存。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(title, Dock.Top);
            root.Children.Add(title);

            var split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _list = new ListBox { Margin = new Thickness(0, 0, 12, 0) };
            _list.Items.Add(new ListBoxItem { Content = "七牛云（本期可用）", Tag = CloudProviders.Qiniu });
            _list.Items.Add(new ListBoxItem { Content = "腾讯云（后续）", Tag = CloudProviders.Tencent });
            _list.Items.Add(new ListBoxItem { Content = "天翼云（后续）", Tag = CloudProviders.Ctyun });
            _list.SelectionChanged += List_OnChanged;
            Grid.SetColumn(_list, 0);
            split.Children.Add(_list);

            var right = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
            _hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            right.Children.Add(_hint);
            _form = new StackPanel();
            _enabled = new CheckBox { Content = "启用", Margin = new Thickness(0, 0, 0, 8) };
            _bucket = new TextBox();
            _ak = new TextBox();
            _sk = new PasswordBox();
            _zone = new ComboBox();
            _minKbps = new TextBox { Text = "0" };
            _maxKbps = new TextBox { Text = "0" };
            _zone.Items.Add(new ComboBoxItem { Content = "华东 z0", Tag = "z0", IsSelected = true });
            _zone.Items.Add(new ComboBoxItem { Content = "华北 z1", Tag = "z1" });
            _zone.Items.Add(new ComboBoxItem { Content = "华南 z2", Tag = "z2" });
            _zone.Items.Add(new ComboBoxItem { Content = "北美 na0", Tag = "na0" });
            _zone.Items.Add(new ComboBoxItem { Content = "东南亚 as0", Tag = "as0" });
            _masked = new TextBlock { Margin = new Thickness(0, 4, 0, 8) };
            _form.Children.Add(_enabled);
            _form.Children.Add(Label("空间名"));
            _form.Children.Add(_bucket);
            _form.Children.Add(Label("AccessKey"));
            _form.Children.Add(_ak);
            _form.Children.Add(_masked);
            _form.Children.Add(Label("SecretKey（留空则保留已保存的）"));
            _form.Children.Add(_sk);
            _form.Children.Add(Label("区域"));
            _form.Children.Add(_zone);
            _form.Children.Add(Label("上传下限（KB/s，0 表示不设）"));
            _form.Children.Add(_minKbps);
            _form.Children.Add(Label("上传上限（KB/s，0 表示不限速）"));
            _form.Children.Add(_maxKbps);
            _form.Children.Add(new TextBlock
            {
                Text = "分片 4MB，同时 2 路。总速度不超过上限。上限为 0 时不限速，下限不生效。下限不能大于上限。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var test = new Button { Content = "测试连接", Width = 88, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = "保存", Width = 80, Height = 28 };
            test.Click += Test_OnClick;
            save.Click += Save_OnClick;
            bar.Children.Add(test);
            bar.Children.Add(save);
            _form.Children.Add(bar);
            right.Children.Add(_form);
            Grid.SetColumn(right, 1);
            split.Children.Add(right);
            root.Children.Add(split);
            Content = root;
            _list.SelectedIndex = 0;
        }

        private void List_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _list.SelectedItem as ListBoxItem;
            _provider = item != null && item.Tag != null ? item.Tag.ToString() : CloudProviders.Qiniu;
            var live = string.Equals(_provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase);
            _form.IsEnabled = live;
            if (!live)
            {
                _hint.Text = "该服务商后续开通，不能保存、不能被任务选中。";
                return;
            }

            _hint.Text = "填写你自己的七牛空间 / AK / SK。产品不提供统一空间。测试与上传走 HTTPS（TLS 1.2）。";
            var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Qiniu);
            _enabled.IsChecked = acc.Enabled;
            _bucket.Text = acc.Bucket ?? "";
            _ak.Text = acc.AccessKey ?? "";
            _sk.Password = "";
            _masked.Text = string.IsNullOrEmpty(acc.AccessKey) ? "尚未保存密钥。" : "已保存 AK：" + acc.MaskedAccessKey;
            SelectZone(acc.Zone);
            var settings = SettingsStore.Get(_boot.Db);
            _minKbps.Text = settings.UploadMinKbps.ToString();
            _maxKbps.Text = settings.UploadMaxKbps.ToString();
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(_provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(Window.GetWindow(this), "腾讯云 / 天翼云后续开通，不能保存。", "云备份");
                return;
            }

            int minKbps;
            int maxKbps;
            if (!int.TryParse((_minKbps.Text ?? "").Trim(), out minKbps) || minKbps < 0
                || !int.TryParse((_maxKbps.Text ?? "").Trim(), out maxKbps) || maxKbps < 0)
            {
                MessageBox.Show(Window.GetWindow(this), "上传上下限请填大于等于 0 的整数，单位 KB/s。", "云备份");
                return;
            }

            if (maxKbps > 0 && minKbps > maxKbps)
            {
                MessageBox.Show(Window.GetWindow(this), "上传下限不能大于上限。上限填 0 表示不限速。", "云备份");
                return;
            }

            var old = CloudAccountStore.Get(_boot.Db, CloudProviders.Qiniu);
            var acc = ReadForm(old);
            CloudAccountStore.Save(_boot.Db, acc);
            var settings = SettingsStore.Get(_boot.Db);
            settings.UploadMinKbps = minKbps;
            settings.UploadMaxKbps = maxKbps;
            SettingsStore.Save(_boot.Db, settings);
            _masked.Text = "已保存 AK：" + acc.MaskedAccessKey;
            MessageBox.Show(Window.GetWindow(this), "已保存。", "云备份");
        }

        private void Test_OnClick(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(_provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(Window.GetWindow(this), "该服务商后续开通。", "云备份");
                return;
            }

            try
            {
                var old = CloudAccountStore.Get(_boot.Db, CloudProviders.Qiniu);
                var acc = ReadForm(old);
                CloudAccountStore.Save(_boot.Db, acc);
                var storage = CloudStorageFactory.Create(CloudProviders.Qiniu, acc);
                storage.TestConnection();
                MessageBox.Show(Window.GetWindow(this), "连接成功。", "云备份", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "云备份", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private CloudAccountDto ReadForm(CloudAccountDto old)
        {
            var sk = _sk.Password;
            if (string.IsNullOrEmpty(sk) && old != null)
            {
                sk = old.SecretKey;
            }

            return new CloudAccountDto
            {
                Provider = CloudProviders.Qiniu,
                Enabled = _enabled.IsChecked == true,
                Bucket = (_bucket.Text ?? "").Trim(),
                AccessKey = (_ak.Text ?? "").Trim(),
                SecretKey = sk,
                Zone = TagOf(_zone, "z0")
            };
        }

        private void SelectZone(string zone)
        {
            var want = string.IsNullOrEmpty(zone) ? "z0" : zone;
            for (var i = 0; i < _zone.Items.Count; i++)
            {
                var item = _zone.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null && string.Equals(item.Tag.ToString(), want, StringComparison.OrdinalIgnoreCase))
                {
                    _zone.SelectedIndex = i;
                    return;
                }
            }

            _zone.SelectedIndex = 0;
        }

        private static string TagOf(ComboBox box, string fallback)
        {
            var item = box.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null)
            {
                return fallback;
            }

            return item.Tag.ToString();
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 4) };
        }
    }
}
