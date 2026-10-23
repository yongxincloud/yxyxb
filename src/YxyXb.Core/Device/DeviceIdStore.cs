using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;

namespace YxyXb.Core.Device
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
            var read = ReadProtectedFile();
            if (read.Kind == FileReadKind.Foreign)
            {
                resetBecauseCopied = true;
                TryDeleteFile(AppPaths.DeviceIdFile);
                db.Execute("DELETE FROM device_id WHERE id = 1");
            }

            var machine = ReadMachineGuid();
            var existing = db.ExecuteScalar("SELECT device_id FROM device_id WHERE id = 1") as string;
            var createdText = db.ExecuteScalar("SELECT created_utc FROM device_id WHERE id = 1") as string;
            var deviceId = machine;
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = read.Kind == FileReadKind.Ok ? read.Id : null;
            }

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = existing;
            }

            if (string.IsNullOrEmpty(deviceId))
            {
                if (read.Kind == FileReadKind.Locked)
                {
                    deviceId = "pc";
                }
                else
                {
                    deviceId = Guid.NewGuid().ToString("N");
                }
            }

            if (!string.Equals(existing, deviceId, StringComparison.Ordinal))
            {
                db.Execute("DELETE FROM device_id WHERE id = 1");
                var now = SqliteDb.UtcNowText();
                db.Execute(
                    "INSERT INTO device_id (id, device_id, created_utc) VALUES (1, @id, @utc)",
                    new SQLiteParameter("@id", deviceId),
                    new SQLiteParameter("@utc", now));
                createdText = now;
            }

            if (read.Kind != FileReadKind.Locked && read.Kind != FileReadKind.Ok)
            {
                WriteProtectedFile(deviceId);
            }
            else if (read.Kind == FileReadKind.Ok && !string.Equals(read.Id, deviceId, StringComparison.Ordinal))
            {
                WriteProtectedFile(deviceId);
            }

            return new DeviceInfoDto
            {
                DeviceId = deviceId,
                CreatedUtc = string.IsNullOrEmpty(createdText) ? DateTime.UtcNow : SqliteDb.ParseUtc(createdText)
            };
        }

        public static string CurrentId()
        {
            var machine = ReadMachineGuid();
            if (!string.IsNullOrEmpty(machine))
            {
                return machine;
            }

            var read = ReadProtectedFile();
            if (read.Kind == FileReadKind.Ok && !string.IsNullOrEmpty(read.Id))
            {
                return read.Id;
            }

            return "pc";
        }

        private static string ReadMachineGuid()
        {
            var from64 = ReadMachineGuid(RegistryView.Registry64);
            if (!string.IsNullOrEmpty(from64))
            {
                return from64;
            }

            return ReadMachineGuid(RegistryView.Registry32);
        }

        private static string ReadMachineGuid(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    var value = key == null ? null : key.GetValue("MachineGuid") as string;
                    return NormalizeGuid(value);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NormalizeGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            Guid parsed;
            if (!Guid.TryParse(value.Trim().Trim('{', '}'), out parsed))
            {
                return null;
            }

            return parsed.ToString("N");
        }

        private static FileRead ReadProtectedFile()
        {
            try
            {
                if (!File.Exists(AppPaths.DeviceIdFile))
                {
                    return new FileRead(FileReadKind.Missing, null);
                }

                var raw = File.ReadAllBytes(AppPaths.DeviceIdFile);
                var plain = ProtectedData.Unprotect(raw, null, DataProtectionScope.LocalMachine);
                var text = Encoding.UTF8.GetString(plain);
                var id = NormalizeGuid(text);
                return new FileRead(FileReadKind.Ok, string.IsNullOrEmpty(id) ? text : id);
            }
            catch (CryptographicException)
            {
                return new FileRead(FileReadKind.Foreign, null);
            }
            catch (IOException)
            {
                return new FileRead(FileReadKind.Locked, null);
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

        private enum FileReadKind
        {
            Missing,
            Ok,
            Foreign,
            Locked
        }

        private sealed class FileRead
        {
            public FileRead(FileReadKind kind, string id)
            {
                Kind = kind;
                Id = id;
            }

            public FileReadKind Kind { get; private set; }

            public string Id { get; private set; }
        }
    }
}
