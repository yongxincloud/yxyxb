using System;
using System.Windows;
using System.Windows.Controls;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Security;
using YxyXb.Core.Store;
using YxyXb.Mssql;

namespace YxyXb.Client.Dialogs
{
    public partial class InstanceDialog : Window
    {
        private readonly long _editId;
        private readonly bool _hadPassword;
        private readonly string _existingProtected;

        public MssqlInstanceDto Instance { get; private set; }

        public string PasswordPlain { get; private set; }

        public bool KeepPassword { get; private set; }

        public InstanceDialog()
            : this(null)
        {
        }

        public InstanceDialog(MssqlInstanceDto existing)
        {
            InitializeComponent();
            _editId = existing == null ? 0 : existing.Id;
            _existingProtected = existing == null ? null : existing.PasswordProtected;
            _hadPassword = !string.IsNullOrEmpty(_existingProtected);
            Title = existing == null ? "添加 MSSQL 实例" : "修改 MSSQL 实例";
            if (existing != null)
            {
                TxtName.Text = existing.Name;
                TxtServer.Text = existing.Server;
                TxtPort.Text = existing.Port.ToString();
                TxtUser.Text = existing.Username ?? "";
                TxtTimeout.Text = existing.TimeoutSeconds.ToString();
                SelectAuth(existing.AuthKind);
            }
        }

        private void CmbAuth_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtUser == null)
            {
                return;
            }

            var sql = TagOf(CmbAuth, "windows") == "sql";
            TxtUser.IsEnabled = sql;
            TxtPassword.IsEnabled = sql;
        }

        private void BtnTest_OnClick(object sender, RoutedEventArgs e)
        {
            var item = Build(false);
            if (item == null)
            {
                return;
            }

            MessageBox.Show(this, MssqlConnect.Test(item), "测试连接");
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            var item = Build(true);
            if (item == null)
            {
                return;
            }

            Instance = item;
            DialogResult = true;
        }

        private MssqlInstanceDto Build(bool save)
        {
            int port, timeout;
            if (!int.TryParse(TxtPort.Text, out port) || !int.TryParse(TxtTimeout.Text, out timeout))
            {
                MessageBox.Show(this, "端口和超时必须是数字。", "校验");
                return null;
            }

            var item = new MssqlInstanceDto
            {
                Id = _editId,
                Name = (TxtName.Text ?? "").Trim(),
                Server = (TxtServer.Text ?? "").Trim(),
                Port = port,
                AuthKind = TagOf(CmbAuth, "windows"),
                Username = (TxtUser.Text ?? "").Trim(),
                TimeoutSeconds = timeout,
                Enabled = true
            };
            var error = MssqlInstanceStore.Validate(item);
            if (error != null)
            {
                MessageBox.Show(this, error, "校验");
                return null;
            }

            PasswordPlain = TxtPassword.Password;
            if (!string.IsNullOrEmpty(PasswordPlain))
            {
                item.PasswordProtected = SecretBox.Protect(PasswordPlain);
            }
            else
            {
                item.PasswordProtected = _existingProtected;
            }

            KeepPassword = item.AuthKind == "sql" && string.IsNullOrEmpty(PasswordPlain) && _hadPassword && save;
            if (item.AuthKind == "sql" && string.IsNullOrEmpty(PasswordPlain) && !KeepPassword)
            {
                MessageBox.Show(this, "请填写 SQL 密码。", "校验");
                return null;
            }

            return item;
        }

        private void SelectAuth(string kind)
        {
            var want = string.IsNullOrEmpty(kind) ? "windows" : kind;
            for (var i = 0; i < CmbAuth.Items.Count; i++)
            {
                var item = CmbAuth.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null && string.Equals(item.Tag.ToString(), want, StringComparison.OrdinalIgnoreCase))
                {
                    CmbAuth.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string TagOf(ComboBox box, string fallback)
        {
            var item = box.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null)
            {
                return fallback;
            }

            return item.Tag.ToString();
        }
    }
}
