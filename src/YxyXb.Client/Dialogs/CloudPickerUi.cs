using System.Collections.Generic;
using System.Windows.Controls;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Client.Dialogs
{
    internal static class CloudPickerUi
    {
        public static void Bind(CheckBox chk, ComboBox cmb, IList<string> ready)
        {
            cmb.Items.Clear();
            var any = ready != null && ready.Count > 0;
            chk.IsEnabled = any;
            cmb.IsEnabled = any;
            chk.Content = any ? "云上传" : "云上传（请先在「云备份」保存并启用一个云）";
            if (!any)
            {
                return;
            }

            for (var i = 0; i < ready.Count; i++)
            {
                cmb.Items.Add(new ComboBoxItem
                {
                    Content = CloudChoice.Label(ready[i]),
                    Tag = ready[i],
                    IsSelected = i == 0
                });
            }
        }
    }
}
