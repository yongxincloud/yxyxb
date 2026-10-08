using System;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;

namespace YxyXb.Core.License
{
    public static class TrialClock
    {
        public const int TrialDays = 30;

        public static LicenseStatusDto Get(SqliteDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            var firstText = db.ExecuteScalar("SELECT first_run_utc FROM license WHERE id = 1") as string;
            var account = db.ExecuteScalar("SELECT account FROM license WHERE id = 1") as string;
            if (string.IsNullOrEmpty(firstText))
            {
                firstText = SqliteDb.UtcNowText();
                db.Execute("UPDATE license SET first_run_utc = @utc WHERE id = 1", new System.Data.SQLite.SQLiteParameter("@utc", firstText));
            }

            var first = SqliteDb.ParseUtc(firstText);
            var elapsed = (DateTime.UtcNow - first).TotalDays;
            var remaining = TrialDays - (int)Math.Floor(elapsed);
            if (remaining < 0)
            {
                remaining = 0;
            }

            var expired = elapsed >= TrialDays;
            return new LicenseStatusDto
            {
                FirstRunUtc = first,
                RemainingDays = remaining,
                IsExpired = expired,
                Account = account,
                HasCachedLicense = !string.IsNullOrEmpty(account)
            };
        }
    }
}
