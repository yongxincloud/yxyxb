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
            var days = _boot.Trial.IsExpired
                ? "试用已到期（本期不拦截备份）。"
                : "本机试用还剩 " + _boot.Trial.RemainingDays + " 天（到期不拦截）。";
            MessageBox.Show(this, "授权登录后续开通。\n" + days + "\n本期无公网网站，不上报日志。", "登录", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Width = 400,
                Height = 176,
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

            var mini = new Button { Content = "最小化", Width = 96, Height = 36, Margin = new Thickness(0, 0, 8, 0) };
            var exit = new Button { Content = "退出程序", Width = 96, Height = 36, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "取消", Width = 80, Height = 36, IsCancel = true };
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
            buttons.Children.Add(mini);
            buttons.Children.Add(exit);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = "请选择：最小化到任务栏，或退出并关闭程序。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
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
                    PageHost.Content = new TasksPage(_boot);
                    break;
                case "media":
                    TxtTitle.Text = "离线介质";
                    PageHost.Content = new MediaPage(_boot);
                    break;
                case "cloud":
                    TxtTitle.Text = "云备份";
                    PageHost.Content = new CloudPage(_boot);
                    break;
                case "instances":
                    TxtTitle.Text = "实例";
                    PageHost.Content = new InstancesPage(_boot);
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
