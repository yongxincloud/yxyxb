using System.Windows;
using System.Windows.Controls;
using Yxy.Contracts.Dto;
using Yxy.Core.Security;
using Yxy.Core.Store;
using Yxy.MySql;

namespace Yxy.Client.Dialogs
{
    public sealed class MysqlInstanceDialog : Window
    {
        private readonly long _editId;
        private readonly bool _hadPassword;
        private readonly string _existingProtected;
        private readonly TextBox _name = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly TextBox _server = new TextBox { Text = "localhost", Margin = new Thickness(0, 0, 0, 8) };
        private readonly TextBox _port = new TextBox { Text = "3306", Margin = new Thickness(0, 0, 0, 8) };
        private readonly TextBox _user = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly PasswordBox _password = new PasswordBox { Margin = new Thickness(0, 0, 0, 8) };
        private readonly TextBox _timeout = new TextBox { Text = "30", Margin = new Thickness(0, 0, 0, 8) };

        public MysqlInstanceDto Instance { get; private set; }

        public string PasswordPlain { get; private set; }

        public bool KeepPassword { get; private set; }

        public MysqlInstanceDialog(MysqlInstanceDto existing)
        {
            Title = existing == null ? "添加 MySQL 实例" : "修改 MySQL 实例";
            Width = 420;
            Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            _editId = existing == null ? 0 : existing.Id;
            _existingProtected = existing == null ? null : existing.PasswordProtected;
            _hadPassword = !string.IsNullOrEmpty(_existingProtected);
            if (existing != null)
            {
                _name.Text = existing.Name;
                _server.Text = existing.Server;
                _port.Text = existing.Port.ToString();
                _user.Text = existing.Username ?? "";
                _timeout.Text = existing.TimeoutSeconds.ToString();
            }

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(Label("名称"));
            panel.Children.Add(_name);
            panel.Children.Add(Label("服务器"));
            panel.Children.Add(_server);
            panel.Children.Add(Label("端口"));
            panel.Children.Add(_port);
            panel.Children.Add(Label("用户名"));
            panel.Children.Add(_user);
            panel.Children.Add(Label("密码"));
            panel.Children.Add(_password);
            panel.Children.Add(Label("超时（秒）"));
            panel.Children.Add(_timeout);
            var bar = new StackPanel { Orientation = Orientation.Horizontal };
            var test = new Button { Content = "测试连接", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            var ok = new Button { Content = "确定", Width = 80, IsDefault = true };
            test.Click += delegate { Test(); };
            ok.Click += delegate { Accept(); };
            bar.Children.Add(test);
            bar.Children.Add(ok);
            panel.Children.Add(bar);
            Content = panel;
        }

        private void Test()
        {
            var item = Build(false);
            if (item == null) return;
            MessageBox.Show(this, MysqlConnect.Test(item), "测试连接");
        }

        private void Accept()
        {
            var item = Build(true);
            if (item == null) return;
            Instance = item;
            DialogResult = true;
        }

        private MysqlInstanceDto Build(bool save)
        {
            int port, timeout;
            if (!int.TryParse(_port.Text, out port) || !int.TryParse(_timeout.Text, out timeout))
            {
                MessageBox.Show(this, "端口和超时必须是数字。", "校验");
                return null;
            }

            var item = new MysqlInstanceDto
            {
                Id = _editId,
                Name = (_name.Text ?? "").Trim(),
                Server = (_server.Text ?? "").Trim(),
                Port = port,
                Username = (_user.Text ?? "").Trim(),
                TimeoutSeconds = timeout,
                Enabled = true
            };
            var error = MysqlInstanceStore.Validate(item);
            if (error != null)
            {
                MessageBox.Show(this, error, "校验");
                return null;
            }

            PasswordPlain = _password.Password;
            if (!string.IsNullOrEmpty(PasswordPlain)) item.PasswordProtected = SecretBox.Protect(PasswordPlain);
            else item.PasswordProtected = _existingProtected;
            KeepPassword = string.IsNullOrEmpty(PasswordPlain) && _hadPassword && save;
            if (string.IsNullOrEmpty(PasswordPlain) && !KeepPassword)
            {
                MessageBox.Show(this, "请填写密码。", "校验");
                return null;
            }

            return item;
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(0, 0, 0, 4) };
        }
    }
}
