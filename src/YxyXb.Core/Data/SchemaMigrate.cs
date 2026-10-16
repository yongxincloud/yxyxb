using System.Data.SQLite;

namespace YxyXb.Core.Data
{
    internal static class SchemaMigrate
    {
        public static void Apply(SqliteDb db)
        {
            Add(db, "settings", "zip_level", "INTEGER NOT NULL DEFAULT 5");
            Add(db, "settings", "zip_password_protected", "TEXT");
            Add(db, "settings", "start_with_windows", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "settings", "close_to_tray", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "settings", "lock_password_protected", "TEXT");
            Add(db, "settings", "engine_advanced", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "media", "registered_device_id", "TEXT");
            Add(db, "media", "tamper_pending", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "media", "auto_held", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "media", "letter_cmd", "TEXT");
            Add(db, "media", "letter_result", "TEXT");
            Add(db, "media_tasks", "waiting_since_utc", "TEXT");
            Add(db, "media_tasks", "source_path", "TEXT");
            Add(db, "settings", "last_purge_utc", "TEXT");
            Add(db, "settings", "name_flags", "INTEGER NOT NULL DEFAULT 191");
            Add(db, "settings", "upload_min_kbps", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "settings", "upload_max_kbps", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "settings", "theme", "TEXT NOT NULL DEFAULT 'light'");
            Add(db, "settings", "baidu_retain_days", "INTEGER NOT NULL DEFAULT 10");
            Add(db, "settings", "purge_baidu", "INTEGER NOT NULL DEFAULT 0");
            Add(db, "settings", "upload_parallel", "INTEGER NOT NULL DEFAULT 1");
            Add(db, "settings", "log_retain_days", "INTEGER NOT NULL DEFAULT 30");
            Add(db, "file_tasks", "run_lock_utc", "TEXT");
            Add(db, "mssql_tasks", "run_lock_utc", "TEXT");
            Add(db, "mysql_tasks", "run_lock_utc", "TEXT");
            Add(db, "file_tasks", "pending_json", "TEXT");
            Add(db, "mssql_tasks", "pending_json", "TEXT");
            Add(db, "mysql_tasks", "pending_json", "TEXT");
        }

        private static void Add(SqliteDb db, string table, string column, string decl)
        {
            try
            {
                db.Execute("ALTER TABLE " + table + " ADD COLUMN " + column + " " + decl);
            }
            catch (SQLiteException)
            {
            }
        }
    }
}
