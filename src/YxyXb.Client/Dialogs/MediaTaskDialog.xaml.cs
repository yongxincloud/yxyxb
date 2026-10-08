using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Schedule;
using YxyXb.Core.Store;

namespace YxyXb.Client.Dialogs
{
    public partial class MediaTaskDialog : Window
    {
        private readonly string _bakPath;
        private readonly long _editId;

        public MediaTaskDto Task { get; private set; }

        public MediaTaskDialog(string bakPath, IList<MediaRecordDto> media)
            : this(bakPath, media, null)
        {
        }

        public MediaTaskDialog(string bakPath, IList<MediaRecordDto> media, MediaTaskDto existing)
        {
            _bakPath = bakPath;
            _editId = existing == null ? 0 : existing.Id;
            InitializeComponent();
            Title = existing == null ? "添加离线介质任务" : "修改离线介质任务";
            TxtSource.Text = existing != null && !string.IsNullOrWhiteSpace(existing.SourcePath)
                ? existing.SourcePath
                : bakPath;
            CmbMedia.ItemsSource = media;
            if (existing != null)
            {
                Fill(existing, media);
            }
            else
            {
                if (media != null && media.Count > 0)
                {
                    CmbMedia.SelectedIndex = 0;
                }

                DateTimePick.SetDateTime(DtNext, TmNext, DateTime.Now.AddMinutes(2));
                DateTimePick.SetHmm(TmWinStart, "22:00");
                DateTimePick.SetHmm(TmWinEnd, "23:00");
            }
        }

        private void Fill(MediaTaskDto existing, IList<MediaRecordDto> media)
        {
            TxtName.Text = existing.Name;
            if (!string.IsNullOrWhiteSpace(existing.SourcePath))
            {
                TxtSource.Text = existing.SourcePath;
            }

            TxtFolder.Text = string.IsNullOrEmpty(existing.TargetFolder) ? AppPaths.ProductPathName : existing.TargetFolder;
            DateTimePick.SetHmm(TmWinStart, existing.WindowStart);
            DateTimePick.SetHmm(TmWinEnd, existing.WindowEnd);
            TxtWait.Text = existing.WaitMinutes.ToString(CultureInfo.InvariantCulture);
            TxtM.Text = existing.RetainVersions.ToString(CultureInfo.InvariantCulture);
            TxtD.Text = existing.RetainDays.ToString(CultureInfo.InvariantCulture);
            TxtInterval.Text = existing.CycleInterval.ToString(CultureInfo.InvariantCulture);
            ChkIsolate.IsChecked = existing.IsolateAfter;
            ChkHash.IsChecked = existing.UseSha256;
            ChkEnabled.IsChecked = existing.Enabled;
            SelectTag(CmbConflict, existing.ConflictMode, "overwrite");
            SelectTag(CmbCycle, existing.CycleKind, "day");
            SelectTag(CmbLevel, existing.IsolateLevel.ToString(CultureInfo.InvariantCulture), "2");
            if (existing.NextRunUtc.HasValue)
            {
                DateTimePick.SetDateTime(DtNext, TmNext, existing.NextRunUtc.Value.ToLocalTime());
            }

            if (media == null)
            {
                return;
            }

            for (var i = 0; i < media.Count; i++)
            {
                if (media[i].Id == existing.MediaId)
                {
                    CmbMedia.SelectedIndex = i;
                    return;
                }
            }

            if (media.Count > 0)
            {
                CmbMedia.SelectedIndex = 0;
            }
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            var selected = CmbMedia.SelectedItem as MediaRecordDto;
            DateTime nextLocal;
            if (!DateTimePick.TryGetDateTime(DtNext, TmNext, out nextLocal))
            {
                MessageBox.Show(this, "请选择下次执行的日期和时间。", "校验");
                return;
            }

            int wait, m, d, interval;
            if (!int.TryParse(TxtWait.Text, out wait) || !int.TryParse(TxtM.Text, out m)
                || !int.TryParse(TxtD.Text, out d) || !int.TryParse(TxtInterval.Text, out interval))
            {
                MessageBox.Show(this, "等待/版本/天数/间隔必须是数字。", "校验");
                return;
            }

            var task = new MediaTaskDto
            {
                Id = _editId,
                Name = (TxtName.Text ?? "").Trim(),
                MediaId = selected == null ? 0 : selected.Id,
                Enabled = ChkEnabled.IsChecked == true,
                SourcePath = (TxtSource.Text ?? "").Trim(),
                TargetFolder = (TxtFolder.Text ?? "").Trim(),
                WindowStart = DateTimePick.GetHmm(TmWinStart, "22:00"),
                WindowEnd = DateTimePick.GetHmm(TmWinEnd, "23:00"),
                WaitMinutes = wait,
                RetainVersions = m,
                RetainDays = d,
                ConflictMode = TagOf(CmbConflict, "overwrite"),
                IsolateAfter = ChkIsolate.IsChecked == true,
                IsolateLevel = int.Parse(TagOf(CmbLevel, "2")),
                UseSha256 = ChkHash.IsChecked == true,
                CycleKind = TagOf(CmbCycle, "day"),
                CycleInterval = interval,
                NextRunUtc = DateTime.SpecifyKind(nextLocal, DateTimeKind.Local).ToUniversalTime()
            };

            var error = MediaTaskStore.Validate(task, _bakPath);
            if (error != null)
            {
                MessageBox.Show(this, error, "校验");
                return;
            }

            if (!WindowClock.IsInWindow(nextLocal, task.WindowStart, task.WindowEnd))
            {
                var tip = "下次执行 " + nextLocal.ToString("HH:mm") + " 不在任务开始 " + task.WindowStart
                    + " 与介质离线 " + task.WindowEnd
                    + " 之间。到任务开始时间后才会检查：介质已在线则执行，未在线则先挂载再执行。确定保存？";
                if (MessageBox.Show(this, tip, "任务时间", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            Task = task;
            DialogResult = true;
        }

        private void BtnBrowse_OnClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择要拷到离线盘的扫描源（默认是 bak 目录）";
                if (!string.IsNullOrWhiteSpace(TxtSource.Text) && System.IO.Directory.Exists(TxtSource.Text))
                {
                    dialog.SelectedPath = TxtSource.Text.Trim();
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtSource.Text = dialog.SelectedPath;
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

            box.SelectedIndex = 0;
        }
    }
}
