using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MySqlConnector;
using Yxy.Contracts.Dto;
using Yxy.Core;
using Yxy.Core.Security;

namespace Yxy.MySql
{
    public static class MysqlConnect
    {
        public static readonly string[] SystemNames = { "information_schema", "mysql", "performance_schema", "sys" };

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

        public static string BuildConnectionString(MysqlInstanceDto instance, string passwordPlain)
        {
            var server = instance == null || string.IsNullOrEmpty(instance.Server) ? "localhost" : instance.Server;
            var builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Port = (uint)(instance == null || instance.Port < 1 ? 3306 : instance.Port),
                UserID = instance == null ? "" : (instance.Username ?? ""),
                Password = passwordPlain ?? "",
                Database = "mysql",
                ConnectionTimeout = (uint)(instance == null || instance.TimeoutSeconds < 1 ? 30 : instance.TimeoutSeconds)
            };
            return builder.ConnectionString;
        }

        public static string ResolvePassword(MysqlInstanceDto instance)
        {
            if (instance == null)
            {
                return null;
            }

            return SecretBox.Unprotect(instance.PasswordProtected);
        }

        public static string Test(MysqlInstanceDto instance)
        {
            try
            {
                using (var conn = Open(instance))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT VERSION()";
                    var version = Convert.ToString(cmd.ExecuteScalar());
                    return "连接成功。" + (string.IsNullOrEmpty(version) ? "" : " " + version.Trim());
                }
            }
            catch (Exception ex)
            {
                return "连接失败：" + ex.Message;
            }
        }

        public static IList<string> ListUserDatabases(MysqlInstanceDto instance)
        {
            var list = new List<string>();
            using (var conn = Open(instance))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SHOW DATABASES";
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

        public static void DumpDatabase(MysqlInstanceDto instance, string dbName, string sqlFile)
        {
            if (IsSystemDb(dbName))
            {
                throw new InvalidOperationException("拒绝备份系统库：" + dbName);
            }

            var exe = FindDump();
            if (exe == null)
            {
                throw new InvalidOperationException("未找到 mysqldump.exe。请安装 MySQL 客户端，或把 mysqldump.exe 放到程序目录。");
            }

            var password = ResolvePassword(instance) ?? "";
            var server = instance == null || string.IsNullOrEmpty(instance.Server) ? "localhost" : instance.Server;
            var port = instance == null || instance.Port < 1 ? 3306 : instance.Port;
            var user = instance == null ? "" : (instance.Username ?? "");
            var cnf = Path.Combine(Path.GetTempPath(), "yxy-mysql-" + Guid.NewGuid().ToString("N") + ".cnf");
            try
            {
                File.WriteAllText(cnf, "[client]\r\nhost=" + server + "\r\nport=" + port + "\r\nuser=" + user + "\r\npassword=" + password.Replace("\r", "").Replace("\n", "") + "\r\n", Encoding.ASCII);
                var args = "--defaults-extra-file=\"" + cnf + "\" --single-transaction --routines --events --databases \"" + dbName.Replace("\"", "") + "\" --result-file=\"" + sqlFile + "\"";
                var start = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(start))
                {
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0 || !File.Exists(sqlFile) || new FileInfo(sqlFile).Length == 0)
                    {
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "mysqldump 失败。" : error.Trim());
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(cnf)) File.Delete(cnf);
                }
                catch (IOException)
                {
                }
            }
        }

        public static MySqlConnection Open(MysqlInstanceDto instance)
        {
            var conn = new MySqlConnection(BuildConnectionString(instance, ResolvePassword(instance)));
            conn.Open();
            return conn;
        }

        public static void RestoreDatabase(MysqlInstanceDto instance, string sqlFile, string targetDb, bool replaceExisting)
        {
            if (string.IsNullOrWhiteSpace(sqlFile) || !File.Exists(sqlFile))
            {
                throw new FileNotFoundException("找不到 .sql 文件。", sqlFile);
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

            var exe = FindClient("mysql.exe");
            if (exe == null)
            {
                throw new InvalidOperationException("未找到 mysql.exe。请安装 MySQL 客户端，或把 mysql.exe 放到程序目录。");
            }

            EnsureDatabase(instance, targetDb, replaceExisting);

            var password = ResolvePassword(instance) ?? "";
            var server = instance == null || string.IsNullOrEmpty(instance.Server) ? "localhost" : instance.Server;
            var port = instance == null || instance.Port < 1 ? 3306 : instance.Port;
            var user = instance == null ? "" : (instance.Username ?? "");
            var cnf = Path.Combine(Path.GetTempPath(), "yxy-mysql-" + Guid.NewGuid().ToString("N") + ".cnf");
            try
            {
                File.WriteAllText(cnf, "[client]\r\nhost=" + server + "\r\nport=" + port + "\r\nuser=" + user
                    + "\r\npassword=" + password.Replace("\r", "").Replace("\n", "") + "\r\n", Encoding.ASCII);
                var args = "--defaults-extra-file=\"" + cnf + "\" --database \"" + targetDb.Replace("\"", "") + "\"";
                var start = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(start))
                {
                    using (var input = File.OpenRead(sqlFile))
                    {
                        input.CopyTo(process.StandardInput.BaseStream);
                    }

                    process.StandardInput.Close();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "mysql 导入失败。" : error.Trim());
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(cnf)) File.Delete(cnf);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void EnsureDatabase(MysqlInstanceDto instance, string targetDb, bool replaceExisting)
        {
            using (var conn = Open(instance))
            using (var cmd = conn.CreateCommand())
            {
                if (replaceExisting)
                {
                    cmd.CommandText = "DROP DATABASE IF EXISTS `" + targetDb.Replace("`", "``") + "`";
                    cmd.ExecuteNonQuery();
                }

                cmd.CommandText = "CREATE DATABASE IF NOT EXISTS `" + targetDb.Replace("`", "``")
                    + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci";
                cmd.ExecuteNonQuery();
            }
        }

        private static string FindDump()
        {
            return FindClient("mysqldump.exe");
        }

        private static string FindClient(string fileName)
        {
            var beside = Path.Combine(AppPaths.InstallDirectory, fileName);
            if (File.Exists(beside))
            {
                return beside;
            }

            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            var dirs = path.Split(';');
            for (var i = 0; i < dirs.Length; i++)
            {
                var dir = (dirs[i] ?? "").Trim();
                if (dir.Length == 0) continue;
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }

            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MySQL");
            if (!Directory.Exists(root)) return null;
            var files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return files.Length == 0 ? null : files[0];
        }
    }
}
