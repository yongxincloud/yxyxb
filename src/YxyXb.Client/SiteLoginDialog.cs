using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YxyXb.Client
{
    public static class SiteLoginDialog
    {
        public static void Prompt(Window owner, Action saved)
        {
            var dialog = new Window
            {
                Title = OemSkin.Load().DisplayName(),
                Owner = owner,
                Width = 440,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = BrushOf("PageBg", "#F7F9FC"),
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            var card = new Border
            {
                Background = BrushOf("CardBg", "#FFFFFF"),
                BorderBrush = BrushOf("CardLine", "#E5E7EB"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(20),
                Padding = new Thickness(20)
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "网站账号登录", FontSize = 20, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock
            {
                Text = "不保存密码，只保存登录令牌。",
                Foreground = BrushOf("Muted", "#6B7280"),
                Margin = new Thickness(0, 6, 0, 14)
            });
            var passwordMode = new RadioButton { Content = "密码登录", IsChecked = true, Margin = new Thickness(0, 0, 16, 8) };
            var codeMode = new RadioButton { Content = "验证码登录", Margin = new Thickness(0, 0, 0, 8) };
            var modes = new StackPanel { Orientation = Orientation.Horizontal };
            modes.Children.Add(passwordMode);
            modes.Children.Add(codeMode);
            panel.Children.Add(modes);
            panel.Children.Add(Label("邮箱"));
            var email = new TextBox { Height = 36, Margin = new Thickness(0, 4, 0, 10) };
            panel.Children.Add(email);
            var passwordLabel = Label("密码");
            panel.Children.Add(passwordLabel);
            var password = new PasswordBox { Height = 36, Margin = new Thickness(0, 4, 0, 10) };
            panel.Children.Add(password);
            var codeLabel = Label("验证码");
            codeLabel.Visibility = Visibility.Collapsed;
            panel.Children.Add(codeLabel);
            var code = new TextBox { Height = 36, Margin = new Thickness(0, 4, 0, 8), Visibility = Visibility.Collapsed };
            panel.Children.Add(code);
            var send = new Button { Content = "获取邮箱验证码", Height = 34, Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            panel.Children.Add(send);
            var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(error);
            var login = new Button { Content = "登录", Height = 40, IsDefault = true, Background = BrushOf("Accent", "#0E7490"), Foreground = Brushes.White };
            panel.Children.Add(login);
            UiHelp.Tip(passwordMode, "使用网站账号密码登录。");
            UiHelp.Tip(codeMode, "向登录邮箱发送一次性验证码，无需输入密码。");
            UiHelp.Tip(email, "注册和购买授权时使用的网站登录邮箱。");
            UiHelp.Tip(password, "网站账号密码只用于本次登录，不保存在本机。");
            UiHelp.Tip(code, "填写邮件中的一次性验证码，通常 5 分钟内有效。");
            UiHelp.Tip(send, "向当前邮箱发送登录验证码。");
            card.Child = panel;
            dialog.Content = card;

            passwordMode.Checked += delegate
            {
                passwordLabel.Visibility = Visibility.Visible;
                password.Visibility = Visibility.Visible;
                codeLabel.Visibility = Visibility.Collapsed;
                code.Visibility = Visibility.Collapsed;
                send.Visibility = Visibility.Collapsed;
            };
            codeMode.Checked += delegate
            {
                passwordLabel.Visibility = Visibility.Collapsed;
                password.Visibility = Visibility.Collapsed;
                codeLabel.Visibility = Visibility.Visible;
                code.Visibility = Visibility.Visible;
                send.Visibility = Visibility.Visible;
            };
            send.Click += delegate
            {
                try
                {
                    var filled = SiteLogin.SendCode(email.Text);
                    if (!string.IsNullOrEmpty(filled)) code.Text = filled;
                    error.Text = string.IsNullOrEmpty(filled) ? "验证码已发送。" : "开发环境验证码已填入。";
                }
                catch (Exception ex)
                {
                    error.Text = ex.Message;
                }
            };
            login.Click += delegate { SubmitLogin(); };
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key != Key.Enter) return;
                args.Handled = true;
                SubmitLogin();
            };
            dialog.ShowDialog();

            void SubmitLogin()
            {
                try
                {
                    var useCode = codeMode.IsChecked == true;
                    var attempt = SiteLogin.TryLogin(email.Text, useCode ? null : password.Password, useCode ? code.Text : null);
                    if (attempt.DeviceLimited)
                    {
                        dialog.Close();
                        MessageBox.Show(owner, SiteSession.DeviceLimitPrompt(attempt.Error), "登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (saved != null) saved();
                        return;
                    }

                    if (saved != null) saved();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    error.Text = ex.Message;
                }
            }
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, FontSize = 13 };
        }

        private static Brush BrushOf(string key, string fallback)
        {
            if (Application.Current != null && Application.Current.Resources.Contains(key))
            {
                return (Brush)Application.Current.Resources[key];
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
        }
    }
}
