using System;
using System.Data.SQLite;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Security;

namespace YxyXb.Core.Store
{
    public static class SettingsStore
    {
        public static AppSettingsDto Get(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM settings WHERE id = 1");
            if (table.Rows.Count == 0)
            {
                return new AppSettingsDto
                {
                    BakPath = AppPaths.BakDirectory,
                    SoftwareId = AppPaths.ProductPathName,
                    NameFlags = NameParts.DefaultFlags,
                    ZipLevel = 5,
                    Theme = "light",
                    LocalRetainDays = 7,
                    PurgeTime = "00:30",
                    UploadParallel = 1,
                    LogRetainDays = 30
                };
            }

            var row = table.Rows[0];
            return new AppSettingsDto
            {
                BakPath = Convert.ToString(row["bak_path"]),
                SoftwareId = Convert.ToString(row["software_id"]),
                NameFlags = GetInt(row, "name_flags", NameParts.DefaultFlags),
                ZipLevel = GetInt(row, "zip_level", 5),
                UploadMinKbps = GetInt(row, "upload_min_kbps", 0),
                UploadMaxKbps = GetInt(row, "upload_max_kbps", 0),
                UploadParallel = ClampParallel(GetInt(row, "upload_parallel", 1)),
                ZipPassword = SecretBox.Unprotect(GetStr(row, "zip_password_protected")),
                StartWithWindows = GetInt(row, "start_with_windows", 0) != 0,
                CloseToTray = GetInt(row, "close_to_tray", 0) != 0,
                Theme = NormalizeTheme(GetStr(row, "theme")),
                LockPassword = SecretBox.Unprotect(GetStr(row, "lock_password_protected")),
                EngineAdvanced = GetInt(row, "engine_advanced", 0) != 0,
                LocalRetainDays = GetInt(row, "local_retain_days", 7),
                NetworkRetainDays = GetInt(row, "network_retain_days", 7),
                FtpRetainDays = GetInt(row, "ftp_retain_days", 7),
                QiniuRetainDays = GetInt(row, "qiniu_retain_days", 7),
                BaiduRetainDays = GetInt(row, "baidu_retain_days", 7),
                PurgeLocal = GetInt(row, "purge_local", 0) != 0,
                PurgeNetwork = GetInt(row, "purge_network", 0) != 0,
                PurgeFtp = GetInt(row, "purge_ftp", 0) != 0,
                PurgeQiniu = GetInt(row, "purge_qiniu", 0) != 0,
                PurgeBaidu = GetInt(row, "purge_baidu", 0) != 0,
                PurgeTime = GetStr(row, "purge_time") ?? "00:30",
                LastPurgeUtc = ParseTime(GetStr(row, "last_purge_utc")),
                LogRetainDays = ClampLogDays(GetInt(row, "log_retain_days", 30))
            };
        }

