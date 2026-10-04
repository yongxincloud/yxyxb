using System;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Yxy.Client
{
    public static class ThemeService
    {
        public static string Normalize(string themeName)
        {
            if (string.Equals(themeName, "dark", StringComparison.OrdinalIgnoreCase))
            {
                return "dark";
            }

            if (string.Equals(themeName, "win11", StringComparison.OrdinalIgnoreCase))
            {
                return "win11";
            }

            if (string.Equals(themeName, "mac", StringComparison.OrdinalIgnoreCase))
            {
                return "mac";
            }

            if (string.Equals(themeName, "yongxin", StringComparison.OrdinalIgnoreCase))
            {
                return "yongxin";
            }

            return "light";
        }

        public static void Apply(string themeName)
        {
            var name = Normalize(themeName);
            var dark = name == "dark";
            var helper = new PaletteHelper();
            var theme = helper.GetTheme();
            theme.SetBaseTheme((dark ? BaseTheme.Dark : BaseTheme.Light).GetBaseTheme());
            theme.SetPrimaryColor(PrimaryOf(name));
            helper.SetTheme(theme);
            Application.Current.Resources["MaterialDesignFont"] = new FontFamily("Microsoft YaHei");
            Paint(name);
        }

        private static Color PrimaryOf(string name)
        {
            if (name == "win11")
            {
                return Color.FromRgb(0, 103, 192);
            }

            if (name == "mac")
            {
                return Color.FromRgb(0, 122, 255);
            }

            if (name == "dark")
            {
                return Color.FromRgb(34, 211, 238);
            }

            if (name == "yongxin")
            {
                return Color.FromRgb(0xDC, 0x06, 0x06);
            }

            return Color.FromRgb(14, 116, 144);
        }

        private static void Paint(string name)
        {
            if (name == "dark")
            {
                Set("NavBg", "#07111C");
                Set("NavHover", "#123044");
                Set("NavActive", "#0E4A5C");
                Set("NavText", "#E2E8F0");
                Set("TitleText", "#F8FAFC");
                Set("PageBg", "#101418");
                Set("CardBg", "#1B2636");
                Set("CardLine", "#155E75");
                Set("Muted", "#94A3B8");
                Set("Accent", "#22D3EE");
                Set("GridText", "#E2E8F0");
                Set("GridAlt", "#152232");
                Set("GridLine", "#243044");
                Set("GridHeader", "#102433");
                Set("GridSelect", "#155E75");
                return;
            }

            if (name == "win11")
            {
                Set("NavBg", "#F3F3F3");
                Set("NavHover", "#E5E5E5");
                Set("NavActive", "#CCE4F7");
                Set("NavText", "#1A1A1A");
                Set("TitleText", "#1A1A1A");
                Set("PageBg", "#F3F3F3");
                Set("CardBg", "#FFFFFF");
                Set("CardLine", "#E5E5E5");
                Set("Muted", "#5D5D5D");
                Set("Accent", "#0067C0");
                Set("GridText", "#1A1A1A");
                Set("GridAlt", "#F7F7F7");
                Set("GridLine", "#EDEDED");
                Set("GridHeader", "#FAFAFA");
                Set("GridSelect", "#CCE4F7");
                return;
            }

            if (name == "yongxin")
            {
                Set("NavBg", "#1C1214");
                Set("NavHover", "#3A2224");
                Set("NavActive", "#F8E8E8");
                Set("NavText", "#FFFFFF");
                Set("TitleText", "#2A211F");
                Set("PageBg", "#F6F3F1");
                Set("CardBg", "#FFFFFF");
                Set("CardLine", "#E6E0DC");
                Set("Muted", "#8A817C");
                Set("Accent", "#DC0606");
                Set("GridText", "#2A211F");
                Set("GridAlt", "#F6F3F1");
                Set("GridLine", "#E6E0DC");
                Set("GridHeader", "#F8E8E8");
                Set("GridSelect", "#F8E8E8");
                return;
            }

            if (name == "mac")
            {
                Set("NavBg", "#F5F5F7");
                Set("NavHover", "#E8E8ED");
                Set("NavActive", "#D6E7FF");
                Set("NavText", "#1D1D1F");
                Set("TitleText", "#1D1D1F");
                Set("PageBg", "#FFFFFF");
                Set("CardBg", "#FFFFFF");
                Set("CardLine", "#D2D2D7");
                Set("Muted", "#86868B");
                Set("Accent", "#007AFF");
                Set("GridText", "#1D1D1F");
                Set("GridAlt", "#F5F5F7");
                Set("GridLine", "#E5E5EA");
                Set("GridHeader", "#F5F5F7");
                Set("GridSelect", "#D6E7FF");
                return;
            }

            Set("NavBg", "#F4F7FB");
            Set("NavHover", "#E0F2FE");
            Set("NavActive", "#CFFAFE");
            Set("NavText", "#0F172A");
            Set("TitleText", "#0F172A");
            Set("PageBg", "#F7F9FC");
            Set("CardBg", "#FFFFFF");
            Set("CardLine", "#BAE6FD");
            Set("Muted", "#64748B");
            Set("Accent", "#0E7490");
            Set("GridText", "#0F172A");
            Set("GridAlt", "#F0F9FF");
            Set("GridLine", "#E2E8F0");
            Set("GridHeader", "#ECFEFF");
            Set("GridSelect", "#CFFAFE");
        }

        private static void Set(string key, string hex)
        {
            var brush = Application.Current.Resources[key] as SolidColorBrush;
            var color = (Color)ColorConverter.ConvertFromString(hex);
            if (brush == null || brush.IsFrozen)
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
                return;
            }

            brush.Color = color;
        }
    }
}
