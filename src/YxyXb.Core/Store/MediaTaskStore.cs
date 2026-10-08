using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Schedule;

namespace YxyXb.Core.Store
{
    public static class MediaTaskStore
    {
        public static IList<MediaTaskDto> List(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM media_tasks ORDER BY id");
            var list = new List<MediaTaskDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static IList<MediaTaskDto> ListEnabled(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM media_tasks WHERE enabled=1 ORDER BY id");
            var list = new List<MediaTaskDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static MediaTaskDto GetById(SqliteDb db, long id)
        {
            var table = db.Query("SELECT * FROM media_tasks WHERE id=@id", new SQLiteParameter("@id", id));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static void ForceDueNow(SqliteDb db, long id)
        {
            SetNextRun(db, id, DateTime.UtcNow);
        }

        public static void SetNextRun(SqliteDb db, long id, DateTime utc)
        {
            db.Execute(
                "UPDATE media_tasks SET next_run_utc=@n, waiting_since_utc=NULL WHERE id=@id",
                new SQLiteParameter("@n", utc.ToString("o")),
                new SQLiteParameter("@id", id));
        }

        public static long Insert(SqliteDb db, MediaTaskDto task)
        {
            db.Execute(
                @"INSERT INTO media_tasks (name,media_id,enabled,source_path,target_folder,window_start,window_end,wait_minutes,
                  retain_versions,retain_days,conflict_mode,isolate_after,isolate_level,use_sha256,
                  cycle_kind,cycle_interval,next_run_utc,created_utc)
                  VALUES (@n,@m,@e,@sp,@tf,@ws,@we,@w,@rv,@rd,@cm,@ia,@il,@sh,@ck,@ci,@nr,@u)",
                new SQLiteParameter("@n", task.Name),
                new SQLiteParameter("@m", task.MediaId),
                new SQLiteParameter("@e", task.Enabled ? 1 : 0),
                new SQLiteParameter("@sp", string.IsNullOrWhiteSpace(task.SourcePath) ? (object)DBNull.Value : task.SourcePath.Trim()),
                new SQLiteParameter("@tf", string.IsNullOrEmpty(task.TargetFolder) ? "用心云循备" : task.TargetFolder),
                new SQLiteParameter("@ws", task.WindowStart),
                new SQLiteParameter("@we", task.WindowEnd),
                new SQLiteParameter("@w", task.WaitMinutes),
                new SQLiteParameter("@rv", task.RetainVersions < 1 ? 7 : task.RetainVersions),
                new SQLiteParameter("@rd", task.RetainDays < 1 ? 7 : task.RetainDays),
                new SQLiteParameter("@cm", string.IsNullOrEmpty(task.ConflictMode) ? "overwrite" : task.ConflictMode),
                new SQLiteParameter("@ia", task.IsolateAfter ? 1 : 0),
                new SQLiteParameter("@il", task.IsolateLevel < 1 ? 2 : task.IsolateLevel),
                new SQLiteParameter("@sh", task.UseSha256 ? 1 : 0),
                new SQLiteParameter("@ck", task.CycleKind ?? "day"),
                new SQLiteParameter("@ci", task.CycleInterval < 1 ? 1 : task.CycleInterval),
                new SQLiteParameter("@nr", task.NextRunUtc.HasValue ? (object)task.NextRunUtc.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@u", SqliteDb.UtcNowText()));
            return Convert.ToInt64(db.ExecuteScalar("SELECT last_insert_rowid()"));
        }

        public static void Update(SqliteDb db, MediaTaskDto task)
        {
            db.Execute(
                @"UPDATE media_tasks SET name=@n, media_id=@m, enabled=@e, source_path=@sp, target_folder=@tf, window_start=@ws, window_end=@we,
                  wait_minutes=@w, retain_versions=@rv, retain_days=@rd, conflict_mode=@cm, isolate_after=@ia, isolate_level=@il,
                  use_sha256=@sh, cycle_kind=@ck, cycle_interval=@ci, next_run_utc=@nr
                  WHERE id=@id",
                new SQLiteParameter("@n", task.Name),
                new SQLiteParameter("@m", task.MediaId),
                new SQLiteParameter("@e", task.Enabled ? 1 : 0),
                new SQLiteParameter("@sp", string.IsNullOrWhiteSpace(task.SourcePath) ? (object)DBNull.Value : task.SourcePath.Trim()),
                new SQLiteParameter("@tf", string.IsNullOrEmpty(task.TargetFolder) ? "用心云循备" : task.TargetFolder),
                new SQLiteParameter("@ws", task.WindowStart),
                new SQLiteParameter("@we", task.WindowEnd),
                new SQLiteParameter("@w", task.WaitMinutes),
                new SQLiteParameter("@rv", task.RetainVersions < 1 ? 7 : task.RetainVersions),
                new SQLiteParameter("@rd", task.RetainDays < 1 ? 7 : task.RetainDays),
                new SQLiteParameter("@cm", string.IsNullOrEmpty(task.ConflictMode) ? "overwrite" : task.ConflictMode),
                new SQLiteParameter("@ia", task.IsolateAfter ? 1 : 0),
                new SQLiteParameter("@il", task.IsolateLevel < 1 ? 2 : task.IsolateLevel),
                new SQLiteParameter("@sh", task.UseSha256 ? 1 : 0),
                new SQLiteParameter("@ck", task.CycleKind ?? "day"),
                new SQLiteParameter("@ci", task.CycleInterval < 1 ? 1 : task.CycleInterval),
                new SQLiteParameter("@nr", task.NextRunUtc.HasValue ? (object)task.NextRunUtc.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@id", task.Id));
        }

        public static void Delete(SqliteDb db, long id)
        {
            db.Execute("DELETE FROM media_tasks WHERE id=@id", new SQLiteParameter("@id", id));
        }

        public static void SetWaiting(SqliteDb db, long id, DateTime? sinceUtc)
        {
            db.Execute(
                "UPDATE media_tasks SET waiting_since_utc=@w WHERE id=@id",
                new SQLiteParameter("@w", sinceUtc.HasValue ? (object)sinceUtc.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@id", id));
        }

        public static void MarkResult(SqliteDb db, MediaTaskDto task, bool ok, DateTime utcNow)
        {
            DateTime? next = ScheduleCalculator.NextAfter(task.CycleKind, task.CycleInterval, utcNow);
            var enabled = task.Enabled;
            if (ok && !next.HasValue)
            {
                enabled = false;
            }

            if (!ok)
            {
                next = utcNow.AddMinutes(5);
            }

            db.Execute(
                "UPDATE media_tasks SET last_run_utc=@lr, next_run_utc=@nr, run_count=run_count+1, enabled=@en, waiting_since_utc=NULL WHERE id=@id",
                new SQLiteParameter("@lr", utcNow.ToString("o")),
                new SQLiteParameter("@nr", next.HasValue ? (object)next.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@en", enabled ? 1 : 0),
                new SQLiteParameter("@id", task.Id));
        }

        public static string Validate(MediaTaskDto task, string bakPath)
        {
            if (task == null)
            {
                return "任务为空。";
            }

            if (string.IsNullOrWhiteSpace(task.Name))
            {
                return "请填写任务名称。";
            }

            if (task.MediaId <= 0)
            {
                return "请选择已登记介质。";
            }

            if (string.IsNullOrWhiteSpace(task.SourcePath))
            {
                return "请填写扫描源。";
            }

            if (!System.IO.Directory.Exists(task.SourcePath))
            {
                return "扫描源文件夹不存在。";
            }

            var win = WindowClock.Validate(task.WindowStart, task.WindowEnd);
            if (win != null)
            {
                return win;
            }

            if (task.RetainDays < 1 || task.RetainVersions < 1)
            {
                return "保留天数和版本数最小为 1。";
            }

            return ScheduleCalculator.Validate(task.CycleKind, task.CycleInterval, task.NextRunUtc);
        }

        private static MediaTaskDto Read(DataRow row)
        {
            return new MediaTaskDto
            {
                Id = Convert.ToInt64(row["id"]),
                Name = Convert.ToString(row["name"]),
                MediaId = Convert.ToInt64(row["media_id"]),
                Enabled = Convert.ToInt32(row["enabled"]) != 0,
                SourcePath = row.Table.Columns.Contains("source_path") && row["source_path"] != DBNull.Value
                    ? Convert.ToString(row["source_path"])
                    : null,
                TargetFolder = Convert.ToString(row["target_folder"]),
                WindowStart = Convert.ToString(row["window_start"]),
                WindowEnd = Convert.ToString(row["window_end"]),
                WaitMinutes = Convert.ToInt32(row["wait_minutes"]),
                RetainVersions = Convert.ToInt32(row["retain_versions"]),
                RetainDays = Convert.ToInt32(row["retain_days"]),
                ConflictMode = Convert.ToString(row["conflict_mode"]),
                IsolateAfter = Convert.ToInt32(row["isolate_after"]) != 0,
                IsolateLevel = Convert.ToInt32(row["isolate_level"]),
                UseSha256 = Convert.ToInt32(row["use_sha256"]) != 0,
                CycleKind = Convert.ToString(row["cycle_kind"]),
                CycleInterval = Convert.ToInt32(row["cycle_interval"]),
                NextRunUtc = Parse(row["next_run_utc"]),
                LastRunUtc = Parse(row["last_run_utc"]),
                WaitingSinceUtc = row.Table.Columns.Contains("waiting_since_utc") ? Parse(row["waiting_since_utc"]) : null,
                RunCount = Convert.ToInt32(row["run_count"])
            };
        }

        private static DateTime? Parse(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return SqliteDb.ParseUtc(Convert.ToString(value));
        }
    }
}
