using System;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Tests
{
    public class ObjectSliceTests
    {
        [Test]
        public void Plan_UsesWholePutUntilFiveMegabytes()
        {
            var small = ObjectSlices.Plan(100);
            Assert.That(small.Simple, Is.True);
            Assert.That(small.PartSize, Is.EqualTo(100));
            Assert.That(small.PartCount, Is.EqualTo(1));

            var edge = ObjectSlices.Plan(ObjectSlices.MinPartBytes);
            Assert.That(edge.Simple, Is.True);
            Assert.That(edge.PartCount, Is.EqualTo(1));
        }

        [Test]
        public void Plan_SplitsAboveFiveMegabytesAndGrowsPastTenThousand()
        {
            var split = ObjectSlices.Plan(ObjectSlices.MinPartBytes + 1);
            Assert.That(split.Simple, Is.False);
            Assert.That(split.PartSize, Is.EqualTo(ObjectSlices.MinPartBytes));
            Assert.That(split.PartCount, Is.EqualTo(2));

            var exact = ObjectSlices.Plan(ObjectSlices.MinPartBytes * ObjectSlices.MaxPartCount);
            Assert.That(exact.PartSize, Is.EqualTo(ObjectSlices.MinPartBytes));
            Assert.That(exact.PartCount, Is.EqualTo(ObjectSlices.MaxPartCount));

            var over = ObjectSlices.Plan(ObjectSlices.MinPartBytes * (long)ObjectSlices.MaxPartCount + 1);
            Assert.That(over.PartCount, Is.LessThanOrEqualTo(ObjectSlices.MaxPartCount));
            Assert.That(over.PartSize, Is.GreaterThan(ObjectSlices.MinPartBytes));
        }

        [Test]
        public void Urls_BuildVirtualHostAndCtyunPath()
        {
            Assert.That(
                ObjectUrls.Aliyun("backup", "oss-cn-hangzhou", "客户/a.zip"),
                Is.EqualTo("https://backup.oss-cn-hangzhou.aliyuncs.com/%E5%AE%A2%E6%88%B7/a.zip"));
            Assert.That(
                ObjectUrls.Aliyun("backup", "https://oss-cn-beijing.aliyuncs.com/ignored", "a.zip"),
                Is.EqualTo("https://backup.oss-cn-beijing.aliyuncs.com/a.zip"));
            Assert.That(
                ObjectUrls.Tencent("backup-1250000000", "ap-guangzhou", "a.zip"),
                Is.EqualTo("https://backup-1250000000.cos.ap-guangzhou.myqcloud.com/a.zip"));
            Assert.That(
                ObjectUrls.Ctyun("jiangsu-10.zos.ctyun.cn", "backup", "dir/a.zip"),
                Is.EqualTo("https://jiangsu-10.zos.ctyun.cn/backup/dir/a.zip"));
        }

        [Test]
        public void Regions_DefaultToHangzhouAndGuangzhou()
        {
            Assert.That(CloudRegions.Aliyun[0].Id, Is.EqualTo("oss-cn-hangzhou"));
            Assert.That(CloudRegions.Tencent[0].Id, Is.EqualTo("ap-guangzhou"));
            Assert.That(CloudRegions.Aliyun[CloudRegions.Aliyun.Length - 1].Label, Is.EqualTo("自定义 Endpoint"));
            Assert.That(CloudRegions.Tencent[CloudRegions.Tencent.Length - 1].Id, Is.EqualTo(""));
            Assert.That(Array.Exists(CloudRegions.Aliyun, item => item.Id == "oss-cn-hongkong"), Is.True);
            Assert.That(Array.Exists(CloudRegions.Aliyun, item => item.Id.IndexOf("finance", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }
    }
}
