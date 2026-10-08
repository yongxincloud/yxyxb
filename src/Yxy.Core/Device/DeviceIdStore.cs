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
        /// <summary>
        /// 取得本机设备号。若 data\device.id 存在但本机无法解密（典型：从其它电脑整包拷贝），
        /// 则作废旧号并生成新号，返回 true 表示发生了「换机重置」。
        /// </summary>
        public static DeviceInfoDto GetOrCreate(SqliteDb db)
        {
            bool reset;
            return GetOrCreate(db, out reset);
        }

        public static DeviceInfoDto GetOrCreate(SqliteDb db, out bool resetBecauseCopied)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            resetBecauseCopied = false;
            var fromFile = TryReadProtectedFile();
            if (File.Exists(AppPaths.DeviceIdFile) && fromFile == null)
            {
                // 文件来自其它机器的 DPAPI，本机解不开 → 视为拷贝安装
                resetBecauseCopied = true;
                TryDeleteFile(AppPaths.DeviceIdFile);
                db.Execute("DELETE FROM device_id WHERE id = 1");
            }

            var existing = db.ExecuteScalar("SELECT device_id FROM device_id WHERE id = 1") as string;
            var createdText = db.ExecuteScalar("SELECT created_utc FROM device_id WHERE id = 1") as string;

            if (!string.IsNullOrEmpty(fromFile))
            {
                if (!string.Equals(existing, fromFile, StringComparison.Ordinal))
                {
                    db.Execute("DELETE FROM device_id WHERE id = 1");
                    var now = SqliteDb.UtcNowText();
                    db.Execute(
                        "INSERT INTO device_id (id, device_id, created_utc) VALUES (1, @id, @utc)",
                        new SQLiteParameter("@id", fromFile),
                        new SQLiteParameter("@utc", now));
                    createdText = now;
                }

                return new DeviceInfoDto
                {
                    DeviceId = fromFile,
                    CreatedUtc = string.IsNullOrEmpty(createdText) ? DateTime.UtcNow : SqliteDb.ParseUtc(createdText)
                };
            }

            if (!string.IsNullOrEmpty(existing))
            {
                WriteProtectedFile(existing);
                return new DeviceInfoDto
                {
                    DeviceId = existing,
                    CreatedUtc = string.IsNullOrEmpty(createdText) ? DateTime.UtcNow : SqliteDb.ParseUtc(createdText)
                };
            }

            var deviceId = Guid.NewGuid().ToString("N");
            var created = SqliteDb.UtcNowText();
            db.Execute(
                "INSERT OR IGNORE INTO device_id (id, device_id, created_utc) VALUES (1, @id, @utc)",
                new SQLiteParameter("@id", deviceId),
                new SQLiteParameter("@utc", created));
            WriteProtectedFile(deviceId);
            return new DeviceInfoDto
            {
                DeviceId = deviceId,
                CreatedUtc = SqliteDb.ParseUtc(created)
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

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
