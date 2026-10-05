using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Yxy.Baidu;
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
        private readonly StackPanel _qiniuPanel;
        private readonly StackPanel _baiduPanel;
        private readonly CheckBox _enabled;
        private readonly TextBox _bucket;
        private readonly TextBox _ak;
        private readonly PasswordBox _sk;
        private readonly ComboBox _zone;
        private readonly TextBox _minKbps;
        private readonly TextBox _maxKbps;
        private readonly TextBox _parallel;
        private readonly TextBlock _masked;
        private readonly TextBlock _baiduStatus;
        private readonly Button _authBtn;
        private string _provider = CloudProviders.Qiniu;
        private int _authBusy;

        public CloudPage(HostBootstrap boot)
        {
            _boot = boot;
            var root = new DockPanel { Margin = new Thickness(8) };
            var title = new TextBlock
            {
                Text = "云凭据只在本页配置。百度用授权，七牛用空间密钥。任务里只勾选服务商。腾讯云 / 天翼云本期不可保存。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(title, Dock.Top);
            root.Children.Add(title);

            var split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _list = new ListBox { Margin = new Thickness(0, 0, 12, 0) };
            _list.Items.Add(new ListBoxItem { Content = "七牛云", Tag = CloudProviders.Qiniu });
            _list.Items.Add(new ListBoxItem { Content = "百度网盘", Tag = CloudProviders.Baidu });
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
            _parallel = new TextBox { Text = "1", Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
            _zone.Items.Add(new ComboBoxItem { Content = "华东 z0", Tag = "z0", IsSelected = true });
            _zone.Items.Add(new ComboBoxItem { Content = "华北 z1", Tag = "z1" });
            _zone.Items.Add(new ComboBoxItem { Content = "华南 z2", Tag = "z2" });
            _zone.Items.Add(new ComboBoxItem { Content = "北美 na0", Tag = "na0" });
            _zone.Items.Add(new ComboBoxItem { Content = "东南亚 as0", Tag = "as0" });
            _masked = new TextBlock { Margin = new Thickness(0, 4, 0, 8) };
            _baiduStatus = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            _authBtn = new Button { Content = "授权百度", Width = 96, Height = 28, HorizontalAlignment = HorizontalAlignment.Left };
            _authBtn.Click += Auth_OnClick;
            _qiniuPanel = new StackPanel();
            _qiniuPanel.Children.Add(Label("空间名"));
            _qiniuPanel.Children.Add(_bucket);
            _qiniuPanel.Children.Add(Label("AccessKey"));
            _qiniuPanel.Children.Add(_ak);
            _qiniuPanel.Children.Add(_masked);
            _qiniuPanel.Children.Add(Label("SecretKey（留空则保留已保存的）"));
            _qiniuPanel.Children.Add(_sk);
            _qiniuPanel.Children.Add(Label("区域"));
            _qiniuPanel.Children.Add(_zone);
            _baiduPanel = new StackPanel();
            _baiduPanel.Children.Add(_baiduStatus);
            _baiduPanel.Children.Add(_authBtn);
            _form.Children.Add(_enabled);
            _form.Children.Add(_qiniuPanel);
            _form.Children.Add(_baiduPanel);
            _form.Children.Add(Label("上传下限（KB/s，0 表示不设）"));
            _form.Children.Add(_minKbps);
            _form.Children.Add(Label("上传上限（KB/s，0 表示不限速）"));
            _form.Children.Add(_maxKbps);
            _form.Children.Add(Label("上传并行数（1–2）"));
            _form.Children.Add(_parallel);
            _form.Children.Add(new TextBlock
            {
                Text = "分片 1MB，可断点续传。并行默认 1。总速度不超过上限。上限为 0 时不限速，下限不生效。下限不能大于上限。",
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

        private bool IsQiniu()
        {
            return string.Equals(_provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsBaidu()
        {
            return string.Equals(_provider, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase);
        }

        private void List_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _list.SelectedItem as ListBoxItem;
            _provider = item != null && item.Tag != null ? item.Tag.ToString() : CloudProviders.Qiniu;
            var live = IsQiniu() || IsBaidu();
            _form.IsEnabled = live;
            _qiniuPanel.Visibility = IsQiniu() ? Visibility.Visible : Visibility.Collapsed;
            _baiduPanel.Visibility = IsBaidu() ? Visibility.Visible : Visibility.Collapsed;
            if (!live)
            {
                _hint.Text = "该服务商后续开通，不能保存、不能被任务选中。";
                return;
            }

            var settings = SettingsStore.Get(_boot.Db);
            _minKbps.Text = settings.UploadMinKbps.ToString();
            _maxKbps.Text = settings.UploadMaxKbps.ToString();
            _parallel.Text = (settings.UploadParallel < 1 ? 1 : settings.UploadParallel).ToString();
            if (IsQiniu())
            {
                _hint.Text = "填写你自己的七牛空间 / AK / SK。产品不提供统一空间。测试与上传走 HTTPS（TLS 1.2）。";
                var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Qiniu);
                _enabled.IsChecked = acc.Enabled;
                _bucket.Text = acc.Bucket ?? "";
                _ak.Text = acc.AccessKey ?? "";
                _sk.Password = "";
                _masked.Text = string.IsNullOrEmpty(acc.AccessKey) ? "尚未保存密钥。" : "已保存 AK：" + acc.MaskedAccessKey;
                SelectZone(acc.Zone);
                return;
            }

            _hint.Text = "点授权后，文件进入你自己的网盘「我的应用数据 / 用心云备份」。控制台回调须与本机一致；改完约 1 小时生效。等待时窗口不应卡住。";
            ShowBaiduStatus(CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu));
        }

        private void ShowBaiduStatus(CloudAccountDto acc)
        {
            _enabled.IsChecked = acc.Enabled;
            if (string.IsNullOrEmpty(acc.RefreshToken))
            {
                _baiduStatus.Text = "未授权";
                _authBtn.Content = "授权百度";
            }
            else
            {
                var name = string.IsNullOrEmpty(acc.DisplayName) ? "已授权" : CloudMask.Mask(acc.DisplayName);
                _baiduStatus.Text = "已授权账号 " + name;
                _authBtn.Content = "重新授权";
            }
        }

        private bool TryReadUploadOptions(out int minKbps, out int maxKbps, out int parallel)
        {
            minKbps = 0;
            maxKbps = 0;
            parallel = 1;
            if (!int.TryParse((_minKbps.Text ?? "").Trim(), out minKbps) || minKbps < 0
                || !int.TryParse((_maxKbps.Text ?? "").Trim(), out maxKbps) || maxKbps < 0)
            {
                MessageBox.Show(Window.GetWindow(this), "上传上下限请填大于等于 0 的整数，单位 KB/s。", "云备份");
                return false;
            }

            if (maxKbps > 0 && minKbps > maxKbps)
            {
                MessageBox.Show(Window.GetWindow(this), "上传下限不能大于上限。上限填 0 表示不限速。", "云备份");
                return false;
            }

            if (!int.TryParse((_parallel.Text ?? "").Trim(), out parallel) || parallel < 1 || parallel > 2)
            {
                MessageBox.Show(Window.GetWindow(this), "上传并行数请填 1 或 2。", "云备份");
                return false;
            }

            return true;
        }

        private void SaveUploadOptions(int minKbps, int maxKbps, int parallel)
        {
            var settings = SettingsStore.Get(_boot.Db);
            settings.UploadMinKbps = minKbps;
            settings.UploadMaxKbps = maxKbps;
            settings.UploadParallel = parallel;
            SettingsStore.Save(_boot.Db, settings);
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (!IsQiniu() && !IsBaidu())
            {
                MessageBox.Show(Window.GetWindow(this), "腾讯云 / 天翼云后续开通，不能保存。", "云备份");
                return;
            }

            int minKbps;
            int maxKbps;
            int parallel;
            if (!TryReadUploadOptions(out minKbps, out maxKbps, out parallel))
            {
                return;
            }

            if (IsQiniu())
            {
                var old = CloudAccountStore.Get(_boot.Db, CloudProviders.Qiniu);
                var acc = ReadQiniuForm(old);
                CloudAccountStore.Save(_boot.Db, acc);
                SaveUploadOptions(minKbps, maxKbps, parallel);
                _masked.Text = "已保存 AK：" + acc.MaskedAccessKey;
                MessageBox.Show(Window.GetWindow(this), "已保存。", "云备份");
                return;
            }

            var baidu = CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu);
            baidu.Provider = CloudProviders.Baidu;
            baidu.Enabled = _enabled.IsChecked == true;
            CloudAccountStore.Save(_boot.Db, baidu);
            SaveUploadOptions(minKbps, maxKbps, parallel);
            ShowBaiduStatus(baidu);
            MessageBox.Show(Window.GetWindow(this), "已保存。", "云备份");
        }

        private void Test_OnClick(object sender, RoutedEventArgs e)
        {
            if (!IsQiniu() && !IsBaidu())
            {
                MessageBox.Show(Window.GetWindow(this), "该服务商后续开通。", "云备份");
                return;
            }

            try
            {
                if (IsQiniu())
                {
                    var old = CloudAccountStore.Get(_boot.Db, CloudProviders.Qiniu);
                    var acc = ReadQiniuForm(old);
                    CloudAccountStore.Save(_boot.Db, acc);
                    CloudStorageFactory.Create(CloudProviders.Qiniu, acc).TestConnection();
                }
                else
                {
                    var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu);
                    acc.Enabled = _enabled.IsChecked == true;
                    CloudAccountStore.Save(_boot.Db, acc);
                    CloudStorageFactory.Create(CloudProviders.Baidu, acc).TestConnection();
                }

                MessageBox.Show(Window.GetWindow(this), "连接成功。", "云备份", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "云备份", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Auth_OnClick(object sender, RoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _authBusy, 1, 0) != 0)
            {
                MessageBox.Show(Window.GetWindow(this), "正在等待浏览器授权，请勿重复点击。最多约 3 分钟，或看授权页是否报错。", "云备份");
                return;
            }

            _authBtn.IsEnabled = false;
            _authBtn.Content = "等待授权…";
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaiduApp.json");
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception err = null;
                CloudAccountDto tokens = null;
                try
                {
                    var cfg = BaiduAppConfig.Load(jsonPath);
                    tokens = BaiduOAuth.CompleteInBrowser(cfg, 180000, null);
                }
                catch (Exception ex)
                {
                    err = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        if (err != null)
                        {
                            MessageBox.Show(Window.GetWindow(this), err.Message, "云备份", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu);
                        acc.Provider = CloudProviders.Baidu;
                        acc.Enabled = true;
                        acc.AccessToken = tokens.AccessToken;
                        acc.RefreshToken = tokens.RefreshToken;
                        acc.TokenExpiryUtc = tokens.TokenExpiryUtc;
                        acc.DisplayName = tokens.DisplayName;
                        CloudAccountStore.Save(_boot.Db, acc);
                        _enabled.IsChecked = true;
                        ShowBaiduStatus(acc);
                        MessageBox.Show(Window.GetWindow(this), "授权成功。", "云备份");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Window.GetWindow(this), ex.Message, "云备份", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _authBusy, 0);
                        _authBtn.IsEnabled = true;
                        var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu);
                        ShowBaiduStatus(acc);
                    }
                }));
            });
        }

        private CloudAccountDto ReadQiniuForm(CloudAccountDto old)
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
