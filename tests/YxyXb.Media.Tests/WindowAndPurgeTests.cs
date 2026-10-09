using System;
using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Schedule;
using YxyXb.Media;
using YxyXb.Media.Disks;
using YxyXb.Media.Sync;

namespace YxyXb.Media.Tests
{
    [TestFixture]
    public sealed class WindowAndPurgeTests
    {
        [Test]
        public void Window_RejectsSameStartEnd()
        {
            Assert.That(WindowClock.Validate("22:00", "22:00"), Is.Not.Null);
            Assert.That(WindowClock.Validate("22:00", "23:00"), Is.Null);
        }

        [Test]
        public void Align_KeepsTimeInsideWindow()
        {
            var now = new DateTime(2026, 10, 9, 18, 0, 0);
            var next = new DateTime(2026, 10, 10, 22, 10, 0);
            var aligned = WindowClock.AlignNextRunLocal(next, now, "22:00", "23:00");
            Assert.That(aligned, Is.EqualTo(next));
        }

        [Test]
        public void Align_PullsMissedClockBackToSameDayWindowStart()
        {
            var now = new DateTime(2026, 10, 9, 18, 0, 0);
            var next = new DateTime(2026, 10, 10, 22, 10, 0);
            var aligned = WindowClock.AlignNextRunLocal(next, now, "21:00", "21:30");
            Assert.That(aligned, Is.EqualTo(new DateTime(2026, 10, 10, 21, 0, 0)));
        }

        [Test]
        public void Align_BeforeWindowUsesThatDaysStart()
        {
            var now = new DateTime(2026, 10, 9, 17, 0, 0);
            var next = new DateTime(2026, 10, 9, 17, 2, 0);
            var aligned = WindowClock.AlignNextRunLocal(next, now, "22:00", "23:00");
            Assert.That(aligned, Is.EqualTo(new DateTime(2026, 10, 9, 22, 0, 0)));
        }

        [Test]
        public void Align_AfterWindowClosedMovesToNextStart()
        {
            var now = new DateTime(2026, 10, 9, 23, 10, 0);
            var next = new DateTime(2026, 10, 9, 23, 30, 0);
            var aligned = WindowClock.AlignNextRunLocal(next, now, "22:00", "23:00");
            Assert.That(aligned, Is.EqualTo(new DateTime(2026, 10, 10, 22, 0, 0)));
        }

        [Test]
        public void Window_CrossMidnight()
        {
            var late = new DateTime(2026, 1, 1, 23, 0, 0);
            var early = new DateTime(2026, 1, 2, 1, 0, 0);
            var noon = new DateTime(2026, 1, 2, 12, 0, 0);
            Assert.That(WindowClock.IsInWindow(late, "22:00", "02:00"), Is.True);
            Assert.That(WindowClock.IsInWindow(early, "22:00", "02:00"), Is.True);
            Assert.That(WindowClock.IsInWindow(noon, "22:00", "02:00"), Is.False);
        }

        [Test]
        public void Purge_KeepsRecentMAndWithinD()
        {
            var versions = new List<VersionManifest>
            {
                Manifest("20260101-100000", "a.zip"),
                Manifest("20260108-100000", "b.zip"),
                Manifest("20260109-100000", "c.zip")
            };
            var now = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var keep = VersionPurge.KeepFiles(versions, 2, 7, now);
            Assert.That(keep, Does.Contain("c.zip"));
            Assert.That(keep, Does.Contain("b.zip"));
            Assert.That(keep, Does.Not.Contain("a.zip"));
        }

        [Test]
        public void Module_Ready()
        {
            Assert.That(MediaModule.StatusText(), Does.Contain("登记"));
        }

        [Test]
        public void ExternalBus_UsbSsdViaUasp()
        {
            var bus = ExternalBus.Resolve(1, @"SCSI\DISK&VEN_WD", "SCSI", "External hard disk media", @"USB\VID_152D");
            Assert.That(bus, Is.EqualTo("USB"));
            Assert.That(ExternalBus.IsAllowed(bus), Is.True);
        }

        [Test]
        public void SameDisk_AcceptsUnreadableCapacity()
        {
            var live = new LiveDiskDto
            {
                SerialNumber = "0B806B006AE8",
                DiskSignature = "wmi-1",
                CapacityGb = 0
            };
            Assert.That(DiskEnumerator.SameDisk(live, "0B806B006AE8", "wmi-1", 14), Is.True);
            live.CapacityGb = 14;
            live.DiskSignature = "A1B2C3D4";
            Assert.That(DiskEnumerator.SameDisk(live, "0B806B006AE8", "wmi-1", 14), Is.True);
            live.CapacityGb = 15;
            live.DiskSignature = "wmi-1";
            Assert.That(DiskEnumerator.SameDisk(live, "0B806B006AE8", "wmi-1", 14), Is.False);
        }

        [Test]
        public void ExternalBus_InternalSataRejected()
        {
            var bus = ExternalBus.Resolve(11, @"SCSI\DISK&VEN_SAMSUNG", "IDE", "Fixed hard disk media", @"PCI\VEN_8086");
            Assert.That(ExternalBus.IsAllowed(bus), Is.False);
        }

        private static VersionManifest Manifest(string time, string file)
        {
            return new VersionManifest
            {
                Time = time,
                Files = new List<FileStamp> { new FileStamp { Rel = file, Size = 1, Mtime = time } }
            };
        }
    }
}
