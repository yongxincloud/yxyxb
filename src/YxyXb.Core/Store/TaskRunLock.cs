using System;
using System.Data.SQLite;
using YxyXb.Core.Data;

namespace YxyXb.Core.Store
{
    public static class TaskRunLock
    {
        public const int StaleHours = 24;

        public static bool TryClaim(SqliteDb db, string table, long id, DateTime utcNow)
        {
            if (!IsKnownTable(table) || id <= 0)
            {
                return false;
            }

            var now = utcNow.ToString("o");
            var stale = utcNow.AddHours(-StaleHours).ToString("o");
            return db.Execute(
                "UPDATE " + table + " SET run_lock_utc=@now WHERE id=@id AND enabled=1 AND (run_lock_utc IS NULL OR run_lock_utc<@stale)",
                new SQLiteParameter("@now", now),
                new SQLiteParameter("@stale", stale),
                new SQLiteParameter("@id", id)) > 0;
        }

        public static SQLiteParameter StaleParam(DateTime utcNow)
        {
            return new SQLiteParameter("@stale", utcNow.AddHours(-StaleHours).ToString("o"));
        }

        public static string DueLockSql
        {
            get { return " AND (run_lock_utc IS NULL OR run_lock_utc<@stale)"; }
        }

        private static bool IsKnownTable(string table)
        {
            return table == "file_tasks" || table == "mssql_tasks" || table == "mysql_tasks";
        }
    }
}
