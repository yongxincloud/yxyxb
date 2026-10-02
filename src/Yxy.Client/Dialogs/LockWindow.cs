using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Yxy.Client.Dialogs
{
    public sealed class LockWindow : Window
    {
        private readonly string _expected;
        private readonly PasswordBox _box;
        private readonly TextBlock _error;

        public LockWindow(string expected, string productName)
            : this(expected, productName, "退出")
        {
        }

        public LockWindow(string expected, string productName, string cancelText)
        {
            _expected = expected ?? "";
            Title = string.IsNullOrWhiteSpace(productName) ? "用心云备份" : productName.Trim();
            Width = 440;
            Height = 260;
            Style = (Style)Application.Current.FindResource("YxyDialog");
            Background = (Brush)Application.Current.FindResource("PageBg");
            FontFamily = new FontFamily("Microsoft YaHei");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            Topmost = true;
            try
            {
                Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/logo.ico"));
            }
            catch (IOException)
            {
            }
            catch (UriFormatException)
            {
            }

            _box = new PasswordBox { Margin = new Thickness(0, 8, 0, 4) };
            _error = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                MinHeight = 18
            };

            var ok = new Button
            {
                Content = "进入",
                Width = 96,
                Height = 36,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var cancel = new Button
            {
                Content = string.IsNullOrWhiteSpace(cancelText) ? "退出" : cancelText,
                Width = 96,
                Height = 36,
                IsCancel = true
            };
            ok.Click += OnOk;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = "已设置本机锁定密码。输入正确后才能进入系统。",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(_box);
            panel.Children.Add(_error);
            panel.Children.Add(buttons);
            Content = panel;
            Loaded += delegate { _box.Focus(); };
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (string.Equals(_box.Password, _expected, StringComparison.Ordinal))
            {
                DialogResult = true;
                return;
            }

            _error.Text = "密码不正确。";
            _box.Clear();
            _box.Focus();
        }
    }
}
