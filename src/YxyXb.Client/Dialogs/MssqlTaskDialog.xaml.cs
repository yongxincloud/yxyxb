using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Store;
using YxyXb.Mssql;

namespace YxyXb.Client.Dialogs
{
    public partial class MssqlTaskDialog : Window
    {
        private readonly long _editId;
        private readonly IList<MssqlInstanceDto> _instances;
        private readonly IList<string> _ready;
        private bool _loading;

        public MssqlTaskDto Task { get; private set; }

        public MssqlTaskDialog(IList<MssqlInstanceDto> instances)
            : this(instances, null, null)
        {
        }

        public MssqlTaskDialog(IList<MssqlInstanceDto> instances, MssqlTaskDto existing)
            : this(instances, existing, null)
        {
        }

        public MssqlTaskDialog(IList<MssqlInstanceDto> instances, MssqlTaskDto existing, IList<string> readyProviders)
        {
            _instances = instances;
            _ready = readyProviders ?? new List<string>();
            _editId = existing == null ? 0 : existing.Id;
            InitializeComponent();
            Title = existing == null ? "添加 MSSQL 库任务" : "修改 MSSQL 库任务";
            CloudPickerUi.Bind(ChkCloud, CmbCloud, _ready);
            CmbInstance.ItemsSource = instances;
            if (existing != null)
            {
                Fill(existing);
            }
            else
            {
                if (instances != null && instances.Count > 0)
                {
                    CmbInstance.SelectedIndex = 0;
                }

                DateTimePick.SetDateTime(DtNext, TmNext, DateTime.Now.AddMinutes(2));
                ReloadDbs();
            }
        }

        private void Fill(MssqlTaskDto existing)
        {
            _loading = true;
            try
            {
                TxtName.Text = existing.Name;
                TxtRetry.Text = existing.RetryMinutes.ToString(CultureInfo.InvariantCulture);
                TxtInterval.Text = existing.CycleInterval.ToString(CultureInfo.InvariantCulture);
                ChkEnabled.IsChecked = existing.Enabled;
                ChkNetwork.IsChecked = existing.DoNetwork;
                ChkFtp.IsChecked = existing.DoFtp;
                ChkCloud.IsChecked = existing.DoCloud && _ready.Count > 0;
                ChkDelete.IsChecked = existing.DoDeleteNow;
                ChkEmail.IsChecked = existing.DoEmail;
                SelectTag(CmbCycle, existing.CycleKind, "day");
                SelectTag(CmbScope, existing.DbScope, "user");
                SelectTag(CmbCloud, existing.CloudProvider, _ready.Count > 0 ? _ready[0] : "qiniu");
            if (existing.NextRunUtc.HasValue)
            {
                DateTimePick.SetDateTime(DtNext, TmNext, existing.NextRunUtc.Value.ToLocalTime());
            }

                if (_instances != null)
                {
                    for (var i = 0; i < _instances.Count; i++)
                    {
                        if (_instances[i].Id == existing.InstanceId)
                        {
                            CmbInstance.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _loading = false;
            }

            ReloadDbs();
            ApplySelected(existing.SelectedDbs);
        }

        private void CmbInstance_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || LstDbs == null)
            {
                return;
            }

            ReloadDbs();
        }

        private void CmbScope_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstDbs == null)
            {
                return;
            }

            LstDbs.IsEnabled = TagOf(CmbScope, "user") == "selected";
        }

        private void ReloadDbs()
        {
            LstDbs.Items.Clear();
            var inst = CmbInstance.SelectedItem as MssqlInstanceDto;
            if (inst == null)
            {
                return;
            }

            try
            {
                var dbs = MssqlConnect.ListUserDatabases(inst);
                for (var i = 0; i < dbs.Count; i++)
                {
                    LstDbs.Items.Add(new CheckBox { Content = dbs[i], IsChecked = false, Margin = new Thickness(0, 2, 0, 2) });
                }
            }
            catch (Exception ex)
            {
                LstDbs.Items.Add(new TextBlock { Text = "列库失败：" + ex.Message, TextWrapping = TextWrapping.Wrap });
            }

            LstDbs.IsEnabled = TagOf(CmbScope, "user") == "selected";
        }

        private void ApplySelected(string selected)
        {
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            var parts = selected.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < LstDbs.Items.Count; i++)
            {
                var box = LstDbs.Items[i] as CheckBox;
                if (box == null)
                {
                    continue;
                }

                var name = Convert.ToString(box.Content);
                for (var j = 0; j < parts.Length; j++)
                {
                    if (string.Equals(name, parts[j].Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        box.IsChecked = true;
                        break;
                    }
                }
            }
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            var inst = CmbInstance.SelectedItem as MssqlInstanceDto;
            DateTime nextLocal;
            if (!DateTimePick.TryGetDateTime(DtNext, TmNext, out nextLocal))
            {
                MessageBox.Show(this, "请选择下次执行的日期和时间。", "校验");
                return;
            }

            int retry, interval;
            if (!int.TryParse(TxtRetry.Text, out retry) || !int.TryParse(TxtInterval.Text, out interval))
            {
                MessageBox.Show(this, "重计时/间隔必须是数字。", "校验");
                return;
            }

            var task = new MssqlTaskDto
            {
                Id = _editId,
                Name = (TxtName.Text ?? "").Trim(),
                InstanceId = inst == null ? 0 : inst.Id,
                Enabled = ChkEnabled.IsChecked == true,
                DbScope = TagOf(CmbScope, "user"),
                SelectedDbs = CollectSelected(),
                RetryMinutes = retry,
                CycleKind = TagOf(CmbCycle, "day"),
                CycleInterval = interval,
                NextRunUtc = DateTime.SpecifyKind(nextLocal, DateTimeKind.Local).ToUniversalTime(),
                DoNetwork = ChkNetwork.IsChecked == true,
                DoFtp = ChkFtp.IsChecked == true,
                DoCloud = _ready.Count > 0 && ChkCloud.IsChecked == true,
                CloudProvider = CloudChoice.Resolve(_ready, ChkCloud.IsChecked == true, TagOf(CmbCloud, _ready.Count > 0 ? _ready[0] : "qiniu")),
                DoDeleteNow = ChkDelete.IsChecked == true,
                DoEmail = ChkEmail.IsChecked == true
            };

            var error = MssqlTaskStore.Validate(task);
            if (error != null)
            {
                MessageBox.Show(this, error, "校验");
                return;
            }

            Task = task;
            DialogResult = true;
        }

        private string CollectSelected()
        {
            var names = new List<string>();
            for (var i = 0; i < LstDbs.Items.Count; i++)
            {
                var box = LstDbs.Items[i] as CheckBox;
                if (box != null && box.IsChecked == true)
                {
                    names.Add(Convert.ToString(box.Content));
                }
            }

            return string.Join(",", names.ToArray());
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
        }
    }
}
