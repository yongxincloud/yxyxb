using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;
using Yxy.Core.Security;

namespace Yxy.Core.Store
{
    public static class CloudAccountStore
    {
        public static CloudAccountDto Get(SqliteDb db, string provider)
        {
            var table = db.Query(
                "SELECT * FROM cloud_accounts WHERE provider=@p LIMIT 1",
                new SQLiteParameter("@p", provider ?? ""));
            if (table.Rows.Count == 0)
            {
                return new CloudAccountDto
                {
                    Provider = provider,
                    Enabled = false,
                    Zone = "z0"
                };
            }

            var row = table.Rows[0];
            var fields = JsonUtil.FromJson<CloudAccountDto>(SecretBox.Unprotect(Convert.ToString(row["fields_protected"])))
                ?? new CloudAccountDto();
            fields.Provider = Convert.ToString(row["provider"]);
            fields.Enabled = Convert.ToInt32(row["enabled"]) != 0;
            if (string.IsNullOrEmpty(fields.Zone))
            {
                fields.Zone = "z0";
            }

            return fields;
        }

        public static void Save(SqliteDb db, CloudAccountDto account)
        {
            if (account == null || string.IsNullOrEmpty(account.Provider))
            {
                throw new ArgumentException("provider");
            }

            var json = SecretBox.Protect(JsonUtil.ToJson(new CloudAccountDto
            {
                Bucket = account.Bucket,
                AccessKey = account.AccessKey,
                SecretKey = account.SecretKey,
                Zone = string.IsNullOrEmpty(account.Zone) ? "z0" : account.Zone,
                AccessToken = account.AccessToken,
                RefreshToken = account.RefreshToken,
                TokenExpiryUtc = account.TokenExpiryUtc,
                DisplayName = account.DisplayName
            }));

            var exists = db.ExecuteScalar(
                "SELECT id FROM cloud_accounts WHERE provider=@p LIMIT 1",
                new SQLiteParameter("@p", account.Provider));
            if (exists == null || exists == DBNull.Value)
            {
                db.Execute(
                    "INSERT INTO cloud_accounts (provider,enabled,fields_protected,created_utc) VALUES (@p,@e,@f,@u)",
                    new SQLiteParameter("@p", account.Provider),
                    new SQLiteParameter("@e", account.Enabled ? 1 : 0),
                    new SQLiteParameter("@f", (object)json ?? DBNull.Value),
                    new SQLiteParameter("@u", SqliteDb.UtcNowText()));
            }
            else
            {
                db.Execute(
                    "UPDATE cloud_accounts SET enabled=@e, fields_protected=@f WHERE provider=@p",
                    new SQLiteParameter("@e", account.Enabled ? 1 : 0),
                    new SQLiteParameter("@f", (object)json ?? DBNull.Value),
                    new SQLiteParameter("@p", account.Provider));
            }
        }

        public static bool IsQiniuReady(SqliteDb db)
        {
            return Get(db, CloudProviders.Qiniu).IsReady;
        }

        public static bool IsBaiduReady(SqliteDb db)
        {
            return Get(db, CloudProviders.Baidu).IsBaiduReady;
        }

        /// <summary>清除百度授权令牌并关闭启用，便于换号重新授权。</summary>
        public static void ClearBaidu(SqliteDb db)
        {
            var acc = Get(db, CloudProviders.Baidu);
            acc.Provider = CloudProviders.Baidu;
            acc.Enabled = false;
            acc.AccessToken = null;
            acc.RefreshToken = null;
            acc.TokenExpiryUtc = null;
            acc.DisplayName = null;
            Save(db, acc);
        }

        public static IList<string> EnabledProviders(SqliteDb db)
        {
            var list = new List<string>();
            if (IsQiniuReady(db))
            {
                list.Add(CloudProviders.Qiniu);
            }

            if (IsBaiduReady(db))
            {
                list.Add(CloudProviders.Baidu);
            }

            return list;
        }
    }
}
