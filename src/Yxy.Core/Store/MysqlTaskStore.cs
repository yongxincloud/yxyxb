using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;
using Yxy.Core.Schedule;

namespace Yxy.Core.Store
{
    public static class MysqlTaskStore
    {
        public static IList<MysqlTaskDto> List(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM mysql_tasks ORDER BY sort_order, id");
            var list = new List<MysqlTaskDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static IList<MysqlTaskDto> GetDue(SqliteDb db, DateTime utcNow)
        {
            var table = db.Query(
                "SELECT * FROM mysql_tasks WHERE enabled=1 AND next_run_utc IS NOT NULL AND next_run_utc<=@n" + TaskRunLock.DueLockSql + " ORDER BY sort_order, id",
                new SQLiteParameter("@n", utcNow.ToString("o")),
                TaskRunLock.StaleParam(utcNow));
            var list = new List<MysqlTaskDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static MysqlTaskDto GetById(SqliteDb db, long id)
        {
            var table = db.Query("SELECT * FROM mysql_tasks WHERE id=@id", new SQLiteParameter("@id", id));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static long Insert(SqliteDb db, MysqlTaskDto task)
        {
            db.Execute(
                @"INSERT INTO mysql_tasks (name,instance_id,enabled,db_scope,selected_dbs,retry_minutes,sort_order,
                  cycle_kind,cycle_interval,next_run_utc,do_network,do_ftp,do_cloud,cloud_provider,do_delete_now,do_email,created_utc)
                  VALUES (@n,@i,@e,@sc,@sd,@rm,@so,@ck,@ci,@nr,@dn,@df,@dc,@cp,@dd,@de,@u)",
                Params(task));
            return Convert.ToInt64(db.ExecuteScalar("SELECT last_insert_rowid()"));
        }

        public static void Update(SqliteDb db, MysqlTaskDto task)
        {
            db.Execute(
                @"UPDATE mysql_tasks SET name=@n, instance_id=@i, enabled=@e, db_scope=@sc, selected_dbs=@sd,
                  retry_minutes=@rm, sort_order=@so, cycle_kind=@ck, cycle_interval=@ci, next_run_utc=@nr,
                  do_network=@dn, do_ftp=@df, do_cloud=@dc, cloud_provider=@cp, do_delete_now=@dd, do_email=@de
                  WHERE id=@id",
                new SQLiteParameter("@n", task.Name),
                new SQLiteParameter("@i", task.InstanceId),
                new SQLiteParameter("@e", task.Enabled ? 1 : 0),
                new SQLiteParameter("@sc", string.IsNullOrEmpty(task.DbScope) ? "user" : task.DbScope),
                new SQLiteParameter("@sd", (object)task.SelectedDbs ?? DBNull.Value),
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
                new SQLiteParameter("@id", task.Id));
        }

        public static void Delete(SqliteDb db, long id)
        {
            db.Execute("DELETE FROM mysql_tasks WHERE id=@id", new SQLiteParameter("@id", id));
        }

        public static bool TryClaim(SqliteDb db, long id, DateTime utcNow)
        {
            return TaskRunLock.TryClaim(db, "mysql_tasks", id, utcNow);
        }

        public static void MarkResult(SqliteDb db, MysqlTaskDto task, bool ok, DateTime utcNow)
        {
            DateTime? next = null;
            var enabled = task.Enabled;
            if (ok)
            {
                next = ScheduleCalculator.NextAfter(task.CycleKind, task.CycleInterval, utcNow);
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
                "UPDATE mysql_tasks SET last_run_utc=@lr, next_run_utc=@nr, run_count=run_count+1, enabled=@en, run_lock_utc=NULL WHERE id=@id",
                new SQLiteParameter("@lr", utcNow.ToString("o")),
                new SQLiteParameter("@nr", next.HasValue ? (object)next.Value.ToString("o") : DBNull.Value),
                new SQLiteParameter("@en", enabled ? 1 : 0),
                new SQLiteParameter("@id", task.Id));
        }

        public static string Validate(MysqlTaskDto task)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.Name))
            {
                return "请填写任务名称。";
            }

            if (task.InstanceId <= 0)
            {
                return "请选择 MySQL 实例。";
            }

            if (task.DbScope == "selected" && string.IsNullOrWhiteSpace(task.SelectedDbs))
            {
                return "指定库范围请填写库名。";
            }

            return ScheduleCalculator.Validate(task.CycleKind, task.CycleInterval, task.NextRunUtc);
        }

        private static SQLiteParameter[] Params(MysqlTaskDto task)
        {
            return new[]
            {
                new SQLiteParameter("@n", task.Name),
                new SQLiteParameter("@i", task.InstanceId),
                new SQLiteParameter("@e", task.Enabled ? 1 : 0),
                new SQLiteParameter("@sc", string.IsNullOrEmpty(task.DbScope) ? "user" : task.DbScope),
                new SQLiteParameter("@sd", (object)task.SelectedDbs ?? DBNull.Value),
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
                new SQLiteParameter("@u", SqliteDb.UtcNowText())
            };
        }

        private static MysqlTaskDto Read(DataRow row)
        {
            return new MysqlTaskDto
            {
                Id = Convert.ToInt64(row["id"]),
                Name = Convert.ToString(row["name"]),
                InstanceId = Convert.ToInt64(row["instance_id"]),
                Enabled = Convert.ToInt32(row["enabled"]) != 0,
                DbScope = Convert.ToString(row["db_scope"]),
                SelectedDbs = row["selected_dbs"] == DBNull.Value ? null : Convert.ToString(row["selected_dbs"]),
                RetryMinutes = Convert.ToInt32(row["retry_minutes"]),
                SortOrder = Convert.ToInt32(row["sort_order"]),
                CycleKind = Convert.ToString(row["cycle_kind"]),
                CycleInterval = Convert.ToInt32(row["cycle_interval"]),
                NextRunUtc = Parse(row["next_run_utc"]),
                LastRunUtc = Parse(row["last_run_utc"]),
                RunCount = Convert.ToInt32(row["run_count"]),
                DoNetwork = Convert.ToInt32(row["do_network"]) != 0,
                DoFtp = Convert.ToInt32(row["do_ftp"]) != 0,
                DoCloud = Convert.ToInt32(row["do_cloud"]) != 0,
                CloudProvider = row["cloud_provider"] == DBNull.Value ? null : Convert.ToString(row["cloud_provider"]),
                DoDeleteNow = Convert.ToInt32(row["do_delete_now"]) != 0,
                DoEmail = Convert.ToInt32(row["do_email"]) != 0
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
