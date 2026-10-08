using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;

namespace YxyXb.Client.Dialogs
{
    public partial class FileTaskDialog : Window
    {
        private readonly string _bakPath;
        private readonly long _editId;
        private readonly IList<string> _ready;
        private bool _loading;

        public FileTaskDto Task { get; private set; }

        public FileTaskDialog(string bakPath)
            : this(bakPath, null, null)
        {
        }

        public FileTaskDialog(string bakPath, FileTaskDto existing)
            : this(bakPath, existing, null)
        {
        }

        public FileTaskDialog(string bakPath, FileTaskDto existing, IList<string> readyProviders)
        {
            _bakPath = bakPath;
            _ready = readyProviders ?? new List<string>();
            _editId = existing == null ? 0 : existing.Id;
            InitializeComponent();
            Title = existing == null ? "添加文件备份任务" : "修改文件备份任务";
            CloudPickerUi.Bind(ChkCloud, CmbCloud, _ready);

            if (existing != null)
            {
                Fill(existing);
            }
            else
            {
                DateTimePick.SetDateTime(DtNext, TmNext, DateTime.Now.AddMinutes(2));
            }
        }

        private void Fill(FileTaskDto existing)
        {
            _loading = true;
            TxtName.Text = existing.Name;
            TxtPath.Text = existing.SourcePath;
            TxtRetry.Text = existing.RetryMinutes.ToString(CultureInfo.InvariantCulture);
            TxtSort.Text = existing.SortOrder.ToString(CultureInfo.InvariantCulture);
            TxtInterval.Text = existing.CycleInterval.ToString(CultureInfo.InvariantCulture);
            TxtPost.Text = existing.PostProgram ?? "";
            ChkEnabled.IsChecked = existing.Enabled;
            ChkNetwork.IsChecked = existing.DoNetwork;
            ChkFtp.IsChecked = existing.DoFtp;
            ChkCloud.IsChecked = existing.DoCloud && _ready.Count > 0;
            ChkDelete.IsChecked = existing.DoDeleteNow;
            ChkEmail.IsChecked = existing.DoEmail;
            SelectTag(CmbSource, existing.SourceKind, "local");
            SelectTag(CmbCycle, existing.CycleKind, "day");
            SelectTag(CmbCloud, existing.CloudProvider, _ready.Count > 0 ? _ready[0] : "qiniu");
            if (existing.NextRunUtc.HasValue)
            {
                DateTimePick.SetDateTime(DtNext, TmNext, existing.NextRunUtc.Value.ToLocalTime());
            }

            BtnBrowse.IsEnabled = SelectedTag(CmbSource) == "local";
            _loading = false;
        }

        private void CmbSource_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BtnBrowse == null)
            {
                return;
            }

            BtnBrowse.IsEnabled = SelectedTag(CmbSource) == "local";
        }

        private void BtnBrowse_OnClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择要备份的文件夹";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void ChkDelete_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            MessageBox.Show(this, "勾选后，网络/FTP/云/邮件（已启用的）都成功才会删当次本地包。离线介质可能拷不到本次包。", "立即删", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            DateTime nextLocal;
            if (!DateTimePick.TryGetDateTime(DtNext, TmNext, out nextLocal))
            {
                MessageBox.Show(this, "请选择下次执行的日期和时间。", "校验", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int retry;
            int sort;
            int interval;
            if (!int.TryParse(TxtRetry.Text, out retry) || !int.TryParse(TxtSort.Text, out sort) || !int.TryParse(TxtInterval.Text, out interval))
            {
                MessageBox.Show(this, "重计时 / 排序 / 间隔必须是数字。", "校验", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var task = new FileTaskDto
            {
                Id = _editId,
                Name = (TxtName.Text ?? "").Trim(),
                Enabled = ChkEnabled.IsChecked == true,
                SourceKind = SelectedTag(CmbSource),
                SourcePath = (TxtPath.Text ?? "").Trim(),
                RetryMinutes = retry,
                SortOrder = sort,
                CycleKind = SelectedTag(CmbCycle),
                CycleInterval = interval,
                NextRunUtc = DateTime.SpecifyKind(nextLocal, DateTimeKind.Local).ToUniversalTime(),
                DoNetwork = ChkNetwork.IsChecked == true,
                DoFtp = ChkFtp.IsChecked == true,
                DoCloud = _ready.Count > 0 && ChkCloud.IsChecked == true,
                CloudProvider = CloudChoice.Resolve(_ready, ChkCloud.IsChecked == true, SelectedTag(CmbCloud)),
                DoDeleteNow = ChkDelete.IsChecked == true,
                DoEmail = ChkEmail.IsChecked == true,
                PostProgram = (TxtPost.Text ?? "").Trim()
            };

            var error = FileTaskValidator.Validate(task, _bakPath);
            if (error != null)
            {
                MessageBox.Show(this, error, "校验", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Task = task;
            DialogResult = true;
        }

        private static string SelectedTag(ComboBox box)
        {
            var item = box.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null)
            {
                return "local";
            }

            return item.Tag.ToString();
        }

        private static void SelectTag(ComboBox box, string tag, string fallback)
        {
            var want = string.IsNullOrEmpty(tag) ? fallback : tag;
            for (var i = 0; i < box.Items.Count; i++)
            {
                var item = box.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null
                    && string.Equals(item.Tag.ToString(), want, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
