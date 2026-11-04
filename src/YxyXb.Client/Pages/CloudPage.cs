using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using YxyXb.Baidu;
using YxyXb.Cloud;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Runtime;
using YxyXb.Core.Store;

namespace YxyXb.Client.Pages
{
    public sealed class CloudPage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly ListBox _list;
        private readonly TextBlock _hint;
        private readonly StackPanel _form;
        private readonly StackPanel _qiniuPanel;
        private readonly StackPanel _baiduPanel;
        private readonly StackPanel _objectPanel;
        private readonly TextBlock _objectBucketLabel;
        private readonly TextBlock _objectZoneLabel;
        private readonly ComboBox _objectRegion;
        private readonly TextBox _objectEndpoint;
        private readonly TextBox _objectBucket;
        private readonly TextBox _objectAk;
        private readonly PasswordBox _objectSk;
        private readonly TextBlock _objectMasked;
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
        private readonly Button _authQrBtn;
        private readonly Button _authPwdBtn;
        private readonly Button _authLogoutBtn;
        private string _provider = CloudProviders.Qiniu;
        private int _authBusy;

        public CloudPage(HostBootstrap boot)
        {
            _boot = boot;
            var root = new DockPanel { Margin = new Thickness(8) };
            var title = new TextBlock
            {
                Text = "云凭据只在本页配置。百度用授权，七牛用空间密钥，阿里云 / 腾讯云 / 天翼云用桶名和密钥。任务里只勾选服务商。",
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
            _list.Items.Add(new ListBoxItem { Content = "阿里云", Tag = CloudProviders.Aliyun });
            _list.Items.Add(new ListBoxItem { Content = "腾讯云", Tag = CloudProviders.Tencent });
            _list.Items.Add(new ListBoxItem { Content = "天翼云", Tag = CloudProviders.Ctyun });
            _list.Items.Add(new ListBoxItem { Content = "百度网盘", Tag = CloudProviders.Baidu });
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
            _authQrBtn = new Button { Content = "扫码授权", Width = 96, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            _authPwdBtn = new Button { Content = "帐号密码", Width = 96, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            _authLogoutBtn = new Button { Content = "注销", Width = 72, Height = 28, Visibility = Visibility.Collapsed };
            ApplyTooltips();
            _authQrBtn.Click += delegate { StartAuth(true); };
            _authPwdBtn.Click += delegate { StartAuth(false); };
            _authLogoutBtn.Click += LogoutBaidu_OnClick;
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
            var authBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            authBar.Children.Add(_authQrBtn);
            authBar.Children.Add(_authPwdBtn);
            authBar.Children.Add(_authLogoutBtn);
            _baiduPanel.Children.Add(authBar);
            _objectPanel = new StackPanel();
            _objectBucketLabel = Label("桶名");
            _objectZoneLabel = Label("地域");
            _objectRegion = new ComboBox();
            _objectEndpoint = new TextBox();
            _objectBucket = new TextBox();
            _objectAk = new TextBox();
            _objectSk = new PasswordBox();
            _objectMasked = new TextBlock { Margin = new Thickness(0, 4, 0, 8) };
            _objectPanel.Children.Add(_objectBucketLabel);
            _objectPanel.Children.Add(_objectBucket);
            _objectPanel.Children.Add(Label("AccessKey"));
            _objectPanel.Children.Add(_objectAk);
            _objectPanel.Children.Add(_objectMasked);
            _objectPanel.Children.Add(Label("SecretKey（留空则保留已保存的）"));
            _objectPanel.Children.Add(_objectSk);
            _objectPanel.Children.Add(_objectZoneLabel);
            _objectPanel.Children.Add(_objectRegion);
            _objectPanel.Children.Add(_objectEndpoint);
            _form.Children.Add(_enabled);
            _form.Children.Add(_qiniuPanel);
            _form.Children.Add(_baiduPanel);
            _form.Children.Add(_objectPanel);
            _form.Children.Add(Label("上行限速（KB/s，上传用，0 表示不限）"));
            _form.Children.Add(_minKbps);
            _form.Children.Add(Label("下行限速（KB/s，下载用，0 表示不限）"));
            _form.Children.Add(_maxKbps);
            _form.Children.Add(Label("上传并行数（1–2）"));
            _form.Children.Add(_parallel);
            _form.Children.Add(new TextBlock
            {
                Text = "分片 4MB（百度接口要求），可断点续传。并行默认 1。手工执行和引擎自动执行都按这里的上行/下行限速。上行限制云/FTP/共享上传；下行限制 FTP 源下载。填 0 表示该方向不限速。",
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

        private void ApplyTooltips()
        {
            UiHelp.Tip(_enabled, "启用后，任务列表才会允许选择该云服务商。");
            UiHelp.Tip(_bucket, "七牛云空间名。必须已在当前账号中创建。");
            UiHelp.Tip(_ak, "七牛云 AccessKey。凭据只保存在本机。");
            UiHelp.Tip(_sk, "七牛云 SecretKey。留空保存表示沿用已有密钥。");
            UiHelp.Tip(_zone, "七牛空间所在区域，选错会导致上传地址不正确。");
            UiHelp.Tip(_minKbps, "上行限速，单位 KB/s。0 表示不限；会影响云、FTP 和共享上传。");
            UiHelp.Tip(_maxKbps, "下行限速，单位 KB/s。0 表示不限；会影响 FTP 源下载。");
            UiHelp.Tip(_parallel, "云上传并行分片数，可填 1 或 2。建议保持 1 以降低断线风险。");
            UiHelp.Tip(_objectBucket, "对象存储桶名。腾讯云桶名通常包含 APPID。");
            UiHelp.Tip(_objectAk, "对象存储 AccessKey 或 SecretId。凭据只保存在本机。");
            UiHelp.Tip(_objectSk, "对象存储 SecretKey。留空保存表示沿用已有密钥。");
            UiHelp.Tip(_objectRegion, "选择桶所在地域；选择自定义 Endpoint 时可手动填写地址。");
            UiHelp.Tip(_objectEndpoint, "服务商控制台提供的公网 Endpoint；天翼云必须填写。");
            UiHelp.Tip(_authQrBtn, "打开百度授权窗口并使用二维码登录。");
            UiHelp.Tip(_authPwdBtn, "打开百度授权窗口并使用帐号和密码登录。");
            UiHelp.Tip(_authLogoutBtn, "清除本机保存的百度授权，以便更换账号。");
        }

        private bool IsQiniu()
        {
            return string.Equals(_provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsBaidu()
        {
            return string.Equals(_provider, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsObject()
        {
            return string.Equals(_provider, CloudProviders.Aliyun, StringComparison.OrdinalIgnoreCase)
                || string.Equals(_provider, CloudProviders.Tencent, StringComparison.OrdinalIgnoreCase)
                || string.Equals(_provider, CloudProviders.Ctyun, StringComparison.OrdinalIgnoreCase);
        }

        private void List_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _list.SelectedItem as ListBoxItem;
            _provider = item != null && item.Tag != null ? item.Tag.ToString() : CloudProviders.Qiniu;
            var live = IsQiniu() || IsBaidu() || IsObject();
            _form.IsEnabled = live;
            _qiniuPanel.Visibility = IsQiniu() ? Visibility.Visible : Visibility.Collapsed;
            _baiduPanel.Visibility = IsBaidu() ? Visibility.Visible : Visibility.Collapsed;
            _objectPanel.Visibility = IsObject() ? Visibility.Visible : Visibility.Collapsed;
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

            if (IsBaidu())
            {
                _hint.Text = "扫码或帐号密码二选一。点对应按钮后打开授权窗，成功会自动关闭。已授权时可点「注销」再换其它百度账号。文件进入「我的应用数据 / 用心云循备」。控制台回调须与本机一致；改完约 1 小时生效。";
                ShowBaiduStatus(CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu));
                return;
            }

            ShowObjectForm();
        }

        private void ShowBaiduStatus(CloudAccountDto acc)
        {
            _enabled.IsChecked = acc.Enabled;
            var authed = !string.IsNullOrEmpty(acc.RefreshToken);
            _authLogoutBtn.Visibility = authed ? Visibility.Visible : Visibility.Collapsed;
            if (!authed)
            {
                _baiduStatus.Text = "未授权。可扫码或帐号密码。";
            }
            else
            {
                var name = string.IsNullOrEmpty(acc.DisplayName) ? "已授权" : CloudMask.Mask(acc.DisplayName);
                _baiduStatus.Text = "已授权账号 " + name + "。可注销后换号，或再扫码/帐号密码重新授权。";
            }
        }

        private void ShowObjectForm()
        {
            var acc = CloudAccountStore.Get(_boot.Db, _provider);
            _enabled.IsChecked = acc.Enabled;
            _objectBucket.Text = acc.Bucket ?? "";
            _objectAk.Text = acc.AccessKey ?? "";
            _objectSk.Password = "";
            _objectMasked.Text = string.IsNullOrEmpty(acc.AccessKey) ? "尚未保存密钥。" : "已保存 AK：" + acc.MaskedAccessKey;
            _objectRegion.Items.Clear();
            _objectEndpoint.Visibility = Visibility.Collapsed;
            _objectRegion.Visibility = Visibility.Visible;

            if (string.Equals(_provider, CloudProviders.Aliyun, StringComparison.OrdinalIgnoreCase))
            {
                _hint.Text = "填写你自己的阿里云桶名 / AccessKey / SecretKey。产品不提供统一桶。测试只访问所填的这一个桶。请关闭桶的版本控制。";
                _objectBucketLabel.Text = "桶名";
                _objectZoneLabel.Text = "地域";
                FillRegions(CloudRegions.Aliyun, acc.Zone);
            }
            else if (string.Equals(_provider, CloudProviders.Tencent, StringComparison.OrdinalIgnoreCase))
            {
                _hint.Text = "填写你自己的腾讯云桶名（带 APPID，如 backup-1250000000）/ SecretId / SecretKey。产品不提供统一桶。测试只访问所填的这一个桶。请关闭桶的版本控制。";
                _objectBucketLabel.Text = "桶名（带 APPID）";
                _objectZoneLabel.Text = "地域";
                FillRegions(CloudRegions.Tencent, acc.Zone);
            }
            else
            {
                _hint.Text = "填写你自己的天翼云桶名 / AccessKey / SecretKey。产品不提供统一桶。测试只访问所填的这一个桶。请关闭桶的版本控制。Endpoint 从控制台复制（如 jiangsu-10.zos.ctyun.cn）。";
                _objectBucketLabel.Text = "桶名";
                _objectZoneLabel.Text = "Endpoint";
                _objectRegion.Visibility = Visibility.Collapsed;
                _objectEndpoint.Visibility = Visibility.Visible;
                _objectEndpoint.Text = acc.Zone ?? "";
            }
        }

        private void FillRegions(CloudRegion[] regions, string current)
        {
            var selected = 0;
            for (var i = 0; i < regions.Length; i++)
            {
                _objectRegion.Items.Add(new ComboBoxItem { Content = regions[i].Label, Tag = regions[i].Id });
                if (!string.IsNullOrEmpty(current) && string.Equals(regions[i].Id, current, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i;
                }
            }

            if (!string.IsNullOrEmpty(current) && selected == 0)
            {
                var found = false;
                for (var i = 0; i < regions.Length; i++)
                {
                    if (string.Equals(regions[i].Id, current, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && regions.Length > 0 && regions[regions.Length - 1].Id == "")
                {
                    selected = regions.Length - 1;
                }
            }

            _objectRegion.SelectedIndex = selected;
            var isCustom = selected == regions.Length - 1 && regions[selected].Id == "";
            _objectEndpoint.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            _objectRegion.SelectionChanged += delegate
            {
                var item = _objectRegion.SelectedItem as ComboBoxItem;
                var custom = item != null && item.Tag != null && item.Tag.ToString() == "";
                _objectEndpoint.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            };
            if (isCustom)
            {
                _objectEndpoint.Text = current;
            }
        }

        private string ObjectZone()
        {
            if (string.Equals(_provider, CloudProviders.Ctyun, StringComparison.OrdinalIgnoreCase))
            {
                return CloudEndpoints.Host(_objectEndpoint.Text);
            }

            var item = _objectRegion.SelectedItem as ComboBoxItem;
            var id = item != null && item.Tag != null ? item.Tag.ToString() : "";
            if (id == "")
            {
                return CloudEndpoints.Host(_objectEndpoint.Text);
            }

            return id;
        }

        private CloudAccountDto ReadObjectForm(CloudAccountDto old)
        {
            var sk = _objectSk.Password;
            if (string.IsNullOrEmpty(sk) && old != null)
            {
                sk = old.SecretKey;
            }

            return new CloudAccountDto
            {
                Provider = _provider,
                Enabled = _enabled.IsChecked == true,
                Bucket = (_objectBucket.Text ?? "").Trim(),
                AccessKey = (_objectAk.Text ?? "").Trim(),
                SecretKey = sk,
                Zone = ObjectZone()
            };
        }

        private void LogoutBaidu_OnClick(object sender, RoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _authBusy, 1, 0) != 0)
            {
                MessageBox.Show(Window.GetWindow(this), "正在授权，请稍后再注销。", "云备份");
                return;
            }

            try
            {
                var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu);
                if (string.IsNullOrEmpty(acc.RefreshToken))
                {
                    ShowBaiduStatus(acc);
                    return;
                }

                if (MessageBox.Show(
                    Window.GetWindow(this),
                    "确定注销当前百度账号？注销后需重新扫码或帐号密码授权，才能再上传。",
                    "注销百度账号",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                CloudAccountStore.ClearBaidu(_boot.Db);
                _enabled.IsChecked = false;
                ShowBaiduStatus(CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu));
                MessageBox.Show(Window.GetWindow(this), "已注销。可重新扫码或帐号密码登录其它百度账号。", "云备份");
            }
            finally
            {
                Interlocked.Exchange(ref _authBusy, 0);
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
                MessageBox.Show(Window.GetWindow(this), "上行、下行请填大于等于 0 的整数，单位 KB/s。", "云备份");
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
            if (!IsQiniu() && !IsBaidu() && !IsObject())
            {
                MessageBox.Show(Window.GetWindow(this), "该服务商后续开通，不能保存。", "云备份");
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

            if (IsObject())
            {
                var oldObj = CloudAccountStore.Get(_boot.Db, _provider);
                var obj = ReadObjectForm(oldObj);
                if (string.IsNullOrEmpty(obj.Zone))
                {
                    MessageBox.Show(Window.GetWindow(this), "请填写地域或 Endpoint。", "云备份");
                    return;
                }

                CloudAccountStore.Save(_boot.Db, obj);
                SaveUploadOptions(minKbps, maxKbps, parallel);
                _objectMasked.Text = "已保存 AK：" + obj.MaskedAccessKey;
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
            if (!IsQiniu() && !IsBaidu() && !IsObject())
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
                else if (IsObject())
                {
                    var oldObj = CloudAccountStore.Get(_boot.Db, _provider);
                    var obj = ReadObjectForm(oldObj);
                    if (string.IsNullOrEmpty(obj.Zone))
                    {
                        MessageBox.Show(Window.GetWindow(this), "请填写地域或 Endpoint。", "云备份");
                        return;
                    }

                    CloudAccountStore.Save(_boot.Db, obj);
                    CloudStorageFactory.Create(_provider, obj).TestConnection();
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

        private void StartAuth(bool useQrcode)
        {
            if (Interlocked.CompareExchange(ref _authBusy, 1, 0) != 0)
            {
                MessageBox.Show(Window.GetWindow(this), "正在等待浏览器授权，请勿重复点击。最多约 3 分钟，或看授权页是否报错。", "云备份");
                return;
            }

            SetAuthEnabled(false);
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaiduApp.json");
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception err = null;
                CloudAccountDto tokens = null;
                try
                {
                    var cfg = BaiduAppConfig.Load(jsonPath);
                    tokens = BaiduOAuth.CompleteInBrowser(cfg, 180000, null, useQrcode);
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
                        SetAuthEnabled(true);
                        var acc = CloudAccountStore.Get(_boot.Db, CloudProviders.Baidu);
                        ShowBaiduStatus(acc);
                    }
                }));
            });
        }

        private void SetAuthEnabled(bool enabled)
        {
            _authQrBtn.IsEnabled = enabled;
            _authPwdBtn.IsEnabled = enabled;
            _authLogoutBtn.IsEnabled = enabled;
            _authQrBtn.Content = enabled ? "扫码授权" : "等待授权…";
            _authPwdBtn.Content = enabled ? "帐号密码" : "等待授权…";
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
