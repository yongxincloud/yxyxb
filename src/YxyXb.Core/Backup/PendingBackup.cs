using System;
using System.Collections.Generic;
using System.Globalization;
using YxyXb.Core.Schedule;
using YxyXb.Core.Security;

namespace YxyXb.Core.Backup
{
    public sealed class PendingItem
    {
        public string Key { get; set; }

        public string ZipPath { get; set; }

        public string Fingerprint { get; set; }

        public int ZipLevel { get; set; }

        public string PasswordSha256 { get; set; }

        public bool NetworkOk { get; set; }

        public bool FtpOk { get; set; }

        public bool CloudOk { get; set; }
    }

    public sealed class PendingRecord
    {
        public string SourceKind { get; set; }

        public string SourcePath { get; set; }

        public long InstanceId { get; set; }

        public string[] DbNames { get; set; }

        public string CycleUntilUtc { get; set; }

        public PendingItem[] Items { get; set; }
    }

    public sealed class FileSnapshot
    {
        public string SourceKind { get; set; }

        public string SourcePath { get; set; }

        public string Fingerprint { get; set; }

        public int ZipLevel { get; set; }

        public string PasswordSha256 { get; set; }
    }

    public sealed class DatabaseSnapshot
    {
        public long InstanceId { get; set; }

        public string[] DbNames { get; set; }

        public int ZipLevel { get; set; }

        public string PasswordSha256 { get; set; }
    }

    public sealed class PendingDecision
    {
        public bool Reuse { get; set; }

        public bool ResetCycle { get; set; }

        public string Reason { get; set; }

        public PendingItem[] Replace { get; set; }

        public PendingItem[] Keep { get; set; }
    }

    public static class PendingBackup
    {
        public static PendingRecord Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var record = JsonUtil.FromJson<PendingRecord>(json);
                if (record == null || record.Items == null)
                {
                    return null;
                }

                return record;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToJson(PendingRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return JsonUtil.ToJson(record);
        }

        public static bool AllowPack(bool zipDeleted)
        {
            return zipDeleted;
        }

        public static string CycleUntil(string cycleKind, int cycleInterval, DateTime packUtc)
        {
            var next = ScheduleCalculator.NextAfter(cycleKind, cycleInterval, packUtc);
            if (!next.HasValue)
            {
                return null;
            }

            return next.Value.ToString("o");
        }

        public static string[] ChannelsToSend(PendingItem item, bool doNetwork, bool doFtp, bool doCloud)
        {
            var list = new List<string>();
            if (doNetwork && (item == null || !item.NetworkOk))
            {
                list.Add("network");
            }

            if (doFtp && (item == null || !item.FtpOk))
            {
                list.Add("ftp");
            }

            if (doCloud && (item == null || !item.CloudOk))
            {
                list.Add("cloud");
            }

            return list.ToArray();
        }

        public static bool NeedsUpload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud)
        {
            return ChannelsToSend(item, doNetwork, doFtp, doCloud).Length > 0;
        }

        public static PendingDecision DecideFile(PendingRecord pending, FileSnapshot now, Func<string, bool> zipExists)
        {
            var items = ItemsOf(pending);
            if (items.Length == 0)
            {
                return Repack("没有未传完的包", new PendingItem[0], false);
            }

            var item = items[0];
            var same = string.Equals(pending.SourceKind ?? "", now.SourceKind ?? "", StringComparison.OrdinalIgnoreCase)
                && string.Equals(pending.SourcePath ?? "", now.SourcePath ?? "", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Fingerprint ?? "", now.Fingerprint ?? "", StringComparison.Ordinal)
                && item.ZipLevel == now.ZipLevel
                && string.Equals(item.PasswordSha256 ?? "", now.PasswordSha256 ?? "", StringComparison.Ordinal);
            if (!same || !ZipStillThere(item, zipExists))
            {
                return Repack(same ? "本地包不在" : "源或压缩设置已变", items, false);
            }

            return new PendingDecision
            {
                Reuse = true,
                ResetCycle = false,
                Reason = "沿用未传完的包",
                Replace = new PendingItem[0],
                Keep = new[] { item }
            };
        }

