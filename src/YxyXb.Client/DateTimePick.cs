using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MaterialDesignThemes.Wpf;

namespace YxyXb.Client.Dialogs
{
    internal static class DateTimePick
    {
        public static DatePicker DateBox()
        {
            return new DatePicker
            {
                Width = 180,
                Margin = new Thickness(0, 4, 8, 4),
                FirstDayOfWeek = DayOfWeek.Monday,
                Language = XmlLanguage.GetLanguage("zh-CN"),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TimePicker TimeBox()
        {
            return new TimePicker
            {
                Width = 140,
                Is24Hours = true,
                WithSeconds = false,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static UIElement Combine(DatePicker date, TimePicker time)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(date);
            row.Children.Add(time);
            return row;
        }

        public static void SetDateTime(DatePicker date, TimePicker time, DateTime local)
        {
            date.SelectedDate = local.Date;
            time.SelectedTime = local;
        }

        public static bool TryGetDateTime(DatePicker date, TimePicker time, out DateTime local)
        {
            if (date == null || time == null || !date.SelectedDate.HasValue || !time.SelectedTime.HasValue)
            {
                local = default(DateTime);
                return false;
            }

            local = date.SelectedDate.Value.Date.Add(time.SelectedTime.Value.TimeOfDay);
            return true;
        }

        public static void SetHmm(TimePicker time, string hhmm)
        {
            DateTime parsed;
            if (DateTime.TryParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                || DateTime.TryParseExact(hhmm, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                time.SelectedTime = DateTime.Today.Add(parsed.TimeOfDay);
                return;
            }

            time.SelectedTime = DateTime.Today.AddHours(0).AddMinutes(30);
        }

        public static string GetHmm(TimePicker time, string fallback)
        {
            if (time == null || !time.SelectedTime.HasValue)
            {
                return fallback;
            }

            return time.SelectedTime.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