        public static void Save(SqliteDb db, AppSettingsDto settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            db.Execute(
                @"UPDATE settings SET bak_path=@bak, software_id=@sid, name_flags=@nf, zip_level=@zl, upload_min_kbps=@umin, upload_max_kbps=@umax, upload_parallel=@upar, zip_password_protected=@zp,
                  start_with_windows=@sw, close_to_tray=@ct, theme=@theme, lock_password_protected=@lp, engine_advanced=@ea,
                  local_retain_days=@rd, network_retain_days=@nrd, ftp_retain_days=@frd, qiniu_retain_days=@qrd, baidu_retain_days=@brd,
                  purge_local=@pl, purge_network=@pn, purge_ftp=@pf, purge_qiniu=@pq, purge_baidu=@pb, purge_time=@pt, log_retain_days=@lrd, updated_utc=@utc WHERE id=1",
                new SQLiteParameter("@bak", settings.BakPath ?? AppPaths.BakDirectory),
                new SQLiteParameter("@sid", string.IsNullOrEmpty(settings.SoftwareId) ? AppPaths.ProductPathName : settings.SoftwareId),
                new SQLiteParameter("@nf", settings.NameFlags == 0 ? NameParts.DefaultFlags : settings.NameFlags),
                new SQLiteParameter("@zl", settings.ZipLevel),
                new SQLiteParameter("@umin", settings.UploadMinKbps < 0 ? 0 : settings.UploadMinKbps),
                new SQLiteParameter("@umax", settings.UploadMaxKbps < 0 ? 0 : settings.UploadMaxKbps),
                new SQLiteParameter("@upar", ClampParallel(settings.UploadParallel)),
                new SQLiteParameter("@zp", (object)SecretBox.Protect(settings.ZipPassword) ?? DBNull.Value),
                new SQLiteParameter("@sw", settings.StartWithWindows ? 1 : 0),
                new SQLiteParameter("@ct", settings.CloseToTray ? 1 : 0),
                new SQLiteParameter("@theme", NormalizeTheme(settings.Theme)),
                new SQLiteParameter("@lp", (object)SecretBox.Protect(settings.LockPassword) ?? DBNull.Value),
                new SQLiteParameter("@ea", settings.EngineAdvanced ? 1 : 0),
                new SQLiteParameter("@rd", settings.LocalRetainDays <= 0 ? 7 : settings.LocalRetainDays),
                new SQLiteParameter("@nrd", settings.NetworkRetainDays <= 0 ? 7 : settings.NetworkRetainDays),
                new SQLiteParameter("@frd", settings.FtpRetainDays <= 0 ? 7 : settings.FtpRetainDays),
                new SQLiteParameter("@qrd", settings.QiniuRetainDays <= 0 ? 7 : settings.QiniuRetainDays),
                new SQLiteParameter("@brd", settings.BaiduRetainDays <= 0 ? 7 : settings.BaiduRetainDays),
                new SQLiteParameter("@pl", settings.PurgeLocal ? 1 : 0),
                new SQLiteParameter("@pn", settings.PurgeNetwork ? 1 : 0),
                new SQLiteParameter("@pf", settings.PurgeFtp ? 1 : 0),
                new SQLiteParameter("@pq", settings.PurgeQiniu ? 1 : 0),
                new SQLiteParameter("@pb", settings.PurgeBaidu ? 1 : 0),
                new SQLiteParameter("@pt", string.IsNullOrEmpty(settings.PurgeTime) ? "00:30" : settings.PurgeTime),
                new SQLiteParameter("@lrd", ClampLogDays(settings.LogRetainDays)),
                new SQLiteParameter("@utc", SqliteDb.UtcNowText()));
        }

        public static void MarkPurged(SqliteDb db, DateTime utc)
        {
            db.Execute(
                "UPDATE settings SET last_purge_utc=@p WHERE id=1",
                new SQLiteParameter("@p", utc.ToString("o")));
        }

        public static T GetXfer<T>(SqliteDb db, string kind) where T : class, new()
        {
            var json = db.ExecuteScalar("SELECT fields_protected FROM xfer_accounts WHERE kind=@k LIMIT 1", new SQLiteParameter("@k", kind)) as string;
            var raw = SecretBox.Unprotect(json);
            var value = JsonUtil.FromJson<T>(raw);
            return value ?? new T();
        }

        public static void SaveXfer(SqliteDb db, string kind, object fields)
        {
            var json = SecretBox.Protect(JsonUtil.ToJson(fields));
            var exists = db.ExecuteScalar("SELECT id FROM xfer_accounts WHERE kind=@k LIMIT 1", new SQLiteParameter("@k", kind));
            if (exists == null || exists == DBNull.Value)
            {
                db.Execute(
                    "INSERT INTO xfer_accounts (kind, fields_protected, created_utc) VALUES (@k,@f,@u)",
                    new SQLiteParameter("@k", kind),
                    new SQLiteParameter("@f", (object)json ?? DBNull.Value),
                    new SQLiteParameter("@u", SqliteDb.UtcNowText()));
            }
            else
            {
                db.Execute(
                    "UPDATE xfer_accounts SET fields_protected=@f WHERE kind=@k",
                    new SQLiteParameter("@f", (object)json ?? DBNull.Value),
                    new SQLiteParameter("@k", kind));
            }
        }

        private static int ClampParallel(int v)
        {
            if (v < 1)
            {
                return 1;
            }

            if (v > 2)
            {
                return 2;
            }

            return v;
        }

        private static int ClampLogDays(int v)
        {
            return v < 1 ? 1 : v;
        }

        private static int GetInt(System.Data.DataRow row, string col, int fallback)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            {
                return fallback;
            }

            return Convert.ToInt32(row[col]);
        }

        private static string GetStr(System.Data.DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            {
                return null;
            }

            return Convert.ToString(row[col]);
        }

        private static string NormalizeTheme(string theme)
        {
            if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
            {
                return "dark";
            }

            if (string.Equals(theme, "win11", StringComparison.OrdinalIgnoreCase))
            {
                return "win11";
            }

            if (string.Equals(theme, "mac", StringComparison.OrdinalIgnoreCase))
            {
                return "mac";
            }

            if (string.Equals(theme, "yongxin", StringComparison.OrdinalIgnoreCase))
            {
                return "yongxin";
            }

            return "light";
        }

        private static DateTime? ParseTime(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return SqliteDb.ParseUtc(text);
        }
    }
}
