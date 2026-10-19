using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YxyXb.Core;
using YxyXb.Core.Backup;

namespace YxyXb.Client.Pages
{
    internal sealed class UploadLiveLogWindow : Window
    {
        private readonly TextBox _box;
        private readonly DispatcherTimer _timer;

        public static UploadLiveLogWindow Open(Window owner, string taskName)
        {
            var window = new UploadLiveLogWindow(taskName);
            window.Owner = owner;
            window.WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            window.Show();
            return window;
        }

        private UploadLiveLogWindow(string taskName)
        {
            Title = "上传日志 · " + (taskName ?? "");
            Width = 760;
            Height = 480;
            WindowStartupLocation = Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            _box = ScrollableMessage.CreateBox();
            _box.MinHeight = 280;
            Content = new DockPanel { Margin = new Thickness(12), LastChildFill = true };
            var panel = (DockPanel)Content;
            var hint = new TextBlock
            {
                Text = "分片过程实时刷新。百度：" + AppPaths.BaiduUploadDirectory + " 七牛：" + AppPaths.QiniuUploadDirectory,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            panel.Children.Add(hint);
            panel.Children.Add(_box);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += delegate { RefreshText(); };
            Loaded += delegate
            {
                RefreshText();
                _timer.Start();
            };
            Closed += delegate { _timer.Stop(); };
        }

        public void Complete()
        {
            _timer.Stop();
            RefreshText();
            if (Title.IndexOf("完成", StringComparison.Ordinal) < 0)
            {
                Title = Title + "（完成，可关闭）";
            }
        }

        private void RefreshText()
        {
            try
            {
                var text = SliceLog.ReadRecent(AppPaths.DataDirectory, 20000);
                _box.Text = string.IsNullOrEmpty(text)
                    ? "等待分片日志… 若长时间为空，确认任务勾选了七牛云或百度网盘。"
                    : text;
                _box.ScrollToEnd();
            }
            catch (Exception ex)
            {
                _box.Text = ex.Message;
            }
        }
    }
}
