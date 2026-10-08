using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace YxyXb.Client.Pages
{
    public sealed class DevicesPage : UserControl
    {
        private readonly Action _onSignedOut;
        private readonly DataGrid _grid;
        private readonly TextBlock _hint;

        public DevicesPage(Action onSignedOut)
        {
            _onSignedOut = onSignedOut;
            var panel = new DockPanel { Margin = new Thickness(8) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(Btn("刷新", delegate { Reload(); }));
            bar.Children.Add(Btn("踢出", delegate { KickSelected(); }));
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);

            _hint = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(_hint, Dock.Top);
            panel.Children.Add(_hint);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column
            };
            _grid.Columns.Add(Col("本机", "ThisText", 50));
            _grid.Columns.Add(Col("设备名称", "DisplayName", 280));
            _grid.Columns.Add(Col("设备 ID", "DeviceId", 220));
            _grid.Columns.Add(Col("首次登录", "FirstSeenLocal", 140));
            _grid.Columns.Add(Col("最近活跃", "LastSeenLocal", 140));
            _grid.Columns.Add(BuildKickColumn());
            panel.Children.Add(_grid);
            Content = panel;
            Loaded += delegate { Reload(); };
        }

        private void Reload()
        {
            if (!SiteLogin.Load().LoggedIn)
            {
                _hint.Text = "未登录。请先登录后再查看占用设备。";
                _grid.ItemsSource = null;
                return;
            }

            try
            {
                var list = SiteLogin.ListDevices();
                var maxText = list.MaxDevices == null ? "—" : list.MaxDevices.Value.ToString();
                _hint.Text = "当前版本：" + list.EditionText
                    + "。设备上限：" + maxText + " 台（已占用 " + list.Items.Count + " 台）。"
                    + "退出登录不释放名额，需主动踢出。踢出本机后将变为未登录。";
                _grid.ItemsSource = null;
                _grid.ItemsSource = list.Items;
            }
            catch (InvalidOperationException ex)
            {
                if (IsSessionDead(ex.Message))
                {
                    BecomeSignedOut(Window.GetWindow(this), "登录已失效（设备可能已被踢出）。");
                    return;
                }

                _hint.Text = ex.Message;
                _grid.ItemsSource = null;
            }
            catch (Exception ex)
            {
                _hint.Text = ex.Message;
                _grid.ItemsSource = null;
            }
        }

        private void KickSelected()
        {
            var row = _grid.SelectedItem as SiteDeviceItem;
            if (row == null)
            {
                Alert("请先选择设备。");
                return;
            }

            Kick(row);
        }

        private void Kick(SiteDeviceItem row)
        {
            if (row == null) return;
            var owner = Window.GetWindow(this);
            var tip = row.IsThisDevice
                ? "确定踢出本机？踢出后当前客户端将变为未登录。"
                : "确定踢出设备「" + row.DisplayName + "」？该设备上的登录将立即失效。";
            if (Ask(owner, tip, "踢出设备") != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                SiteLogin.KickDevice(row.DeviceId);
            }
            catch (InvalidOperationException ex)
            {
                if (IsSessionDead(ex.Message))
                {
                    BecomeSignedOut(owner, "登录已失效（设备可能已被踢出）。");
                    return;
                }

                Alert(owner, ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Alert(owner, ex.Message);
                return;
            }

            if (row.IsThisDevice)
            {
                BecomeSignedOut(owner, "本机已被踢出，当前为未登录状态。");
                return;
            }

            Reload();
        }

        private void BecomeSignedOut(Window owner, string message)
        {
            SiteLogin.SignOut();
            _hint.Text = message ?? "";
            _grid.ItemsSource = null;
            if (_onSignedOut != null) _onSignedOut();
        }

        private void Alert(string message)
        {
            Alert(Window.GetWindow(this), message);
        }

        private static void Alert(Window owner, string message)
        {
            if (owner != null) MessageBox.Show(owner, message, "我的设备");
            else MessageBox.Show(message, "我的设备");
        }

        private static MessageBoxResult Ask(Window owner, string message, string title)
        {
            if (owner != null)
                return MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        }

        private static bool IsSessionDead(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.IndexOf("登录已失效", StringComparison.Ordinal) >= 0
                || message.IndexOf("未登录", StringComparison.Ordinal) >= 0;
        }

        private DataGridTemplateColumn BuildKickColumn()
        {
            var col = new DataGridTemplateColumn { Header = "操作", Width = 72 };
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(Button));
            factory.SetValue(Button.ContentProperty, "踢出");
            factory.SetValue(Button.HeightProperty, 26.0);
            factory.SetValue(Button.PaddingProperty, new Thickness(8, 0, 8, 0));
            factory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 2));
            factory.AddHandler(Button.ClickEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs e)
            {
                var button = sender as Button;
                if (button == null) return;
                Kick(button.DataContext as SiteDeviceItem);
            }));
            template.VisualTree = factory;
            col.CellTemplate = template;
            return col;
        }

        private static Button Btn(string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Height = 28, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 0, 10, 0) };
            button.Click += click;
            return button;
        }

        private static DataGridTextColumn Col(string header, string path, double width)
        {
            return new DataGridTextColumn { Header = header, Binding = new Binding(path), Width = width };
        }
    }
}
