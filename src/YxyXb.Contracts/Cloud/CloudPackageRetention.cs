using System;
using System.Collections.Generic;

namespace YxyXb.Contracts.Cloud
{
    public static class CloudPackageRetention
    {
        public static IList<string> GetExpiredUnitFiles(
            string manifestKey,
            IDictionary<string, DateTime> objectTimes,
            DateTime olderThanUtc)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(manifestKey) || objectTimes == null || !objectTimes.ContainsKey(manifestKey))
            {
                return result;
            }

            var slash = manifestKey.LastIndexOf('/');
            var prefix = slash < 0 ? "" : manifestKey.Substring(0, slash + 1);
            var hasPart = false;
            foreach (var pair in objectTimes)
            {
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal)
                    || pair.Key.Substring(prefix.Length).Contains("/"))
                {
                    continue;
                }

                if (!CloudObjectKeys.IsPackageObject(pair.Key)
                    || pair.Value == DateTime.MinValue
                    || pair.Value >= olderThanUtc)
                {
                    return new List<string>();
                }

                if (!CloudObjectKeys.IsManifest(pair.Key))
                {
                    hasPart = true;
                    result.Add(pair.Key);
                }
            }

            if (!hasPart)
            {
                return new List<string>();
            }

            result.Sort(StringComparer.Ordinal);
            result.Add(manifestKey);
            return result;
        }
    }
}
