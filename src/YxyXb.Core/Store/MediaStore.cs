using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;

namespace YxyXb.Core.Store
{
    public static class MediaStore
    {
        public static IList<MediaRecordDto> List(SqliteDb db)
        {
            var table = db.Query("SELECT * FROM media ORDER BY id");
            var list = new List<MediaRecordDto>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(Read(row));
            }

            return list;
        }

        public static MediaRecordDto GetById(SqliteDb db, long id)
        {
            var table = db.Query("SELECT * FROM media WHERE id=@id", new SQLiteParameter("@id", id));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static MediaRecordDto Find(SqliteDb db, string serial, string signature, int capacityGb)
        {
            var table = db.Query(
                "SELECT * FROM media WHERE serial_number=@s AND disk_signature=@g AND capacity_gb=@c",
                new SQLiteParameter("@s", serial ?? ""),
                new SQLiteParameter("@g", signature ?? ""),
                new SQLiteParameter("@c", capacityGb));
            if (table.Rows.Count == 0)
            {
                return null;
            }

            return Read(table.Rows[0]);
        }

        public static long Insert(SqliteDb db, MediaRecordDto item)
        {
            db.Execute(
                @"INSERT INTO media (serial_number, disk_signature, capacity_gb, volume_guid, registered_utc, last_seen_utc, registered_device_id, tamper_pending)
                  VALUES (@s,@g,@c,@v,@u,@u,@d,0)",
                new SQLiteParameter("@s", item.SerialNumber ?? ""),
                new SQLiteParameter("@g", item.DiskSignature ?? ""),
                new SQLiteParameter("@c", item.CapacityGb),
                new SQLiteParameter("@v", (object)item.VolumeGuid ?? DBNull.Value),
                new SQLiteParameter("@u", SqliteDb.UtcNowText()),
                new SQLiteParameter("@d", (object)item.RegisteredDeviceId ?? DBNull.Value));
            return Convert.ToInt64(db.ExecuteScalar("SELECT last_insert_rowid()"));
        }

        public static void Touch(SqliteDb db, long id, string volumeGuid)
        {
            db.Execute(
                "UPDATE media SET last_seen_utc=@u, volume_guid=@v WHERE id=@id",
                new SQLiteParameter("@u", SqliteDb.UtcNowText()),
                new SQLiteParameter("@v", (object)volumeGuid ?? DBNull.Value),
                new SQLiteParameter("@id", id));
        }

        public static void RequestLetter(SqliteDb db, long id, string cmd)
        {
            db.Execute(
                "UPDATE media SET letter_cmd=@c, letter_result=NULL WHERE id=@id",
                new SQLiteParameter("@c", cmd),
                new SQLiteParameter("@id", id));
        }

        public static void FinishLetter(SqliteDb db, long id, string result)
        {
            db.Execute(
                "UPDATE media SET letter_cmd=NULL, letter_result=@r WHERE id=@id",
                new SQLiteParameter("@r", (object)result ?? DBNull.Value),
                new SQLiteParameter("@id", id));
        }

        public static void SetAutoHeld(SqliteDb db, long id, bool held)
        {
            db.Execute(
                "UPDATE media SET auto_held=@h WHERE id=@id",
                new SQLiteParameter("@h", held ? 1 : 0),
                new SQLiteParameter("@id", id));
        }

        public static void SetTamper(SqliteDb db, long id, bool pending)
        {
            db.Execute(
                "UPDATE media SET tamper_pending=@p WHERE id=@id",
                new SQLiteParameter("@p", pending ? 1 : 0),
                new SQLiteParameter("@id", id));
        }

        public static void Delete(SqliteDb db, long id)
        {
            db.Execute("DELETE FROM media_tasks WHERE media_id=@id", new SQLiteParameter("@id", id));
            db.Execute("DELETE FROM media WHERE id=@id", new SQLiteParameter("@id", id));
        }

        private static MediaRecordDto Read(DataRow row)
        {
            return new MediaRecordDto
            {
                Id = Convert.ToInt64(row["id"]),
                SerialNumber = Convert.ToString(row["serial_number"]),
                DiskSignature = Convert.ToString(row["disk_signature"]),
                CapacityGb = Convert.ToInt32(row["capacity_gb"]),
                VolumeGuid = row.Table.Columns.Contains("volume_guid") && row["volume_guid"] != DBNull.Value
                    ? Convert.ToString(row["volume_guid"])
                    : null,
                RegisteredDeviceId = GetStr(row, "registered_device_id"),
                RegisteredUtc = SqliteDb.ParseUtc(Convert.ToString(row["registered_utc"])),
                LastSeenUtc = row["last_seen_utc"] == DBNull.Value ? (DateTime?)null : SqliteDb.ParseUtc(Convert.ToString(row["last_seen_utc"])),
                TamperPending = GetInt(row, "tamper_pending") != 0,
                AutoHeld = GetInt(row, "auto_held") != 0,
                LetterCmd = GetStr(row, "letter_cmd"),
                LetterResult = GetStr(row, "letter_result")
            };
        }

        private static string GetStr(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            {
                return null;
            }

            return Convert.ToString(row[col]);
        }

        private static int GetInt(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(row[col]);
        }
    }
}
