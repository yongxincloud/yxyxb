using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Security;

namespace YxyXb.Core.Store
{
    public static class MysqlInstanceStore
    {
        public static IList<MysqlInstanceDto> List(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM mysql_instances ORDER BY id");
            var list = new List<MysqlInstanceDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static MysqlInstanceDto GetById(SqliteDb db, long id)
        {
            var table = db.Query("SELECT * FROM mysql_instances WHERE id=@id", new SQLiteParameter("@id", id));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static long Insert(SqliteDb db, MysqlInstanceDto item, string passwordPlain)
        {
            db.Execute(
                @"INSERT INTO mysql_instances (name,server,port,username,password_protected,timeout_seconds,enabled,created_utc)
                  VALUES (@n,@s,@p,@u,@pw,@t,@e,@c)",
                Params(item, passwordPlain));
            return Convert.ToInt64(db.ExecuteScalar("SELECT last_insert_rowid()"));
        }

        public static void Update(SqliteDb db, MysqlInstanceDto item, string passwordPlain, bool keepPassword)
        {
            var protectedPw = item.PasswordProtected;
            if (!keepPassword)
            {
                protectedPw = SecretBox.Protect(passwordPlain);
            }

            db.Execute(
                @"UPDATE mysql_instances SET name=@n, server=@s, port=@p, username=@u,
                  password_protected=@pw, timeout_seconds=@t, enabled=@e WHERE id=@id",
                new SQLiteParameter("@n", item.Name),
                new SQLiteParameter("@s", string.IsNullOrEmpty(item.Server) ? "localhost" : item.Server),
                new SQLiteParameter("@p", item.Port > 0 ? item.Port : 3306),
                new SQLiteParameter("@u", (object)item.Username ?? DBNull.Value),
                new SQLiteParameter("@pw", (object)protectedPw ?? DBNull.Value),
                new SQLiteParameter("@t", item.TimeoutSeconds < 1 ? 30 : item.TimeoutSeconds),
                new SQLiteParameter("@e", item.Enabled ? 1 : 0),
                new SQLiteParameter("@id", item.Id));
        }

        public static void Delete(SqliteDb db, long id)
        {
            db.Execute("DELETE FROM mysql_tasks WHERE instance_id=@id", new SQLiteParameter("@id", id));
            db.Execute("DELETE FROM mysql_instances WHERE id=@id", new SQLiteParameter("@id", id));
        }

        public static string Validate(MysqlInstanceDto item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
            {
                return "请填写实例名称。";
            }

            if (string.IsNullOrWhiteSpace(item.Server))
            {
                return "请填写服务器。";
            }

            if (string.IsNullOrWhiteSpace(item.Username))
            {
                return "请填写用户名。";
            }

            return null;
        }

        private static SQLiteParameter[] Params(MysqlInstanceDto item, string passwordPlain)
        {
            return new[]
            {
                new SQLiteParameter("@n", item.Name),
                new SQLiteParameter("@s", string.IsNullOrEmpty(item.Server) ? "localhost" : item.Server),
                new SQLiteParameter("@p", item.Port > 0 ? item.Port : 3306),
                new SQLiteParameter("@u", (object)item.Username ?? DBNull.Value),
                new SQLiteParameter("@pw", (object)SecretBox.Protect(passwordPlain) ?? DBNull.Value),
                new SQLiteParameter("@t", item.TimeoutSeconds < 1 ? 30 : item.TimeoutSeconds),
                new SQLiteParameter("@e", item.Enabled ? 1 : 0),
                new SQLiteParameter("@c", SqliteDb.UtcNowText())
            };
        }

        private static MysqlInstanceDto Read(DataRow row)
        {
            return new MysqlInstanceDto
            {
                Id = Convert.ToInt64(row["id"]),
                Name = Convert.ToString(row["name"]),
                Server = Convert.ToString(row["server"]),
                Port = Convert.ToInt32(row["port"]),
                Username = row["username"] == DBNull.Value ? null : Convert.ToString(row["username"]),
                PasswordProtected = row["password_protected"] == DBNull.Value ? null : Convert.ToString(row["password_protected"]),
                TimeoutSeconds = Convert.ToInt32(row["timeout_seconds"]),
                Enabled = Convert.ToInt32(row["enabled"]) != 0
            };
        }
    }
}
