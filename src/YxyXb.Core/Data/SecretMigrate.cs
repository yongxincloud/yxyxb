using System;
using System.Data.SQLite;
using YxyXb.Core.Security;

namespace YxyXb.Core.Data
{
    internal static class SecretMigrate
    {
        public static void Apply(SqliteDb db)
        {
            Rewrite(db, "settings", "zip_password_protected");
            Rewrite(db, "settings", "lock_password_protected");
            Rewrite(db, "xfer_accounts", "fields_protected");
            Rewrite(db, "cloud_accounts", "fields_protected");
            Rewrite(db, "mssql_instances", "password_protected");
            Rewrite(db, "mysql_instances", "password_protected");
        }

        private static void Rewrite(SqliteDb db, string table, string column)
        {
            var rows = db.Query("SELECT id, " + column + " AS secret FROM " + table + " WHERE " + column + " IS NOT NULL AND " + column + " != ''");
            foreach (System.Data.DataRow row in rows.Rows)
            {
                var cipher = Convert.ToString(row["secret"]);
                if (SecretBox.IsPortable(cipher))
                {
                    continue;
                }

                var plain = SecretBox.Unprotect(cipher);
                if (string.IsNullOrEmpty(plain))
                {
                    continue;
                }

                db.Execute(
                    "UPDATE " + table + " SET " + column + "=@v WHERE id=@id",
                    new SQLiteParameter("@v", SecretBox.Protect(plain)),
                    new SQLiteParameter("@id", Convert.ToInt64(row["id"])));
            }
        }
    }
}
