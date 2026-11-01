using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using YxyXb.Core;

namespace YxyXb.Client.Dialogs
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
            Title = string.IsNullOrWhiteSpace(productName) ? AppPaths.ProductDisplayName : productName.Trim();
            Width = 400;
            Height = 470;
            Style = (Style)Application.Current.FindResource("YxyXbDialog");
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

            _box = new PasswordBox { FontSize = 14, Margin = new Thickness(0, 18, 0, 2) };
            HintAssist.SetHint(_box, "请输入密码");

            _error = new TextBlock
            {
                Foreground = (Brush)Application.Current.FindResource("Danger"),
                FontSize = 12,
                MinHeight = 18,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var ok = MakeButton("进入", true);
            ok.IsDefault = true;
            ok.Margin = new Thickness(0, 10, 0, 0);
            ok.Click += OnOk;

            var cancel = MakeButton(string.IsNullOrWhiteSpace(cancelText) ? "退出" : cancelText, false);
            cancel.IsCancel = true;
            cancel.Margin = new Thickness(0, 6, 0, 0);

            var panel = new StackPanel();
            panel.Children.Add(MakeLogo());
            panel.Children.Add(new TextBlock
            {
                Text = Title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("TitleText"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "已设置本机锁定密码，输入正确后才能进入系统。",
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontSize = 12,
                Foreground = (Brush)Application.Current.FindResource("Muted"),
                Margin = new Thickness(0, 6, 0, 0)
            });
            panel.Children.Add(_box);
            panel.Children.Add(_error);
            panel.Children.Add(ok);
            panel.Children.Add(cancel);

            var card = new Border
            {
                Width = 320,
                Padding = new Thickness(28, 26, 28, 22),
                CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.FindResource("CardBg"),
                BorderBrush = (Brush)Application.Current.FindResource("CardLine"),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0x0F, 0x17, 0x2A),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.14
                },
                Child = panel
            };

            var root = new Grid();
            root.Children.Add(card);
            Content = root;
            Loaded += delegate { _box.Focus(); };
        }

        private static Image MakeLogo()
        {
            var img = new Image
            {
                Width = 52,
                Height = 52,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            try
            {
                img.Source = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/logo.ico"));
            }
            catch (IOException)
            {
            }
            catch (UriFormatException)
            {
            }

            return img;
        }

        private static Button MakeButton(string text, bool primary)
        {
            var button = new Button
            {
                Content = text,
                Height = 38,
                FontSize = 14,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            var normal = primary
                ? (Brush)Application.Current.FindResource("Accent")
                : Brushes.Transparent;
            var hover = primary
                ? new SolidColorBrush(Color.FromRgb(0x15, 0x5E, 0x75))
                : (Brush)Application.Current.FindResource("NavHover");
            button.Background = normal;
            button.Foreground = primary
                ? Brushes.White
                : (Brush)Application.Current.FindResource("Muted");

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ButtonBase.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hover, "border"));
            border.Name = "border";
            template.Triggers.Add(hoverTrigger);
            button.Template = template;

            return button;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (string.Equals(_box.Password, _expected, StringComparison.Ordinal))
            {
                DialogResult = true;
                return;
            }

            _error.Text = "密码不正确，请重试。";
            _box.Clear();
            _box.Focus();
        }
    }
}
