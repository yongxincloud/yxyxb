using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Security;

namespace YxyXb.Core.Store
{
    public static class MssqlInstanceStore
    {
        public static IList<MssqlInstanceDto> List(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM mssql_instances ORDER BY id");
            var list = new List<MssqlInstanceDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static MssqlInstanceDto GetById(SqliteDb db, long id)
        {
            var table = db.Query("SELECT * FROM mssql_instances WHERE id=@id", new SQLiteParameter("@id", id));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static long Insert(SqliteDb db, MssqlInstanceDto item, string passwordPlain)
        {
            db.Execute(
                @"INSERT INTO mssql_instances (name,server,port,auth_kind,username,password_protected,timeout_seconds,enabled,created_utc)
                  VALUES (@n,@s,@p,@a,@u,@pw,@t,@e,@c)",
                Params(item, passwordPlain));
            return Convert.ToInt64(db.ExecuteScalar("SELECT last_insert_rowid()"));
        }

        public static void Update(SqliteDb db, MssqlInstanceDto item, string passwordPlain, bool keepPassword)
        {
            var protectedPw = item.PasswordProtected;
            if (!keepPassword)
            {
                protectedPw = SecretBox.Protect(passwordPlain);
            }

            db.Execute(
                @"UPDATE mssql_instances SET name=@n, server=@s, port=@p, auth_kind=@a, username=@u,
                  password_protected=@pw, timeout_seconds=@t, enabled=@e WHERE id=@id",
                new SQLiteParameter("@n", item.Name),
                new SQLiteParameter("@s", string.IsNullOrEmpty(item.Server) ? "localhost" : item.Server),
                new SQLiteParameter("@p", item.Port),
                new SQLiteParameter("@a", string.IsNullOrEmpty(item.AuthKind) ? "windows" : item.AuthKind),
                new SQLiteParameter("@u", (object)item.Username ?? DBNull.Value),
                new SQLiteParameter("@pw", (object)protectedPw ?? DBNull.Value),
                new SQLiteParameter("@t", item.TimeoutSeconds < 1 ? 30 : item.TimeoutSeconds),
                new SQLiteParameter("@e", item.Enabled ? 1 : 0),
                new SQLiteParameter("@id", item.Id));
        }

        public static void Delete(SqliteDb db, long id)
        {
            db.Execute("DELETE FROM mssql_tasks WHERE instance_id=@id", new SQLiteParameter("@id", id));
            db.Execute("DELETE FROM mssql_instances WHERE id=@id", new SQLiteParameter("@id", id));
        }

        public static string Validate(MssqlInstanceDto item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
            {
                return "请填写实例名称。";
            }

            if (string.IsNullOrWhiteSpace(item.Server))
            {
                return "请填写服务器。";
            }

            if (item.AuthKind == "sql" && string.IsNullOrWhiteSpace(item.Username))
            {
                return "SQL 账户请填写用户名。";
            }

            return null;
        }

        private static SQLiteParameter[] Params(MssqlInstanceDto item, string passwordPlain)
        {
            return new[]
            {
                new SQLiteParameter("@n", item.Name),
                new SQLiteParameter("@s", string.IsNullOrEmpty(item.Server) ? "localhost" : item.Server),
                new SQLiteParameter("@p", item.Port),
                new SQLiteParameter("@a", string.IsNullOrEmpty(item.AuthKind) ? "windows" : item.AuthKind),
                new SQLiteParameter("@u", (object)item.Username ?? DBNull.Value),
                new SQLiteParameter("@pw", (object)SecretBox.Protect(passwordPlain) ?? DBNull.Value),
                new SQLiteParameter("@t", item.TimeoutSeconds < 1 ? 30 : item.TimeoutSeconds),
                new SQLiteParameter("@e", item.Enabled ? 1 : 0),
                new SQLiteParameter("@c", SqliteDb.UtcNowText())
            };
        }

        private static MssqlInstanceDto Read(DataRow row)
        {
            return new MssqlInstanceDto
            {
                Id = Convert.ToInt64(row["id"]),
                Name = Convert.ToString(row["name"]),
                Server = Convert.ToString(row["server"]),
                Port = Convert.ToInt32(row["port"]),
                AuthKind = Convert.ToString(row["auth_kind"]),
                Username = row["username"] == DBNull.Value ? null : Convert.ToString(row["username"]),
                PasswordProtected = row["password_protected"] == DBNull.Value ? null : Convert.ToString(row["password_protected"]),
                TimeoutSeconds = Convert.ToInt32(row["timeout_seconds"]),
                Enabled = Convert.ToInt32(row["enabled"]) != 0
            };
        }
    }
}
