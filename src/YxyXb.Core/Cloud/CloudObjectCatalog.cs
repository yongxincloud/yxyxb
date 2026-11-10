using System;
using System.Collections.Generic;

namespace YxyXb.Core.Cloud
{
    public sealed class CloudObjectEntry
    {
        public string Key { get; set; }

        public DateTime ModifiedUtc { get; set; }
    }

    public interface ICloudObjectCatalog
    {
        IList<CloudObjectEntry> List(string prefix);
    }
}
