using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Yxy.Contracts.Dto;
using Yxy.Core.Security;

namespace Yxy.Mssql
{
    public static class MssqlConnect
    {
        public static readonly string[] SystemNames = { "master", "model", "msdb", "tempdb", "distribution" };

        public static bool IsSystemDb(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            for (var i = 0; i < SystemNames.Length; i++)
            {
                if (string.Equals(name, SystemNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string QuoteName(string name)
        {
            return "[" + (name ?? "").Replace("]", "]]") + "]";
        }

        public static string BuildConnectionString(MssqlInstanceDto instance, string passwordPlain)
        {
            var server = instance == null || string.IsNullOrEmpty(instance.Server) ? "localhost" : instance.Server;
            if (instance != null && instance.Port > 0 && server.IndexOf(',') < 0)
            {
                server = server + "," + instance.Port;
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = "master",
                ConnectTimeout = instance == null || instance.TimeoutSeconds < 1 ? 30 : instance.TimeoutSeconds
            };

            if (instance != null && instance.AuthKind == "sql")
            {
                builder.IntegratedSecurity = false;
                builder.UserID = instance.Username ?? "";
                builder.Password = passwordPlain ?? "";
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            return builder.ConnectionString;
        }

        public static string ResolvePassword(MssqlInstanceDto instance)
        {
            if (instance == null || instance.AuthKind != "sql")
            {
                return null;
            }

            return SecretBox.Unprotect(instance.PasswordProtected);
        }

        public static string Test(MssqlInstanceDto instance)
        {
            try
            {
                using (var conn = Open(instance))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT @@VERSION";
                    var version = Convert.ToString(cmd.ExecuteScalar());
                    return "连接成功。" + (string.IsNullOrEmpty(version) ? "" : " " + FirstLine(version));
                }
            }
            catch (Exception ex)
            {
                return "连接失败：" + ex.Message;
            }
        }

        public static IList<string> ListUserDatabases(MssqlInstanceDto instance)
        {
            var list = new List<string>();
            using (var conn = Open(instance))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sys.databases WHERE state = 0 ORDER BY name";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var name = reader.GetString(0);
                        if (!IsSystemDb(name))
                        {
                            list.Add(name);
                        }
                    }
                }
            }

            return list;
        }

        public static SqlConnection Open(MssqlInstanceDto instance)
        {
            var conn = new SqlConnection(BuildConnectionString(instance, ResolvePassword(instance)));
            conn.Open();
            return conn;
        }

        public static void BackupDatabase(MssqlInstanceDto instance, string dbName, string bakFile)
        {
            if (IsSystemDb(dbName))
            {
                throw new InvalidOperationException("拒绝备份系统库：" + dbName);
            }

            using (var conn = Open(instance))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "BACKUP DATABASE " + QuoteName(dbName)
                    + " TO DISK = N'" + (bakFile ?? "").Replace("'", "''") + "' WITH INIT, COPY_ONLY";
                cmd.ExecuteNonQuery();
            }
        }

        private static string FirstLine(string text)
        {
            var n = text.IndexOf('\n');
            if (n < 0)
            {
                return text.Trim();
            }

            return text.Substring(0, n).Trim();
        }
    }
}
