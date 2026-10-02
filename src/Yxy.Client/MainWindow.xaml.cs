using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yxy.Client.Dialogs;
using Yxy.Client.Pages;
using Yxy.Core;
using Yxy.Core.Runtime;
using Yxy.Core.Store;

namespace Yxy.Client
{
    public partial class MainWindow : Window
    {
        private readonly HostBootstrap _boot;
        private readonly System.Windows.Forms.NotifyIcon _tray;
        private string _current = "home";
        private bool _forceClose;
        private bool _unlocking;

        public MainWindow()
            : this(null)
        {
        }

        public MainWindow(HostBootstrap boot)
        {
            InitializeComponent();
            _boot = boot ?? HostBootstrap.Start();
            _tray = new System.Windows.Forms.NotifyIcon();
            _tray.Text = "用心云备份";
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            if (File.Exists(exe))
            {
                _tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            }

            _tray.DoubleClick += delegate { RestoreFromTray(); };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("打开", null, delegate { RestoreFromTray(); });
            menu.Items.Add("退出", null, delegate
            {
                _forceClose = true;
                Close();
            });
            _tray.ContextMenuStrip = menu;
            ShowPage("home");
        }

        private void Nav_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null || button.Tag == null)
            {
                return;
            }

            ShowPage(button.Tag.ToString());
        }

        private void BtnLogin_OnClick(object sender, RoutedEventArgs e)
        {
            SiteLogin.Prompt(this, delegate
            {
                var session = SiteLogin.Load();
                BtnLogin.Content = session.LoggedIn ? "已登录" : "登录";
                ShowPage("home");
            });
        }

        private void Drag_OnDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnWindow_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "退出",
                Owner = this,
                Width = 480,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Background = (Brush)FindResource("PageBg")
            };
            var style = TryFindResource("YxyDialog") as Style;
            if (style != null)
            {
                dialog.Style = style;
            }

            var signOut = new Button { Content = "注销账号", Width = 120, Height = 40, Margin = new Thickness(8, 0, 0, 0), FontSize = 15 };
            var mini = new Button { Content = "最小化", Width = 120, Height = 40, Margin = new Thickness(8, 0, 0, 0), FontSize = 15 };
            var exit = new Button { Content = "退出程序", Width = 120, Height = 40, Margin = new Thickness(8, 0, 0, 0), FontSize = 15 };
            signOut.Click += delegate
            {
                if (!SiteLogin.Load().LoggedIn)
                {
                    MessageBox.Show(dialog, "当前没有登录账号。", "注销账号", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SiteLogin.SignOut();
                BtnLogin.Content = "登录";
                dialog.Close();
                ShowPage(_current);
            };
            mini.Click += delegate
            {
                dialog.Close();
                WindowState = WindowState.Minimized;
            };
            exit.Click += delegate
            {
                _forceClose = true;
                dialog.Close();
                Close();
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(signOut);
            buttons.Children.Add(mini);
            buttons.Children.Add(exit);

            var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };
            panel.Children.Add(new TextBlock
            {
                Text = "请选择：最小化到任务栏、注销当前网站账号，或退出并关闭程序。",
                FontSize = 18,
                LineHeight = 30,
                Foreground = (Brush)FindResource("TitleText"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 28)
            });
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void BtnSite_OnClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "官网地址后续配置。", "官网", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowPage(string key)
        {
            _current = key;
            Highlight(key);
            switch (key)
            {
                case "home":
                    TxtTitle.Text = "首页";
                    PageHost.Content = new HomePage(_boot);
                    break;
                case "tasks":
                    TxtTitle.Text = "任务";
                    PageHost.Content = Licensed(key) ? (object)new TasksPage(_boot) : LockedPage();
                    break;
                case "media":
                    TxtTitle.Text = "离线介质";
                    PageHost.Content = new MediaPage(_boot);
                    break;
                case "cloud":
                    TxtTitle.Text = "云备份";
                    PageHost.Content = Licensed(key) ? (object)new CloudPage(_boot) : LockedPage();
                    break;
                case "instances":
                    TxtTitle.Text = "实例";
                    PageHost.Content = Licensed(key) ? (object)new InstancesPage(_boot) : LockedPage();
                    break;
                case "logs":
                    TxtTitle.Text = "日志";
                    PageHost.Content = new LogsPage(_boot);
                    break;
                case "settings":
                    TxtTitle.Text = "设置";
                    PageHost.Content = new SettingsPage(_boot);
                    break;
                default:
                    break;
            }
        }

        private static bool Licensed(string key)
        {
            return SiteLogin.Load().Usable;
        }

        private static UserControl LockedPage()
        {
            var page = new UserControl();
            var text = new TextBlock
            {
                Text = SiteLogin.Load().Summary + "\n\n文件备份、数据库备份、云、UNC、FTP、邮件需要有效授权。离线介质不受影响。",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16,
                Margin = new Thickness(8)
            };
            page.Content = text;
            return page;
        }

        private void Highlight(string key)
        {
            SetNav(NavHome, key == "home");
            SetNav(NavTasks, key == "tasks");
            SetNav(NavMedia, key == "media");
            SetNav(NavCloud, key == "cloud");
            SetNav(NavInstances, key == "instances");
            SetNav(NavLogs, key == "logs");
            SetNav(NavSettings, key == "settings");
        }

        private static void SetNav(Button button, bool active)
        {
            button.Background = active
                ? (Brush)Application.Current.Resources["NavActive"]
                : Brushes.Transparent;
            button.Foreground = active
                ? (Brush)Application.Current.Resources["Accent"]
                : (Brush)Application.Current.Resources["NavText"];
        }

        private void RestoreFromTray()
        {
            if (_unlocking)
            {
                return;
            }

            if (!IsVisible && !ConfirmLock())
            {
                return;
            }

            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
            if (!SettingsStore.Get(_boot.Db).CloseToTray)
            {
                _tray.Visible = false;
            }
        }

        private bool ConfirmLock()
        {
            var settings = SettingsStore.Get(_boot.Db);
            if (string.IsNullOrEmpty(settings.LockPassword))
            {
                return true;
            }

            _unlocking = true;
            try
            {
                var gate = new LockWindow(settings.LockPassword, settings.SoftwareId, "取消");
                return gate.ShowDialog() == true;
            }
            finally
            {
                _unlocking = false;
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
                if (_tray.Icon != null)
                {
                    _tray.Visible = true;
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceClose && SettingsStore.Get(_boot.Db).CloseToTray)
            {
                e.Cancel = true;
                Hide();
                _tray.Visible = true;
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _tray.Visible = false;
            _tray.Dispose();
            if (_boot != null)
            {
                _boot.Db.Dispose();
            }

            base.OnClosed(e);
        }
    }
}
