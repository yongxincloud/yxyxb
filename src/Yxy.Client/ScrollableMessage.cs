using System.Windows;
using System.Windows.Controls;

namespace Yxy.Client.Pages
{
    internal static class ScrollableMessage
    {
        public static TextBox CreateBox()
        {
            return new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 140,
                FontSize = 13,
                Padding = new Thickness(8)
            };
        }

        public static void Show(Window owner, string title, string text)
        {
            var box = CreateBox();
            box.Text = text ?? "";
            box.MinHeight = 220;
            var window = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "说明" : title,
                Owner = owner,
                Width = 720,
                Height = 420,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Content = new DockPanel { Margin = new Thickness(12), LastChildFill = true }
            };
            var panel = (DockPanel)window.Content;
            var ok = new Button
            {
                Content = "关闭",
                Width = 88,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0),
                IsDefault = true,
                IsCancel = true
            };
            ok.Click += delegate { window.DialogResult = true; };
            DockPanel.SetDock(ok, Dock.Bottom);
            panel.Children.Add(ok);
            panel.Children.Add(box);
            window.ShowDialog();
        }
    }
}
