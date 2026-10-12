using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;

namespace YxyXb.Core.Store
{
    public static class FileTaskStore
    {
        public static IList<FileTaskDto> List(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM file_tasks ORDER BY sort_order, id");
            var list = new List<FileTaskDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static FileTaskDto GetById(SqliteDb db, long id)
        {
            var table = db.Query("SELECT * FROM file_tasks WHERE id=@id", new SQLiteParameter("@id", id));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static IList<FileTaskDto> GetDue(SqliteDb db, DateTime utcNow)
        {
            var table = db.Query(
                "SELECT * FROM file_tasks WHERE enabled=1 AND next_run_utc IS NOT NULL AND next_run_utc<=@n" + TaskRunLock.DueLockSql + " ORDER BY sort_order, id",
                new SQLiteParameter("@n", utcNow.ToString("o")),
                TaskRunLock.StaleParam(utcNow));
            var list = new List<FileTaskDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static long Insert(SqliteDb db, FileTaskDto task)
        {
            db.Execute(
                @"INSERT INTO file_tasks (name,enabled,source_kind,source_path,retry_minutes,sort_order,cycle_kind,cycle_interval,
                  next_run_utc,do_network,do_ftp,do_cloud,cloud_provider,do_delete_now,do_email,post_program,created_utc)
                  VALUES (@name,@en,@sk,@sp,@rm,@so,@ck,@ci,@nr,@dn,@df,@dc,@cp,@dd,@de,@pp,@utc)",
                Params(task, true));
            var id = db.ExecuteScalar("SELECT last_insert_rowid()");
            return Convert.ToInt64(id);
        }

        public static void Update(SqliteDb db, FileTaskDto task)
        {
            db.Execute(
                @"UPDATE file_tasks SET name=@name, enabled=@en, source_kind=@sk, source_path=@sp, retry_minutes=@rm,
                  sort_order=@so, cycle_kind=@ck, cycle_interval=@ci, next_run_utc=@nr, do_network=@dn, do_ftp=@df,
                  do_cloud=@dc, cloud_provider=@cp, do_delete_now=@dd, do_email=@de, post_program=@pp
                  WHERE id=@id",
                new SQLiteParameter("@name", task.Name),
                new SQLiteParameter("@en", task.Enabled ? 1 : 0),
                new SQLiteParameter("@sk", task.SourceKind ?? "local"),
                new SQLiteParameter("@sp", task.SourcePath ?? ""),
                new SQLiteParameter("@rm", task.RetryMinutes < 1 ? 5 : task.RetryMinutes),
                new SQLiteParameter("@so", task.SortOrder),
                new SQLiteParameter("@ck", task.CycleKind ?? "day"),
                new SQLiteParameter("@ci", task.CycleInterval < 1 ? 1 : task.CycleInterval),
                new SQLiteParameter("@nr", task.NextRunUtc.HasValue ? (object)task.NextRunUtc.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@dn", task.DoNetwork ? 1 : 0),
                new SQLiteParameter("@df", task.DoFtp ? 1 : 0),
                new SQLiteParameter("@dc", task.DoCloud ? 1 : 0),
                new SQLiteParameter("@cp", (object)task.CloudProvider ?? DBNull.Value),
                new SQLiteParameter("@dd", task.DoDeleteNow ? 1 : 0),
                new SQLiteParameter("@de", task.DoEmail ? 1 : 0),
                new SQLiteParameter("@pp", (object)task.PostProgram ?? DBNull.Value),
                new SQLiteParameter("@id", task.Id));
        }

        public static void SavePending(SqliteDb db, long id, string json)
        {
            db.Execute(
                "UPDATE file_tasks SET pending_json=@p WHERE id=@id",
                new SQLiteParameter("@p", (object)json ?? DBNull.Value),
                new SQLiteParameter("@id", id));
        }

        public static string Delete(SqliteDb db, long id)
        {
            var task = GetById(db, id);
            if (task == null)
            {
                return null;
            }

            var error = PendingFiles.DeleteUnpublished(task.PendingJson, task.DoNetwork, task.DoFtp, task.DoCloud);
            if (error != null)
            {
                return error;
            }

            db.Execute("DELETE FROM file_tasks WHERE id=@id", new SQLiteParameter("@id", id));
            return null;
        }

        public static bool TryClaim(SqliteDb db, long id, DateTime utcNow)
        {
            return TaskRunLock.TryClaim(db, "file_tasks", id, utcNow);
        }

        public static void MarkResult(SqliteDb db, FileTaskDto task, bool ok, DateTime utcNow)
        {
            DateTime? next = null;
            var enabled = task.Enabled;
            if (ok)
            {
                next = YxyXb.Core.Schedule.ScheduleCalculator.NextAfter(task.CycleKind, task.CycleInterval, utcNow);
                if (!next.HasValue)
                {
                    enabled = false;
                }
            }
            else
            {
                var retry = task.RetryMinutes < 1 ? 5 : task.RetryMinutes;
                next = utcNow.AddMinutes(retry);
            }

            db.Execute(
                "UPDATE file_tasks SET last_run_utc=@lr, next_run_utc=@nr, run_count=run_count+1, enabled=@en, run_lock_utc=NULL WHERE id=@id",
                new SQLiteParameter("@lr", utcNow.ToString("o")),
                new SQLiteParameter("@nr", next.HasValue ? (object)next.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@en", enabled ? 1 : 0),
                new SQLiteParameter("@id", task.Id));
        }

        private static FileTaskDto Read(DataRow row)
        {
            return new FileTaskDto
            {
                Id = Convert.ToInt64(row["id"]),
                Name = Convert.ToString(row["name"]),
                Enabled = Convert.ToInt32(row["enabled"]) != 0,
                SourceKind = Convert.ToString(row["source_kind"]),
                SourcePath = Convert.ToString(row["source_path"]),
                RetryMinutes = Convert.ToInt32(row["retry_minutes"]),
                SortOrder = Convert.ToInt32(row["sort_order"]),
                CycleKind = Convert.ToString(row["cycle_kind"]),
                CycleInterval = Convert.ToInt32(row["cycle_interval"]),
                NextRunUtc = ParseTime(row["next_run_utc"]),
                LastRunUtc = ParseTime(row["last_run_utc"]),
                RunCount = Convert.ToInt32(row["run_count"]),
                DoNetwork = Convert.ToInt32(row["do_network"]) != 0,
                DoFtp = Convert.ToInt32(row["do_ftp"]) != 0,
                DoCloud = Convert.ToInt32(row["do_cloud"]) != 0,
                CloudProvider = row["cloud_provider"] == DBNull.Value ? null : Convert.ToString(row["cloud_provider"]),
                DoDeleteNow = Convert.ToInt32(row["do_delete_now"]) != 0,
                DoEmail = Convert.ToInt32(row["do_email"]) != 0,
                PostProgram = row["post_program"] == DBNull.Value ? null : Convert.ToString(row["post_program"]),
                PendingJson = row["pending_json"] == DBNull.Value ? null : Convert.ToString(row["pending_json"])
            };
        }

        private static DateTime? ParseTime(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return SqliteDb.ParseUtc(Convert.ToString(value));
        }

        private static SQLiteParameter[] Params(FileTaskDto task, bool insert)
        {
            return new[]
            {
                new SQLiteParameter("@name", task.Name),
                new SQLiteParameter("@en", task.Enabled ? 1 : 0),
                new SQLiteParameter("@sk", task.SourceKind ?? "local"),
                new SQLiteParameter("@sp", task.SourcePath ?? ""),
                new SQLiteParameter("@rm", task.RetryMinutes < 1 ? 5 : task.RetryMinutes),
                new SQLiteParameter("@so", task.SortOrder),
                new SQLiteParameter("@ck", task.CycleKind ?? "day"),
                new SQLiteParameter("@ci", task.CycleInterval < 1 ? 1 : task.CycleInterval),
                new SQLiteParameter("@nr", task.NextRunUtc.HasValue ? (object)task.NextRunUtc.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@dn", task.DoNetwork ? 1 : 0),
                new SQLiteParameter("@df", task.DoFtp ? 1 : 0),
                new SQLiteParameter("@dc", task.DoCloud ? 1 : 0),
                new SQLiteParameter("@cp", (object)task.CloudProvider ?? DBNull.Value),
                new SQLiteParameter("@dd", task.DoDeleteNow ? 1 : 0),
                new SQLiteParameter("@de", task.DoEmail ? 1 : 0),
                new SQLiteParameter("@pp", (object)task.PostProgram ?? DBNull.Value),
                new SQLiteParameter("@utc", SqliteDb.UtcNowText())
            };
        }
    }
}
