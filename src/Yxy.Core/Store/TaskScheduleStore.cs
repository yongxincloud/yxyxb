using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;

namespace Yxy.Core.Store
{
    public static class TaskScheduleStore
    {
        public static IList<ScheduledItemDto> GetUpcoming(SqliteDb db, int take)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            if (take <= 0)
            {
                take = 20;
            }

            var sql = @"
SELECT kind, id, name, enabled, next_run_utc FROM (
SELECT 'file' AS kind, id, name, enabled, next_run_utc FROM file_tasks WHERE enabled = 1
UNION ALL
SELECT 'mssql' AS kind, id, name, enabled, next_run_utc FROM mssql_tasks WHERE enabled = 1
UNION ALL
SELECT 'media' AS kind, id, name, enabled, next_run_utc FROM media_tasks WHERE enabled = 1
) t
ORDER BY CASE WHEN next_run_utc IS NULL THEN 1 ELSE 0 END, next_run_utc
LIMIT " + take.ToString(CultureInfo.InvariantCulture);

            var table = db.Query(sql);
            var list = new List<ScheduledItemDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(ReadItem(row));
            }

            return list;
        }

        public static HomeCountsDto GetHomeCounts(SqliteDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            var today = DateTime.UtcNow.Date.ToString("o", CultureInfo.InvariantCulture);
            return new HomeCountsDto
            {
                FileTaskCount = ToInt(db.ExecuteScalar("SELECT COUNT(*) FROM file_tasks")),
                FileExecutedCount = ToInt(db.ExecuteScalar("SELECT COALESCE(SUM(run_count), 0) FROM file_tasks")),
                MssqlTaskCount = ToInt(db.ExecuteScalar("SELECT COUNT(*) FROM mssql_tasks")),
                MssqlExecutedCount = ToInt(db.ExecuteScalar("SELECT COALESCE(SUM(run_count), 0) FROM mssql_tasks")),
                TodayErrorCount = ToInt(db.ExecuteScalar(
                    "SELECT COUNT(*) FROM logs WHERE result = 'error' AND created_utc >= @d",
                    new System.Data.SQLite.SQLiteParameter("@d", today))),
                AutoLogCount = ToInt(db.ExecuteScalar("SELECT COUNT(*) FROM logs WHERE mode = 'auto'")),
                ManualLogCount = ToInt(db.ExecuteScalar("SELECT COUNT(*) FROM logs WHERE mode = 'manual'"))
            };
        }

        private static ScheduledItemDto ReadItem(DataRow row)
        {
            DateTime? next = null;
            var nextText = row["next_run_utc"] == DBNull.Value ? null : Convert.ToString(row["next_run_utc"]);
            if (!string.IsNullOrEmpty(nextText))
            {
                next = SqliteDb.ParseUtc(nextText);
            }

            return new ScheduledItemDto
            {
                Kind = Convert.ToString(row["kind"]),
                Id = Convert.ToInt64(row["id"]),
                Name = Convert.ToString(row["name"]),
                Enabled = Convert.ToInt32(row["enabled"]) != 0,
                NextRunUtc = next
            };
        }

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
