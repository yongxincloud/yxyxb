using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;
using YxyXb.Core.Transfer;

namespace YxyXb.Core.Tests
{
    public class FtpMdtmTests
    {
        [Test]
        public void TryParseUtc_Rfc213_IsUtc()
        {
            DateTime utc;
            Assert.That(FtpMdtm.TryParseUtc("213 20200102030405\r\n", out utc), Is.True);
            Assert.That(utc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(utc, Is.EqualTo(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
        }

        [Test]
        public void TryParseUtc_FractionAndGluedCode_SameInstant()
        {
            DateTime plain;
            DateTime frac;
            DateTime glued;
            Assert.That(FtpMdtm.TryParseUtc("213 20200102030405.5", out frac), Is.True);
            Assert.That(FtpMdtm.TryParseUtc("213 20200102030405", out plain), Is.True);
            Assert.That(FtpMdtm.TryParseUtc("21320200102030405", out glued), Is.True);
            Assert.That(frac, Is.EqualTo(plain.AddMilliseconds(500)));
            Assert.That(glued, Is.EqualTo(plain));
        }

        [Test]
        public void TryParseUtc_MissingTime_False()
        {
            DateTime utc;
            Assert.That(FtpMdtm.TryParseUtc(null, out utc), Is.False);
            Assert.That(FtpMdtm.TryParseUtc("", out utc), Is.False);
            Assert.That(FtpMdtm.TryParseUtc("550 MDTM not supported", out utc), Is.False);
            Assert.That(FtpMdtm.TryParseUtc("213", out utc), Is.False);
        }

        [Test]
        public void ApplyUtcWriteTime_SameServerTime_FingerprintStable()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yxy-mdtm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                DateTime utc;
                Assert.That(FtpMdtm.TryParseUtc("213 20200102030405", out utc), Is.True);
                var first = Path.Combine(dir, "a.txt");
                var second = Path.Combine(dir, "b.txt");
                var fresh = Path.Combine(dir, "c.txt");
                var bytes = new byte[] { 1, 2, 3 };
                File.WriteAllBytes(first, bytes);
                File.WriteAllBytes(second, bytes);
                File.WriteAllBytes(fresh, bytes);
                FtpMdtm.ApplyUtcWriteTime(first, utc);
                FtpMdtm.ApplyUtcWriteTime(second, utc);

                var stampedA = FingerprintOf(first);
                var stampedB = FingerprintOf(second);
                var downloadedNow = FingerprintOf(fresh);
                Assert.That(stampedA[0].LastWriteUtcTicks, Is.EqualTo(utc.Ticks));
                Assert.That(SourceFingerprint.Compute(stampedA), Is.EqualTo(SourceFingerprint.Compute(stampedB)));
                Assert.That(SourceFingerprint.Compute(downloadedNow), Is.Not.EqualTo(SourceFingerprint.Compute(stampedA)));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static List<FingerprintFile> FingerprintOf(string path)
        {
            var info = new FileInfo(path);
            return new List<FingerprintFile>
            {
                new FingerprintFile
                {
                    RelativePath = "a.txt",
                    Length = info.Length,
                    LastWriteUtcTicks = File.GetLastWriteTimeUtc(path).Ticks
                }
            };
        }
    }
}
