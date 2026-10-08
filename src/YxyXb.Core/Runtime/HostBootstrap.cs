using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Device;
using YxyXb.Core.License;

namespace YxyXb.Core.Runtime
{
    public sealed class HostBootstrap
    {
        public HostBootstrap(SqliteDb db, DeviceInfoDto device, LicenseStatusDto trial)
        {
            Db = db;
            Device = device;
            Trial = trial;
        }

        public SqliteDb Db { get; private set; }

        public DeviceInfoDto Device { get; private set; }

        public LicenseStatusDto Trial { get; private set; }

        public static HostBootstrap Start()
        {
            AppPaths.EnsureDirectories();
            var db = SqliteDb.OpenDefault();
            var device = DeviceIdStore.GetOrCreate(db);
            var trial = TrialClock.Get(db);
            return new HostBootstrap(db, device, trial);
        }
    }
}
