using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;

namespace Yxy.Core.Store
{
    public static class LogStore
    {
        public static void Add(SqliteDb db, string type, string mode, string result, string taskName, string message)
        {
            db.Execute(
                "INSERT INTO logs (type, mode, result, message, task_name, created_utc) VALUES (@t,@m,@r,@msg,@n,@u)",
                new SQLiteParameter("@t", type ?? "file"),
                new SQLiteParameter("@m", string.IsNullOrEmpty(mode) ? "auto" : mode),
                new SQLiteParameter("@r", result ?? "ok"),
                new SQLiteParameter("@msg", (object)message ?? DBNull.Value),
                new SQLiteParameter("@n", (object)taskName ?? DBNull.Value),
                new SQLiteParameter("@u", SqliteDb.UtcNowText()));
        }

        public static LogEntryDto LatestForTask(SqliteDb db, string type, string taskName)
        {
            var table = db.Query(
                "SELECT * FROM logs WHERE type=@t AND task_name=@n ORDER BY id DESC LIMIT 1",
                new SQLiteParameter("@t", type),
                new SQLiteParameter("@n", taskName ?? ""));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            var row = table.Rows[0];
            return new LogEntryDto
            {
                Id = Convert.ToInt64(row["id"]),
                Type = Convert.ToString(row["type"]),
                Mode = Convert.ToString(row["mode"]),
                Result = Convert.ToString(row["result"]),
                Message = row["message"] == DBNull.Value ? "" : Convert.ToString(row["message"]),
                TaskName = row["task_name"] == DBNull.Value ? "" : Convert.ToString(row["task_name"]),
                CreatedUtc = SqliteDb.ParseUtc(Convert.ToString(row["created_utc"]))
            };
        }

        public static IList<LogEntryDto> List(SqliteDb db, string type, int take)
        {
            if (take <= 0)
            {
                take = 200;
            }

            var table = db.Query(
                "SELECT * FROM logs WHERE type=@t ORDER BY id DESC LIMIT " + take,
                new SQLiteParameter("@t", type));
            var list = new List<LogEntryDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(new LogEntryDto
                {
                    Id = Convert.ToInt64(row["id"]),
                    Type = Convert.ToString(row["type"]),
                    Mode = Convert.ToString(row["mode"]),
                    Result = Convert.ToString(row["result"]),
                    Message = row["message"] == DBNull.Value ? "" : Convert.ToString(row["message"]),
                    TaskName = row["task_name"] == DBNull.Value ? "" : Convert.ToString(row["task_name"]),
                    CreatedUtc = SqliteDb.ParseUtc(Convert.ToString(row["created_utc"]))
                });
            }

            return list;
        }

        public static int DeleteOlderThan(SqliteDb db, DateTime cutoffUtc)
        {
            return db.Execute(
                "DELETE FROM logs WHERE created_utc < @c",
                new SQLiteParameter("@c", cutoffUtc.ToString("o")));
        }
    }
}
