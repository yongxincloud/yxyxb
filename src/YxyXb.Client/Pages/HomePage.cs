using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Runtime;
using YxyXb.Core.Service;
using YxyXb.Core.Store;

namespace YxyXb.Client.Pages
{
    public sealed class HomePage : UserControl
    {
        private readonly HostBootstrap _boot;
        private readonly TextBlock _txtTrial;
        private readonly TextBlock _txtFile;
        private readonly TextBlock _txtMssql;
        private readonly TextBlock _txtError;
        private readonly TextBlock _txtLogs;
        private readonly TextBlock _txtEngine;
        private readonly TextBlock _txtEngineHint;
        private readonly Button _btnInstall;
        private readonly Button _btnStart;
        private readonly Button _btnStop;
        private readonly Button _btnUninstall;

        public HomePage(HostBootstrap boot)
        {
            _boot = boot;

            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel();

            _txtTrial = AddCard(stack, "授权", "");
            var grid = new UniformGridTwo();
            _txtFile = AddMini(grid, "文件任务");
            _txtMssql = AddMini(grid, "库任务");
            _txtError = AddMini(grid, "当日错误");
            _txtLogs = AddMini(grid, "日志");
            stack.Children.Add(grid);

            var engineCard = CreateCard();
            var enginePanel = new StackPanel();
            enginePanel.Children.Add(Caption("引擎"));
            _txtEngine = new TextBlock { FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) };
            enginePanel.Children.Add(_txtEngine);
            _txtEngineHint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            enginePanel.Children.Add(_txtEngineHint);

            var buttons = new WrapPanel();
            _btnInstall = MakeButton("安装服务");
            _btnStart = MakeButton("启动服务");
            _btnStop = MakeButton("停止服务");
            _btnUninstall = MakeButton("卸载服务");
            _btnInstall.Click += BtnInstall_OnClick;
            _btnStart.Click += BtnStart_OnClick;
            _btnStop.Click += BtnStop_OnClick;
            _btnUninstall.Click += BtnUninstall_OnClick;
            buttons.Children.Add(_btnInstall);
            buttons.Children.Add(_btnStart);
            buttons.Children.Add(_btnStop);
            buttons.Children.Add(_btnUninstall);
            enginePanel.Children.Add(buttons);
            engineCard.Child = enginePanel;
            stack.Children.Add(engineCard);

            root.Content = stack;
            Content = root;
            Loaded += delegate { Refresh(); };
        }

        private void Refresh()
        {
            var session = SiteLogin.Load();
            _txtTrial.Inlines.Clear();
            if (!session.LoggedIn)
            {
                _txtTrial.Inlines.Add(new Run("未登录。请先登录或"));
                var link = new Hyperlink(new Run("申请帐号"));
                link.Click += OpenRegister;
                _txtTrial.Inlines.Add(link);
            }
            else
            {
                _txtTrial.Text = session.Summary;
            }

            var counts = TaskScheduleStore.GetHomeCounts(_boot.Db);
            _txtFile.Text = counts.FileTaskCount + " / 已执行 " + counts.FileExecutedCount;
            _txtMssql.Text = "MSSQL " + counts.MssqlTaskCount + "/" + counts.MssqlExecutedCount + " · MySQL " + counts.MysqlTaskCount + "/" + counts.MysqlExecutedCount;
            _txtError.Text = counts.TodayErrorCount.ToString();
            _txtLogs.Text = "自动 " + counts.AutoLogCount + " / 手动 " + counts.ManualLogCount;

            var engine = WindowsServiceControl.Query();
            ApplyEngine(engine);
        }

        private void ApplyEngine(EngineStatusDto engine)
        {
            _txtEngine.Text = engine.DisplayText;
            _txtEngine.Foreground = engine.Running
                ? (Brush)Application.Current.Resources["Ok"]
                : (Brush)Application.Current.Resources["Danger"];

            var hint = engine.Installed
                ? (engine.Running ? "关客户端后引擎仍会运行。" : "服务已安装但未运行。")
                : "未安装。安装需要管理员权限。";
            if (!string.IsNullOrEmpty(engine.LastError))
            {
                hint = hint + "\n" + engine.LastError;
            }

            var statusFile = AppPaths.EngineStatusFile;
            if (File.Exists(statusFile))
            {
                try
                {
                    hint = hint + "\n心跳：" + File.ReadAllText(statusFile).Trim();
                }
                catch (IOException)
                {
                }
            }

            _txtEngineHint.Text = hint;
            _btnInstall.IsEnabled = !engine.Installed;
            _btnStart.IsEnabled = engine.Installed && !engine.Running;
            _btnStop.IsEnabled = engine.Installed && engine.Running;
            _btnUninstall.IsEnabled = engine.Installed;
        }

        private void BtnInstall_OnClick(object sender, RoutedEventArgs e)
        {
            var exe = Path.Combine(AppPaths.InstallDirectory, "YxyXb.Engine.exe");
            var status = WindowsServiceControl.Install(exe);
            if (!status.Installed)
            {
                MessageBox.Show(Window.GetWindow(this), "安装失败。请用管理员身份运行客户端后重试。\n" + status.LastError, "引擎", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ApplyEngine(status);
        }

        private void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            var status = WindowsServiceControl.Start();
            if (!status.Running)
            {
                MessageBox.Show(Window.GetWindow(this), "启动失败。\n" + status.LastError, "引擎", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ApplyEngine(status);
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            var status = WindowsServiceControl.Stop();
            if (status.Running)
            {
                MessageBox.Show(Window.GetWindow(this), "停止失败。\n" + status.LastError, "引擎", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ApplyEngine(status);
        }

        private void BtnUninstall_OnClick(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                Window.GetWindow(this),
                "卸载后计划任务不会自动执行。已设置打开密码时，只能在本软件里停止并卸载。确定卸载服务？",
                "卸载服务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var status = WindowsServiceControl.Uninstall();
            if (status.Installed)
            {
                MessageBox.Show(Window.GetWindow(this), "卸载失败。请用管理员身份运行客户端后重试。\n" + status.LastError, "引擎", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ApplyEngine(status);
        }

        private static void OpenRegister(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(SiteLogin.RegisterUrl) { UseShellExecute = true });
        }

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Content = text,
                Width = 108,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 8)
            };
        }

        private static TextBlock AddCard(Panel parent, string title, string value)
        {
            var card = CreateCard();
            var panel = new StackPanel();
            panel.Children.Add(Caption(title));
            var text = new TextBlock { Text = value, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(text);
            card.Child = panel;
            parent.Children.Add(card);
            return text;
        }

        private static TextBlock AddMini(Panel parent, string title)
        {
            var card = CreateCard();
            var panel = new StackPanel();
            panel.Children.Add(Caption(title));
            var text = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) };
            panel.Children.Add(text);
            card.Child = panel;
            parent.Children.Add(card);
            return text;
        }

        private static Border CreateCard()
        {
            return new Border
            {
                Background = (Brush)Application.Current.Resources["CardBg"],
                BorderBrush = (Brush)Application.Current.Resources["CardLine"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };
        }

        private static TextBlock Caption(string text)
        {
            return new TextBlock { Text = text, Foreground = (Brush)Application.Current.Resources["Muted"] };
        }

        private sealed class UniformGridTwo : WrapPanel
        {
            public UniformGridTwo()
            {
                Loaded += delegate
                {
                    foreach (FrameworkElement child in Children)
                    {
                        child.Width = Math.Max(200, (ActualWidth - 16) / 2);
                        child.Margin = new Thickness(0, 0, 8, 0);
                    }
                };
            }
        }
    }
}
