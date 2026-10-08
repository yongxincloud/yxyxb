using System;
using System.Collections.Generic;

namespace YxyXb.Core.Backup
{
    public static class DatabaseScope
    {
        public static IList<string> Pick(IList<string> allUserDbs, string dbScope, string selectedCsv, Func<string, bool> isSystem)
        {
            var all = allUserDbs ?? new string[0];
            if (string.IsNullOrEmpty(dbScope) || dbScope != "selected")
            {
                return all;
            }

            var picked = new List<string>();
            var parts = (selectedCsv ?? "").Split(new[] { ',', ';', '，' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (isSystem != null && isSystem(name))
                {
                    continue;
                }

                for (var j = 0; j < all.Count; j++)
                {
                    if (string.Equals(all[j], name, StringComparison.OrdinalIgnoreCase))
                    {
                        picked.Add(all[j]);
                        break;
                    }
                }
            }

            return picked;
        }
    }
}