        public static PendingDecision DecideDatabase(PendingRecord pending, DatabaseSnapshot now, DateTime utcNow, Func<string, bool> zipExists)
        {
            var items = ItemsOf(pending);
            if (items.Length == 0)
            {
                return Repack("没有未传完的包", new PendingItem[0], true);
            }

            if (pending.InstanceId != now.InstanceId || !SameNames(pending.DbNames, now.DbNames) || !SameZipSettings(items, now))
            {
                return Repack("实例、库列表或压缩设置已变", items, true);
            }

            DateTime until;
            if (!string.IsNullOrEmpty(pending.CycleUntilUtc)
                && DateTime.TryParse(pending.CycleUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until)
                && utcNow >= until)
            {
                return Repack("到了下一轮计划时间", items, true);
            }

            var missing = new List<PendingItem>();
            var keep = new List<PendingItem>();
            for (var i = 0; i < items.Length; i++)
            {
                if (ZipStillThere(items[i], zipExists))
                {
                    keep.Add(items[i]);
                }
                else
                {
                    missing.Add(items[i]);
                }
            }

            if (missing.Count == 0)
            {
                return new PendingDecision
                {
                    Reuse = true,
                    ResetCycle = false,
                    Reason = "沿用未传完的包",
                    Replace = new PendingItem[0],
                    Keep = keep.ToArray()
                };
            }

            return new PendingDecision
            {
                Reuse = false,
                ResetCycle = false,
                Reason = "缺少本地包",
                Replace = missing.ToArray(),
                Keep = keep.ToArray()
            };
        }

        public static PendingRecord RemoveItems(PendingRecord record, PendingItem[] remove)
        {
            if (record == null)
            {
                return new PendingRecord { Items = new PendingItem[0] };
            }

            var keep = new List<PendingItem>();
            var items = record.Items ?? new PendingItem[0];
            for (var i = 0; i < items.Length; i++)
            {
                if (!Listed(remove, items[i]))
                {
                    keep.Add(items[i]);
                }
            }

            record.Items = keep.ToArray();
            return record;
        }

        public static void Upsert(PendingRecord record, PendingItem item)
        {
            var list = new List<PendingItem>();
            var items = record.Items ?? new PendingItem[0];
            for (var i = 0; i < items.Length; i++)
            {
                if (!string.Equals(items[i].Key ?? "", item.Key ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(items[i]);
                }
            }

            list.Add(item);
            record.Items = list.ToArray();
        }

        private static PendingDecision Repack(string reason, PendingItem[] replace, bool resetCycle)
        {
            return new PendingDecision
            {
                Reuse = false,
                ResetCycle = resetCycle,
                Reason = reason,
                Replace = replace ?? new PendingItem[0],
                Keep = new PendingItem[0]
            };
        }

        private static PendingItem[] ItemsOf(PendingRecord pending)
        {
            if (pending == null || pending.Items == null)
            {
                return new PendingItem[0];
            }

            return pending.Items;
        }

        private static bool ZipStillThere(PendingItem item, Func<string, bool> zipExists)
        {
            if (item == null || string.IsNullOrEmpty(item.ZipPath) || zipExists == null)
            {
                return false;
            }

            return zipExists(item.ZipPath);
        }

        private static bool SameZipSettings(PendingItem[] items, DatabaseSnapshot now)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].ZipLevel != now.ZipLevel
                    || !string.Equals(items[i].PasswordSha256 ?? "", now.PasswordSha256 ?? "", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameNames(string[] left, string[] right)
        {
            var a = SortCopy(left);
            var b = SortCopy(right);
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] SortCopy(string[] names)
        {
            var copy = names == null ? new string[0] : (string[])names.Clone();
            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }

        private static bool Listed(PendingItem[] remove, PendingItem item)
        {
            if (remove == null || item == null)
            {
                return false;
            }

            for (var i = 0; i < remove.Length; i++)
            {
                if (string.Equals(remove[i].ZipPath ?? "", item.ZipPath ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
