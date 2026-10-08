using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Yxy.Client.Dialogs;
using System.Windows.Media;
using Yxy.Contracts.Dto;
using Yxy.Core;
using Yxy.Core.Backup;
using Yxy.Core.Runtime;
using Yxy.Core.Store;
using Yxy.Core.Transfer;

namespace Yxy.Client.Pages
{
    public sealed class SettingsPage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly TextBox _bak;
        private readonly TextBox _zipLevel;
        private readonly PasswordBox _zipPwd;
        private readonly TextBlock _preview;
        private readonly TextBox _softwareId;
        private readonly CheckBox _startWin;
        private readonly CheckBox _tray;
        private readonly PasswordBox _lockPwd;
        private readonly TextBox _logRetain;
        private readonly ComboBox _theme;
        private readonly TextBox _uncPath;
        private readonly TextBox _uncDomain;
        private readonly TextBox _uncUser;
        private readonly PasswordBox _uncPwd;
        private readonly TextBox _ftpHost;
        private readonly TextBox _ftpPort;
        private readonly TextBox _ftpUser;
        private readonly PasswordBox _ftpPwd;
        private readonly TextBox _ftpDir;
        private readonly CheckBox _ftpPassive;
        private readonly TextBox _smtpHost;
        private readonly TextBox _smtpPort;
        private readonly CheckBox _smtpSsl;
        private readonly TextBox _smtpUser;
        private readonly PasswordBox _smtpPwd;
        private readonly TextBox _smtpFrom;
        private readonly TextBox _smtpTo;
        private readonly TextBox _smtpSubject;
        private readonly TextBox _smtpBody;
        private readonly TextBox _smtpFooter;
        private readonly CheckBox _sendAfterTask;
        private readonly ComboBox _sendWhen;
        private readonly TextBlock _sendWhenLabel;
        private readonly CheckBox _nameFileSid;
        private readonly CheckBox _nameFileTask;
        private readonly CheckBox _nameFileTime;
        private readonly CheckBox _nameFileDb;
        private readonly CheckBox _nameDirSid;
        private readonly CheckBox _nameDirKind;
        private readonly CheckBox _nameDirInstance;
        private readonly CheckBox _nameDirTask;
        private readonly CheckBox _purgeLocal;
        private readonly CheckBox _purgeNetwork;
        private readonly CheckBox _purgeFtp;
        private readonly CheckBox _purgeQiniu;
        private readonly CheckBox _purgeBaidu;
        private readonly TextBox _retainLocal;
        private readonly TextBox _retainNetwork;
        private readonly TextBox _retainFtp;
        private readonly TextBox _retainQiniu;
        private readonly TextBox _retainBaidu;
        private readonly TimePicker _purgeTime;

