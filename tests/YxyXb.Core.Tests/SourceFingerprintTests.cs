using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class SourceFingerprintTests
    {
        [Test]
        public void Compute_SameFilesAnyOrder_SameHash()
        {
            var a = new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "b.txt", Length = 2, LastWriteUtcTicks = 20 },
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 10 }
            };
            var b = new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 10 },
                new FingerprintFile { RelativePath = "b.txt", Length = 2, LastWriteUtcTicks = 20 }
            };
            Assert.That(SourceFingerprint.Compute(a), Is.EqualTo(SourceFingerprint.Compute(b)));
        }

        [Test]
        public void Compute_LengthOrTicksChange_DifferentHash()
        {
            var origin = SourceFingerprint.Compute(new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 10 }
            });
            var sized = SourceFingerprint.Compute(new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 2, LastWriteUtcTicks = 10 }
            });
            var timed = SourceFingerprint.Compute(new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 11 }
            });
            Assert.That(sized, Is.Not.EqualTo(origin));
            Assert.That(timed, Is.Not.EqualTo(origin));
        }

        [Test]
        public void PasswordHash_EmptyIsBlank_ValueChangesHash()
        {
            Assert.That(SourceFingerprint.PasswordHash(null), Is.EqualTo(""));
            Assert.That(SourceFingerprint.PasswordHash(""), Is.EqualTo(""));
            Assert.That(SourceFingerprint.PasswordHash("甲"), Is.Not.EqualTo(SourceFingerprint.PasswordHash("乙")));
            Assert.That(SourceFingerprint.PasswordHash("甲"), Is.Not.EqualTo(""));
        }
    }
}
