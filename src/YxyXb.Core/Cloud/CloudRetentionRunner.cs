using System;
using System.Collections.Generic;
using System.IO;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Cloud
{
    public sealed class CloudRetentionResult
    {
        public int DeletedCount { get; set; }

        public int DeletedUnitCount { get; set; }
    }

    public static class CloudRetentionRunner
    {
        public static CloudRetentionResult DeleteExpired(
            ICloudObjectCatalog catalog,
            string productPrefix,
            DateTime olderThanUtc,
            Action<IList<string>> deleteBatch,
            Action<string> onDeleted)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            if (string.IsNullOrWhiteSpace(productPrefix)
                || !productPrefix.EndsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("云清理产品前缀为空或不完整，已停止。");
            }

            if (deleteBatch == null)
            {
                throw new ArgumentNullException("deleteBatch");
            }

            var entries = catalog.List(productPrefix) ?? new List<CloudObjectEntry>();
            var objects = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null
                    || string.IsNullOrEmpty(entry.Key)
                    || !entry.Key.StartsWith(productPrefix, StringComparison.Ordinal)
                    || !CloudObjectKeys.IsPackageObject(entry.Key))
                {
                    continue;
                }

                objects[entry.Key] = entry.ModifiedUtc;
            }

            var manifests = new Dictionary<string, string>(StringComparer.Ordinal);
            var parts = new Dictionary<string, List<CloudPart>>(StringComparer.Ordinal);
            var standalone = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            foreach (var pair in objects)
            {
                if (CloudObjectKeys.IsManifest(pair.Key))
                {
                    var identity = ManifestIdentity(pair.Key);
                    if (identity == null || manifests.ContainsKey(identity))
                    {
                        continue;
                    }

                    manifests[identity] = pair.Key;
                    continue;
                }

                CloudPart part;
                if (TryPart(pair.Key, out part))
                {
                    List<CloudPart> list;
                    if (!parts.TryGetValue(part.Identity, out list))
                    {
                        list = new List<CloudPart>();
                        parts[part.Identity] = list;
                    }

                    list.Add(part);
                    continue;
                }

                if (pair.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    standalone[pair.Key] = pair.Value;
                }
            }

            var result = new CloudRetentionResult();
            var manifestIdentities = new HashSet<string>(manifests.Keys, StringComparer.Ordinal);
            foreach (var identity in SortedKeys(manifests))
            {
                List<CloudPart> unitParts;
                if (!parts.TryGetValue(identity, out unitParts) || !Continuous(unitParts))
                {
                    continue;
                }

                unitParts.Sort(delegate (CloudPart left, CloudPart right)
                {
                    return left.Number.CompareTo(right.Number);
                });

                var manifestKey = manifests[identity];
                var allOld = IsOld(objects[manifestKey], olderThanUtc);
                for (var i = 0; i < unitParts.Count && allOld; i++)
                {
                    allOld = IsOld(objects[unitParts[i].Key], olderThanUtc);
                }

                if (!allOld)
                {
                    continue;
                }

                var partKeys = new List<string>();
                for (var i = 0; i < unitParts.Count; i++)
                {
                    partKeys.Add(unitParts[i].Key);
                }

                deleteBatch(partKeys);
                Notify(partKeys, onDeleted);
                result.DeletedCount += partKeys.Count;

                var manifestBatch = new List<string> { manifestKey };
                deleteBatch(manifestBatch);
                Notify(manifestBatch, onDeleted);
                result.DeletedCount++;
                result.DeletedUnitCount++;
            }

            var standaloneKeys = new List<string>();
            foreach (var pair in standalone)
            {
                if (!manifestIdentities.Contains(ZipIdentity(pair.Key))
                    && IsOld(pair.Value, olderThanUtc))
                {
                    standaloneKeys.Add(pair.Key);
                }
            }

            standaloneKeys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < standaloneKeys.Count; i++)
            {
                var batch = new List<string> { standaloneKeys[i] };
                deleteBatch(batch);
                Notify(batch, onDeleted);
                result.DeletedCount++;
            }

            return result;
        }

        private static IEnumerable<string> SortedKeys(Dictionary<string, string> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static bool Continuous(List<CloudPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return false;
            }

            var numbers = new HashSet<int>();
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts[i].Number < 1 || !numbers.Add(parts[i].Number))
                {
                    return false;
                }
            }

            for (var number = 1; number <= parts.Count; number++)
            {
                if (!numbers.Contains(number))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsOld(DateTime time, DateTime olderThanUtc)
        {
            return time != DateTime.MinValue && time < olderThanUtc;
        }

        private static void Notify(IList<string> keys, Action<string> onDeleted)
        {
            if (onDeleted == null)
            {
                return;
            }

            for (var i = 0; i < keys.Count; i++)
            {
                try
                {
                    onDeleted(keys[i]);
                }
                catch (Exception)
                {
                }
            }
        }

        private static string ManifestIdentity(string key)
        {
            const string suffix = ".manifest.json";
            if (!key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return key.Substring(0, key.Length - suffix.Length);
        }

        private static string ZipIdentity(string key)
        {
            return key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? key.Substring(0, key.Length - ".zip".Length)
                : null;
        }

        private static bool TryPart(string key, out CloudPart part)
        {
            part = null;
            if (!key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var nameStart = key.LastIndexOf('/') + 1;
            var name = key.Substring(nameStart);
            var marker = name.LastIndexOf(".part", StringComparison.OrdinalIgnoreCase);
            if (marker <= 0)
            {
                return false;
            }

            var numberText = name.Substring(marker + ".part".Length, name.Length - marker - ".part".Length - 4);
            int number;
            if (!int.TryParse(numberText, out number) || number <= 0)
            {
                return false;
            }

            part = new CloudPart
            {
                Identity = key.Substring(0, nameStart) + name.Substring(0, marker),
                Number = number,
                Key = key
            };
            return true;
        }

        private sealed class CloudPart
        {
            public string Identity;
            public int Number;
            public string Key;
        }
    }
}