        public SettingsPage(HostBootstrap boot)
        {
            _boot = boot;
            var tabs = new TabControl();
            _bak = new TextBox();
            _zipLevel = new TextBox { Text = "5" };
            _zipPwd = new PasswordBox();
            _preview = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            _softwareId = new TextBox();
            _startWin = new CheckBox { Content = "开机启动" };
            _tray = new CheckBox { Content = "关闭窗口到托盘" };
            _lockPwd = new PasswordBox();
            _logRetain = new TextBox { Text = "30", Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
            _theme = new ComboBox();
            _theme.Items.Add(new ComboBoxItem { Content = "明亮", Tag = "light", IsSelected = true });
            _theme.Items.Add(new ComboBoxItem { Content = "深色", Tag = "dark" });
            _theme.Items.Add(new ComboBoxItem { Content = "Windows 11", Tag = "win11" });
            _theme.Items.Add(new ComboBoxItem { Content = "Mac", Tag = "mac" });
            _theme.Items.Add(new ComboBoxItem { Content = "用心云", Tag = "yongxin" });
            _uncPath = new TextBox();
            _uncDomain = new TextBox();
            _uncUser = new TextBox();
            _uncPwd = new PasswordBox();
            _ftpHost = new TextBox();
            _ftpPort = new TextBox { Text = "21" };
            _ftpUser = new TextBox();
            _ftpPwd = new PasswordBox();
            _ftpDir = new TextBox { Text = "/" };
            _ftpPassive = new CheckBox { Content = "被动模式", IsChecked = true };
            _smtpHost = new TextBox();
            _smtpPort = new TextBox { Text = "25" };
            _smtpSsl = new CheckBox { Content = "SSL/TLS" };
            _smtpUser = new TextBox();
            _smtpPwd = new PasswordBox();
            _smtpFrom = new TextBox();
            _smtpTo = new TextBox();
            _smtpSubject = new TextBox { Text = MailUtil.DefaultSubject };
            _smtpBody = new TextBox
            {
                Text = MailUtil.DefaultBody,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 96
            };
            _smtpFooter = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 64
            };
            _sendAfterTask = new CheckBox { Content = "任务执行后发送邮件", Margin = new Thickness(0, 12, 0, 8) };
            _sendWhenLabel = Label("发送条件");
            _sendWhen = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            _sendWhen.Items.Add(new ComboBoxItem { Content = "失败", Tag = "fail" });
            _sendWhen.Items.Add(new ComboBoxItem { Content = "成功", Tag = "success" });
            _sendWhen.Items.Add(new ComboBoxItem { Content = "所有", Tag = "all" });
            _sendWhen.SelectedIndex = 0;
            _sendAfterTask.Checked += delegate { SyncMailPolicyUi(); };
            _sendAfterTask.Unchecked += delegate { SyncMailPolicyUi(); };
            _smtpHost.LostFocus += delegate { SyncMailPolicyUi(); };
            _smtpFrom.LostFocus += delegate { SyncMailPolicyUi(); };
            _smtpTo.LostFocus += delegate { SyncMailPolicyUi(); };
            _purgeLocal = new CheckBox { Content = "删本机 bak 过期 zip" };
            _purgeNetwork = new CheckBox { Content = "删网络共享过期 zip" };
            _purgeFtp = new CheckBox { Content = "删 FTP 过期 zip" };
            _purgeQiniu = new CheckBox { Content = "删七牛本产品过期对象" };
            _purgeBaidu = new CheckBox { Content = "删百度网盘本产品过期对象" };
            _retainLocal = new TextBox { Text = "7" };
            _retainNetwork = new TextBox { Text = "7" };
            _retainFtp = new TextBox { Text = "7" };
            _retainQiniu = new TextBox { Text = "7" };
            _retainBaidu = new TextBox { Text = "7" };
            _purgeTime = DateTimePick.TimeBox();
            _nameFileSid = NameCheck("客户标识");
            _nameFileTask = NameCheck("任务名");
            _nameFileTime = NameCheck("时间");
            _nameFileDb = NameCheck("库名（仅数据库任务）");
            _nameDirSid = NameCheck("客户标识");
            _nameDirKind = NameCheck("备份类型（文件备份 / 库备份）");
            _nameDirInstance = NameCheck("实例名（仅数据库任务）");
            _nameDirTask = NameCheck("任务名");

            tabs.Items.Add(new TabItem { Header = "备份存储", Content = StorageTab() });
            tabs.Items.Add(new TabItem { Header = "备份命名", Content = NamingTab() });
            tabs.Items.Add(new TabItem { Header = "网络传输", Content = UncTab() });
            tabs.Items.Add(new TabItem { Header = "FTP", Content = FtpTab() });
            tabs.Items.Add(new TabItem { Header = "EMAIL", Content = MailTab() });
            tabs.Items.Add(new TabItem { Header = "自动删除", Content = PurgeTab() });
            tabs.Items.Add(new TabItem { Header = "综合设置", Content = GeneralTab() });
            Content = tabs;
            Loaded += delegate { Load(); };
        }

