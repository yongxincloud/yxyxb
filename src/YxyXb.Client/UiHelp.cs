using System.Windows;
using System.Windows.Controls;

namespace YxyXb.Client
{
    public static class UiHelp
    {
        public static void Tip(FrameworkElement control, string text)
        {
            if (control == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            control.ToolTip = text;
            ToolTipService.SetInitialShowDelay(control, 200);
            ToolTipService.SetShowDuration(control, 12000);
            ToolTipService.SetBetweenShowDelay(control, 100);
            ToolTipService.SetShowOnDisabled(control, true);
        }
    }
}
