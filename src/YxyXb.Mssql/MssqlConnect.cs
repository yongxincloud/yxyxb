using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Security;

namespace YxyXb.Mssql
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

        public static void RestoreDatabase(MssqlInstanceDto instance, string bakFile, string targetDb, bool replaceExisting)
        {
            if (string.IsNullOrWhiteSpace(bakFile) || !File.Exists(bakFile))
            {
                throw new FileNotFoundException("找不到 .bak 文件。", bakFile);
            }

            targetDb = (targetDb ?? "").Trim();
            if (targetDb.Length == 0)
            {
                throw new InvalidOperationException("请填写目标库名。");
            }

            if (IsSystemDb(targetDb))
            {
                throw new InvalidOperationException("拒绝恢复到系统库：" + targetDb);
            }

            using (var conn = Open(instance))
            {
                var files = ListBakFiles(conn, bakFile);
                if (files.Count == 0)
                {
                    throw new InvalidOperationException("无法读取备份文件列表。");
                }

                string dataPath;
                string logPath;
                ReadDefaultPaths(conn, out dataPath, out logPath);
                var moves = new StringBuilder();
                for (var i = 0; i < files.Count; i++)
                {
                    var logical = files[i].LogicalName;
                    var type = files[i].Type;
                    var ext = string.Equals(type, "L", StringComparison.OrdinalIgnoreCase) ? ".ldf" : ".mdf";
                    var folder = string.Equals(type, "L", StringComparison.OrdinalIgnoreCase) ? logPath : dataPath;
                    var physical = Path.Combine(folder, SafeFile(targetDb) + (i == 0 ? "" : "_" + i) + ext);
                    if (moves.Length > 0) moves.Append(", ");
                    moves.Append("MOVE N'").Append(logical.Replace("'", "''")).Append("' TO N'")
                        .Append(physical.Replace("'", "''")).Append("'");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 0;
                    if (replaceExisting && DatabaseExists(conn, targetDb))
                    {
                        cmd.CommandText = "ALTER DATABASE " + QuoteName(targetDb)
                            + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                        cmd.ExecuteNonQuery();
                    }

                    var with = "RECOVERY, " + moves;
                    if (replaceExisting) with = "REPLACE, " + with;
                    cmd.CommandText = "RESTORE DATABASE " + QuoteName(targetDb)
                        + " FROM DISK = N'" + bakFile.Replace("'", "''") + "' WITH " + with;
                    cmd.ExecuteNonQuery();

                    if (replaceExisting)
                    {
                        try
                        {
                            cmd.CommandText = "ALTER DATABASE " + QuoteName(targetDb) + " SET MULTI_USER";
                            cmd.ExecuteNonQuery();
                        }
                        catch (SqlException)
                        {
                        }
                    }
                }
            }
        }

        private static bool DatabaseExists(SqlConnection conn, string dbName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(1) FROM sys.databases WHERE name = @n";
                cmd.Parameters.AddWithValue("@n", dbName);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static void ReadDefaultPaths(SqlConnection conn, out string dataPath, out string logPath)
        {
            dataPath = null;
            logPath = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT CONVERT(nvarchar(260), SERVERPROPERTY('InstanceDefaultDataPath')),"
                    + " CONVERT(nvarchar(260), SERVERPROPERTY('InstanceDefaultLogPath'))";
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        dataPath = reader.IsDBNull(0) ? null : reader.GetString(0);
                        logPath = reader.IsDBNull(1) ? null : reader.GetString(1);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(dataPath) || string.IsNullOrWhiteSpace(logPath))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TOP 1 physical_name FROM master.sys.master_files WHERE database_id = 1 AND type = 0";
                    var master = Convert.ToString(cmd.ExecuteScalar()) ?? "";
                    var dir = Path.GetDirectoryName(master);
                    if (string.IsNullOrWhiteSpace(dataPath)) dataPath = dir;
                    if (string.IsNullOrWhiteSpace(logPath)) logPath = dir;
                }
            }

            if (string.IsNullOrWhiteSpace(dataPath)) dataPath = @"C:\Program Files\Microsoft SQL Server\MSSQL\DATA";
            if (string.IsNullOrWhiteSpace(logPath)) logPath = dataPath;
        }

        private static IList<BakFileRow> ListBakFiles(SqlConnection conn, string bakFile)
        {
            var list = new List<BakFileRow>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = "RESTORE FILELISTONLY FROM DISK = N'" + bakFile.Replace("'", "''") + "'";
                using (var reader = cmd.ExecuteReader())
                {
                    var logicalOrd = reader.GetOrdinal("LogicalName");
                    var typeOrd = reader.GetOrdinal("Type");
                    while (reader.Read())
                    {
                        list.Add(new BakFileRow
                        {
                            LogicalName = reader.GetString(logicalOrd),
                            Type = reader.GetString(typeOrd)
                        });
                    }
                }
            }

            return list;
        }

        private static string SafeFile(string name)
        {
            var chars = Path.GetInvalidFileNameChars();
            var text = name ?? "db";
            for (var i = 0; i < chars.Length; i++)
            {
                text = text.Replace(chars[i], '_');
            }

            return text;
        }

        private sealed class BakFileRow
        {
            public string LogicalName;
            public string Type;
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