        private UIElement StorageTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(Label("备份目录"));
            var row = new DockPanel();
            var browse = new Button { Content = "选择", Width = 72, Margin = new Thickness(8, 0, 0, 0) };
            DockPanel.SetDock(browse, Dock.Right);
            browse.Click += delegate
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _bak.Text = dialog.SelectedPath;
                        RefreshPreview();
                    }
                }
            };
            row.Children.Add(browse);
            row.Children.Add(_bak);
            panel.Children.Add(row);
            panel.Children.Add(Label("压缩等级 0-9（默认 5）"));
            panel.Children.Add(_zipLevel);
            panel.Children.Add(Label("压缩密码（可空；不进日志）"));
            panel.Children.Add(_zipPwd);
            panel.Children.Add(SaveButton(SaveStorage));
            return panel;
        }

        private UIElement NamingTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock
            {
                Text = "勾选要写进新任务备份名的部分。扩展名固定 .zip。预览随勾选变化，保存后新任务按此落盘。",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(Label("文件名"));
            panel.Children.Add(_nameFileSid);
            panel.Children.Add(_nameFileTask);
            panel.Children.Add(_nameFileTime);
            panel.Children.Add(_nameFileDb);
            panel.Children.Add(Label("目录"));
            panel.Children.Add(_nameDirSid);
            panel.Children.Add(_nameDirKind);
            panel.Children.Add(_nameDirInstance);
            panel.Children.Add(_nameDirTask);
            panel.Children.Add(_preview);
            panel.Children.Add(SaveButton(SaveNaming));
            return panel;
        }

        private UIElement UncTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(Label("共享路径"));
            panel.Children.Add(_uncPath);
            panel.Children.Add(Label("域（可空）"));
            panel.Children.Add(_uncDomain);
            panel.Children.Add(Label("用户"));
            panel.Children.Add(_uncUser);
            panel.Children.Add(Label("密码"));
            panel.Children.Add(_uncPwd);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var test = new Button { Content = "测试", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = "保存", Width = 80, Height = 28 };
            test.Click += delegate
            {
                try
                {
                    SaveUnc();
                    UncShare.Test(ReadUnc());
                    MessageBox.Show(Window.GetWindow(this), "连接成功。", "网络传输", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Window.GetWindow(this), ex.Message, "网络传输", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            save.Click += delegate { SaveUnc(); MessageBox.Show(Window.GetWindow(this), "已保存。", "网络传输"); };
            bar.Children.Add(test);
            bar.Children.Add(save);
            panel.Children.Add(bar);
            return panel;
        }

        private UIElement FtpTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(Label("主机"));
            panel.Children.Add(_ftpHost);
            panel.Children.Add(Label("端口"));
            panel.Children.Add(_ftpPort);
            panel.Children.Add(Label("用户"));
            panel.Children.Add(_ftpUser);
            panel.Children.Add(Label("密码"));
            panel.Children.Add(_ftpPwd);
            panel.Children.Add(Label("远程目录"));
            panel.Children.Add(_ftpDir);
            panel.Children.Add(_ftpPassive);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var test = new Button { Content = "测试", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = "保存", Width = 80, Height = 28 };
            test.Click += delegate
            {
                try
                {
                    SaveFtp();
                    FtpUtil.Test(ReadFtp());
                    MessageBox.Show(Window.GetWindow(this), "连接成功。", "FTP", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Window.GetWindow(this), ex.Message, "FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            save.Click += delegate { SaveFtp(); MessageBox.Show(Window.GetWindow(this), "已保存。", "FTP"); };
            bar.Children.Add(test);
            bar.Children.Add(save);
            panel.Children.Add(bar);
            return panel;
        }

        private UIElement MailTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(Label("SMTP 主机"));
            panel.Children.Add(_smtpHost);
            panel.Children.Add(Label("端口"));
            panel.Children.Add(_smtpPort);
            panel.Children.Add(_smtpSsl);
            panel.Children.Add(Label("用户"));
            panel.Children.Add(_smtpUser);
            panel.Children.Add(Label("密码"));
            panel.Children.Add(_smtpPwd);
            panel.Children.Add(Label("发件人"));
            panel.Children.Add(_smtpFrom);
            panel.Children.Add(Label("收件人"));
            panel.Children.Add(_smtpTo);
            panel.Children.Add(_sendAfterTask);
            panel.Children.Add(_sendWhenLabel);
            panel.Children.Add(_sendWhen);
            panel.Children.Add(Label("邮件标题"));
            panel.Children.Add(_smtpSubject);
            panel.Children.Add(Label("邮件内容"));
            panel.Children.Add(_smtpBody);
            panel.Children.Add(Label("邮件结尾"));
            panel.Children.Add(_smtpFooter);
            panel.Children.Add(new TextBlock
            {
                Text = "可用 {任务}、{结果}、{时间}、{详情}。标题或内容留空时用默认句式。结尾写在正文最后，留空则不附加。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var test = new Button { Content = "测试发送", Width = 88, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = "保存", Width = 80, Height = 28 };
            test.Click += delegate
            {
                try
                {
                    SaveSmtp();
                    MailUtil.Test(ReadSmtp());
                    MessageBox.Show(Window.GetWindow(this), "已发送。", "邮件", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Window.GetWindow(this), ex.Message, "邮件", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            save.Click += delegate { SaveSmtp(); MessageBox.Show(Window.GetWindow(this), "已保存。", "邮件"); };
            bar.Children.Add(test);
            bar.Children.Add(save);
            panel.Children.Add(bar);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        }

        private UIElement PurgeTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock
            {
                Text = "每天到点执行一次。只删备份包，不删任务、不删离线盘。未勾选的通道不删。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(Label("执行时间"));
            panel.Children.Add(_purgeTime);
            panel.Children.Add(_purgeLocal);
            panel.Children.Add(Label("本机保留天数"));
            panel.Children.Add(_retainLocal);
            panel.Children.Add(_purgeNetwork);
            panel.Children.Add(Label("网络保留天数"));
            panel.Children.Add(_retainNetwork);
            panel.Children.Add(_purgeFtp);
            panel.Children.Add(Label("FTP 保留天数"));
            panel.Children.Add(_retainFtp);
            panel.Children.Add(_purgeQiniu);
            panel.Children.Add(Label("七牛保留天数"));
            panel.Children.Add(_retainQiniu);
            panel.Children.Add(_purgeBaidu);
            panel.Children.Add(Label("百度保留天数"));
            panel.Children.Add(_retainBaidu);
            panel.Children.Add(SaveButton(SavePurge));
            return panel;
        }

        private UIElement GeneralTab()
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(Label("界面主题"));
            panel.Children.Add(_theme);
            panel.Children.Add(Label("客户标识"));
            panel.Children.Add(_softwareId);
            panel.Children.Add(_startWin);
            panel.Children.Add(_tray);
            panel.Children.Add(Label("本机锁定密码（留空则不锁定）"));
            panel.Children.Add(_lockPwd);
            panel.Children.Add(new TextBlock
            {
                Text = "保存后，下次启动或从托盘打开时必须输入正确密码。本次已打开的窗口不受影响。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 8),
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Muted"]
            });
            panel.Children.Add(Label("日志保留天数（最少 1）"));
            panel.Children.Add(_logRetain);
            panel.Children.Add(new TextBlock
            {
                Text = "到自动删除时间后，删除超过该天数的本机日志行。不删备份包、不删百度续传文件。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 8),
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Muted"]
            });
            panel.Children.Add(SaveButton(SaveGeneral));
            return panel;
        }

        private void Load()
        {
            var s = SettingsStore.Get(_boot.Db);
            _bak.Text = s.BakPath;
            _zipLevel.Text = s.ZipLevel.ToString();
            _zipPwd.Password = s.ZipPassword ?? "";
            _softwareId.Text = s.SoftwareId;
            ApplyNameFlags(s.NameFlags == 0 ? NameParts.DefaultFlags : s.NameFlags);
            _startWin.IsChecked = s.StartWithWindows;
            _tray.IsChecked = s.CloseToTray;
            _lockPwd.Password = s.LockPassword ?? "";
            _logRetain.Text = (s.LogRetainDays < 1 ? 30 : s.LogRetainDays).ToString();
            SelectTheme(s.Theme);
            var unc = SettingsStore.GetXfer<UncSettingsDto>(_boot.Db, "unc");
            _uncPath.Text = unc.Path ?? "";
            _uncDomain.Text = unc.Domain ?? "";
            _uncUser.Text = unc.User ?? "";
            _uncPwd.Password = unc.Password ?? "";
            var ftp = SettingsStore.GetXfer<FtpSettingsDto>(_boot.Db, "ftp");
            _ftpHost.Text = ftp.Host ?? "";
            _ftpPort.Text = ftp.Port <= 0 ? "21" : ftp.Port.ToString();
            _ftpUser.Text = ftp.User ?? "";
            _ftpPwd.Password = ftp.Password ?? "";
            _ftpDir.Text = string.IsNullOrEmpty(ftp.RemoteDir) ? "/" : ftp.RemoteDir;
            _ftpPassive.IsChecked = ftp.Passive;
            var smtp = SettingsStore.GetXfer<SmtpSettingsDto>(_boot.Db, "smtp");
            _smtpHost.Text = smtp.Host ?? "";
            _smtpPort.Text = smtp.Port <= 0 ? "25" : smtp.Port.ToString();
            _smtpSsl.IsChecked = smtp.Ssl;
            _smtpUser.Text = smtp.User ?? "";
            _smtpPwd.Password = smtp.Password ?? "";
            _smtpFrom.Text = smtp.From ?? "";
            _smtpTo.Text = smtp.To ?? "";
            _smtpSubject.Text = string.IsNullOrWhiteSpace(smtp.Subject) ? MailUtil.DefaultSubject : smtp.Subject;
            _smtpBody.Text = string.IsNullOrWhiteSpace(smtp.Body) ? MailUtil.DefaultBody : smtp.Body;
            _smtpFooter.Text = smtp.Footer ?? "";
            _sendAfterTask.IsChecked = smtp.SendAfterTask;
            SelectMailWhen(MailUtil.NormalizeWhen(smtp.SendWhen));
            SyncMailPolicyUi();
            _purgeLocal.IsChecked = s.PurgeLocal;
            _purgeNetwork.IsChecked = s.PurgeNetwork;
            _purgeFtp.IsChecked = s.PurgeFtp;
            _purgeQiniu.IsChecked = s.PurgeQiniu;
            _purgeBaidu.IsChecked = s.PurgeBaidu;
            _retainLocal.Text = (s.LocalRetainDays <= 0 ? 7 : s.LocalRetainDays).ToString();
            _retainNetwork.Text = (s.NetworkRetainDays <= 0 ? 7 : s.NetworkRetainDays).ToString();
            _retainFtp.Text = (s.FtpRetainDays <= 0 ? 7 : s.FtpRetainDays).ToString();
            _retainQiniu.Text = (s.QiniuRetainDays <= 0 ? 7 : s.QiniuRetainDays).ToString();
            _retainBaidu.Text = (s.BaiduRetainDays <= 0 ? 7 : s.BaiduRetainDays).ToString();
            DateTimePick.SetHmm(_purgeTime, string.IsNullOrEmpty(s.PurgeTime) ? "00:30" : s.PurgeTime);
            RefreshPreview();
        }

        private void SaveStorage()
        {
            var s = SettingsStore.Get(_boot.Db);
            s.BakPath = string.IsNullOrWhiteSpace(_bak.Text) ? AppPaths.BakDirectory : _bak.Text.Trim();
            int level;
            s.ZipLevel = int.TryParse(_zipLevel.Text, out level) ? level : 5;
            s.ZipPassword = _zipPwd.Password;
            Directory.CreateDirectory(s.BakPath);
            SettingsStore.Save(_boot.Db, s);
            RefreshPreview();
            MessageBox.Show(Window.GetWindow(this), "已保存。", "备份存储");
        }

        private void SelectTheme(string theme)
        {
            var want = ThemeService.Normalize(theme);
            for (var i = 0; i < _theme.Items.Count; i++)
            {
                var item = _theme.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null && string.Equals(item.Tag.ToString(), want, StringComparison.OrdinalIgnoreCase))
                {
                    _theme.SelectedIndex = i;
                    return;
                }
            }

            _theme.SelectedIndex = 0;
        }

        private string ThemeTag()
        {
            var item = _theme.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null)
            {
                return "light";
            }

            return ThemeService.Normalize(item.Tag.ToString());
        }

        private void SaveGeneral()
        {
            int logDays;
            if (!int.TryParse((_logRetain.Text ?? "").Trim(), out logDays) || logDays < 1)
            {
                MessageBox.Show(Window.GetWindow(this), "日志保留天数请填大于等于 1 的整数。", "综合设置");
                return;
            }

            var s = SettingsStore.Get(_boot.Db);
            s.SoftwareId = string.IsNullOrWhiteSpace(_softwareId.Text) ? AppPaths.ProductPathName : _softwareId.Text.Trim();
            s.StartWithWindows = _startWin.IsChecked == true;
            s.CloseToTray = _tray.IsChecked == true;
            s.LockPassword = _lockPwd.Password;
            s.Theme = ThemeTag();
            s.LogRetainDays = logDays;
            SettingsStore.Save(_boot.Db, s);
            ThemeService.Apply(s.Theme);
            var exe = Path.Combine(AppPaths.InstallDirectory, "Yxy.Client.exe");
            StartupHelper.Apply(s.StartWithWindows, exe);
            RefreshPreview();
            var tip = "已保存，主题已切换。" + (string.IsNullOrEmpty(s.LockPassword)
                ? "未设置锁定密码，下次打开可直接进入。"
                : "下次启动或从托盘打开时需要输入锁定密码。");
            MessageBox.Show(Window.GetWindow(this), tip, "综合设置");
        }

        private void SavePurge()
        {
            var s = SettingsStore.Get(_boot.Db);
            int a, b, c, d, e;
            s.PurgeLocal = _purgeLocal.IsChecked == true;
            s.PurgeNetwork = _purgeNetwork.IsChecked == true;
            s.PurgeFtp = _purgeFtp.IsChecked == true;
            s.PurgeQiniu = _purgeQiniu.IsChecked == true;
            s.PurgeBaidu = _purgeBaidu.IsChecked == true;
            s.LocalRetainDays = int.TryParse(_retainLocal.Text, out a) && a > 0 ? a : 7;
            s.NetworkRetainDays = int.TryParse(_retainNetwork.Text, out b) && b > 0 ? b : 7;
            s.FtpRetainDays = int.TryParse(_retainFtp.Text, out c) && c > 0 ? c : 7;
            s.QiniuRetainDays = int.TryParse(_retainQiniu.Text, out d) && d > 0 ? d : 7;
            s.BaiduRetainDays = int.TryParse(_retainBaidu.Text, out e) && e > 0 ? e : 7;
            s.PurgeTime = DateTimePick.GetHmm(_purgeTime, "00:30");
            SettingsStore.Save(_boot.Db, s);
            MessageBox.Show(Window.GetWindow(this), "已保存。", "自动删除");
        }

        private void SaveUnc()
        {
            SettingsStore.SaveXfer(_boot.Db, "unc", ReadUnc());
        }

        private void SaveFtp()
        {
            SettingsStore.SaveXfer(_boot.Db, "ftp", ReadFtp());
        }

        private void SaveSmtp()
        {
            SettingsStore.SaveXfer(_boot.Db, "smtp", ReadSmtp());
        }

        private void SyncMailPolicyUi()
        {
            var ready = MailUtil.SmtpReady(new SmtpSettingsDto
            {
                Host = (_smtpHost.Text ?? "").Trim(),
                From = (_smtpFrom.Text ?? "").Trim(),
                To = (_smtpTo.Text ?? "").Trim()
            });
            var vis = ready ? Visibility.Visible : Visibility.Collapsed;
            _sendAfterTask.Visibility = vis;
            var showWhen = ready && _sendAfterTask.IsChecked == true;
            _sendWhenLabel.Visibility = showWhen ? Visibility.Visible : Visibility.Collapsed;
            _sendWhen.Visibility = showWhen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectMailWhen(string when)
        {
            var want = MailUtil.NormalizeWhen(when);
            for (var i = 0; i < _sendWhen.Items.Count; i++)
            {
                var item = _sendWhen.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null
                    && string.Equals(item.Tag.ToString(), want, StringComparison.OrdinalIgnoreCase))
                {
                    _sendWhen.SelectedIndex = i;
                    return;
                }
            }

            _sendWhen.SelectedIndex = 0;
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

        private UncSettingsDto ReadUnc()
        {
            return new UncSettingsDto
            {
                Path = _uncPath.Text.Trim(),
                Domain = _uncDomain.Text.Trim(),
                User = _uncUser.Text.Trim(),
                Password = _uncPwd.Password
            };
        }

        private FtpSettingsDto ReadFtp()
        {
            int port;
            return new FtpSettingsDto
            {
                Host = _ftpHost.Text.Trim(),
                Port = int.TryParse(_ftpPort.Text, out port) ? port : 21,
                User = _ftpUser.Text.Trim(),
                Password = _ftpPwd.Password,
                Passive = _ftpPassive.IsChecked == true,
                RemoteDir = _ftpDir.Text.Trim()
            };
        }

        private SmtpSettingsDto ReadSmtp()
        {
            int port;
            return new SmtpSettingsDto
            {
                Host = _smtpHost.Text.Trim(),
                Port = int.TryParse(_smtpPort.Text, out port) ? port : 25,
                Ssl = _smtpSsl.IsChecked == true,
                User = _smtpUser.Text.Trim(),
                Password = _smtpPwd.Password,
                From = _smtpFrom.Text.Trim(),
                To = _smtpTo.Text.Trim(),
                Subject = _smtpSubject.Text ?? "",
                Body = _smtpBody.Text ?? "",
                Footer = _smtpFooter.Text ?? "",
                SendAfterTask = _sendAfterTask.IsChecked == true,
                SendWhen = MailUtil.NormalizeWhen(TagOf(_sendWhen, "fail"))
            };
        }

        private void SaveNaming()
        {
            var flags = ReadNameFlags();
            if ((flags & NameParts.FileMask) == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "文件名至少勾选一项。", "备份命名", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var s = SettingsStore.Get(_boot.Db);
            s.NameFlags = flags;
            SettingsStore.Save(_boot.Db, s);
            RefreshPreview();
            MessageBox.Show(Window.GetWindow(this), "已保存。之后执行的任务按此命名。", "备份命名");
        }

        private void RefreshPreview()
        {
            if (_preview == null || _nameFileSid == null)
            {
                return;
            }

            var sid = string.IsNullOrWhiteSpace(_softwareId.Text) ? AppPaths.ProductPathName : _softwareId.Text.Trim();
            var bak = string.IsNullOrWhiteSpace(_bak.Text) ? AppPaths.BakDirectory : _bak.Text.Trim();
            _preview.Text = "预览：\n" + BackupNaming.Preview(bak, sid, NameParts.FromFlags(ReadNameFlags()));
        }

        private int ReadNameFlags()
        {
            var parts = new NameParts
            {
                UseFileSoftwareId = _nameFileSid.IsChecked == true,
                UseFileTask = _nameFileTask.IsChecked == true,
                UseFileTime = _nameFileTime.IsChecked == true,
                UseFileDb = _nameFileDb.IsChecked == true,
                UseDirSoftwareId = _nameDirSid.IsChecked == true,
                UseDirKind = _nameDirKind.IsChecked == true,
                UseDirInstance = _nameDirInstance.IsChecked == true,
                UseDirTask = _nameDirTask.IsChecked == true
            };
            return parts.ToFlags();
        }

        private void ApplyNameFlags(int flags)
        {
            var parts = NameParts.FromFlags(flags);
            _nameFileSid.IsChecked = parts.UseFileSoftwareId;
            _nameFileTask.IsChecked = parts.UseFileTask;
            _nameFileTime.IsChecked = parts.UseFileTime;
            _nameFileDb.IsChecked = parts.UseFileDb;
            _nameDirSid.IsChecked = parts.UseDirSoftwareId;
            _nameDirKind.IsChecked = parts.UseDirKind;
            _nameDirInstance.IsChecked = parts.UseDirInstance;
            _nameDirTask.IsChecked = parts.UseDirTask;
        }

        private CheckBox NameCheck(string text)
        {
            var box = new CheckBox { Content = text, Margin = new Thickness(0, 4, 0, 0) };
            box.Checked += delegate { RefreshPreview(); };
            box.Unchecked += delegate { RefreshPreview(); };
            return box;
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 4) };
        }

        private static Button SaveButton(Action action)
        {
            var button = new Button { Content = "保存", Width = 80, Height = 28, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += delegate { action(); };
            return button;
        }

        private static TextBlock Hint(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap };
        }
    }
}
