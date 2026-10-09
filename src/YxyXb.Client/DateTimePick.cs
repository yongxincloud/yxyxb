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
            DateTime day;
            TimeSpan clock;
            if (date == null || time == null || !TryReadDate(date, out day) || !TryReadClock(time, out clock))
            {
                local = default(DateTime);
                return false;
            }

            local = day.Date.Add(clock);
            return true;
        }

        private static bool TryReadDate(DatePicker date, out DateTime day)
        {
            DateTime parsed;
            var text = date.Text == null ? string.Empty : date.Text.Trim();
            if (text.Length > 0 && DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            {
                day = parsed.Date;
                return true;
            }

            if (date.SelectedDate.HasValue)
            {
                day = date.SelectedDate.Value.Date;
                return true;
            }

            day = default(DateTime);
            return false;
        }

        private static bool TryReadClock(TimePicker time, out TimeSpan clock)
        {
            DateTime parsed;
            var text = time.Text == null ? string.Empty : time.Text.Trim();
            var formats = new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss" };
            if (text.Length > 0
                && (DateTime.TryParseExact(text, formats, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)
                    || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out parsed)))
            {
                clock = parsed.TimeOfDay;
                return true;
            }

            if (time.SelectedTime.HasValue)
            {
                clock = time.SelectedTime.Value.TimeOfDay;
                return true;
            }

            clock = default(TimeSpan);
            return false;
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
            TimeSpan clock;
            if (time == null || !TryReadClock(time, out clock))
            {
                return fallback;
            }

            return new DateTime(clock.Ticks).ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
