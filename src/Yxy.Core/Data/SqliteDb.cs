using System;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace Yxy.Core.Data
{
    public sealed class SqliteDb : IDisposable
    {
        private readonly SQLiteConnection _connection;
        private bool _disposed;

        public SqliteDb(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("filePath");
            }

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = filePath,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal
            };

            _connection = new SQLiteConnection(builder.ToString());
            _connection.Open();
            using (var pragma = _connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA busy_timeout=8000;";
                pragma.ExecuteNonQuery();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = SchemaSql.CreateAll;
                cmd.ExecuteNonQuery();
            }

            EnsureSingletonRows();
            SchemaMigrate.Apply(this);
        }

        public static SqliteDb OpenDefault()
        {
            AppPaths.EnsureDirectories();
            return new SqliteDb(AppPaths.DatabaseFile);
        }

        public SQLiteConnection Connection
        {
            get { return _connection; }
        }

        public SQLiteCommand CreateCommand(string sql)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = CreateCommand(sql))
            {
                if (parameters != null)
                {
                    cmd.Parameters.AddRange(parameters);
                }

                return cmd.ExecuteScalar();
            }
        }

        public int Execute(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = CreateCommand(sql))
            {
                if (parameters != null)
                {
                    cmd.Parameters.AddRange(parameters);
                }

                return cmd.ExecuteNonQuery();
            }
        }

        public DataTable Query(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = CreateCommand(sql))
            {
                if (parameters != null)
                {
                    cmd.Parameters.AddRange(parameters);
                }

                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static string UtcNowText()
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        public static DateTime ParseUtc(string text)
        {
            return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
        }

        private void EnsureSingletonRows()
        {
            var now = UtcNowText();
            Execute(
                "INSERT OR IGNORE INTO settings (id, bak_path, software_id, updated_utc) VALUES (1, @bak, @sid, @utc)",
                new SQLiteParameter("@bak", AppPaths.BakDirectory),
                new SQLiteParameter("@sid", AppPaths.ProductName),
                new SQLiteParameter("@utc", now));
            var legacy = Path.Combine(AppPaths.InstallDirectory, "bak");
            Execute(
                "UPDATE settings SET bak_path=@bak, updated_utc=@utc WHERE id=1 AND (bak_path=@old OR bak_path=@oldSlash)",
                new SQLiteParameter("@bak", AppPaths.BakDirectory),
                new SQLiteParameter("@utc", now),
                new SQLiteParameter("@old", legacy),
                new SQLiteParameter("@oldSlash", legacy + Path.DirectorySeparatorChar));

            Execute(
                "INSERT OR IGNORE INTO license (id, first_run_utc) VALUES (1, @utc)",
                new SQLiteParameter("@utc", now));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _connection.Dispose();
            _disposed = true;
        }
    }
}
