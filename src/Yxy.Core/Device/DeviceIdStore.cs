using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;

namespace Yxy.Core.Device
{
    public static class DeviceIdStore
    {
        public static DeviceInfoDto GetOrCreate(SqliteDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            var existing = db.ExecuteScalar("SELECT device_id FROM device_id WHERE id = 1") as string;
            var createdText = db.ExecuteScalar("SELECT created_utc FROM device_id WHERE id = 1") as string;
            if (!string.IsNullOrEmpty(existing))
            {
                return new DeviceInfoDto
                {
                    DeviceId = existing,
                    CreatedUtc = string.IsNullOrEmpty(createdText) ? DateTime.UtcNow : SqliteDb.ParseUtc(createdText)
                };
            }

            var fromFile = TryReadProtectedFile();
            var deviceId = string.IsNullOrEmpty(fromFile) ? Guid.NewGuid().ToString("N") : fromFile;
            var now = SqliteDb.UtcNowText();
            db.Execute(
                "INSERT OR IGNORE INTO device_id (id, device_id, created_utc) VALUES (1, @id, @utc)",
                new SQLiteParameter("@id", deviceId),
                new SQLiteParameter("@utc", now));

            WriteProtectedFile(deviceId);
            return new DeviceInfoDto
            {
                DeviceId = deviceId,
                CreatedUtc = SqliteDb.ParseUtc(now)
            };
        }

        private static string TryReadProtectedFile()
        {
            try
            {
                if (!File.Exists(AppPaths.DeviceIdFile))
                {
                    return null;
                }

                var raw = File.ReadAllBytes(AppPaths.DeviceIdFile);
                var plain = ProtectedData.Unprotect(raw, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static void WriteProtectedFile(string deviceId)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var plain = Encoding.UTF8.GetBytes(deviceId);
                var raw = ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine);
                File.WriteAllBytes(AppPaths.DeviceIdFile, raw);
            }
            catch (CryptographicException)
            {
            }
            catch (IOException)
            {
            }
        }
    }
}
