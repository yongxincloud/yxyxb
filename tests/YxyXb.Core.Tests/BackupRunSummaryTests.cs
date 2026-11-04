using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class BackupRunSummaryTests
    {
        [Test]
        public void MailStats_ReportSinglePackageAndIndependentNotificationState()
        {
            var stats = new BackupRunStats
            {
                DiscoveredFiles = 10,
                PackedFiles = 9,
                SkippedFiles = 1,
                SourceBytes = 2048
            };

            var mail = BackupRunSummary.ToMailStats(stats, 1024, true, true);

            Assert.That(mail.FileStats, Is.EqualTo("发现 10，打包 9，跳过 1"));
            Assert.That(mail.PackageStats, Is.EqualTo("共 1 个包，上传成功 1/1"));
            Assert.That(mail.SizeStats, Is.EqualTo("源 2.0 KB，压缩后 1.0 KB，已上传 1.0 KB"));
        }

        [Test]
        public void ResultCode_SeparatesDataAndNotificationOutcomes()
        {
            Assert.That(BackupRunSummary.ResultCode(true, true), Is.EqualTo("ok"));
            Assert.That(BackupRunSummary.ResultCode(true, false), Is.EqualTo("warn"));
            Assert.That(BackupRunSummary.ResultCode(false, true), Is.EqualTo("error"));
            Assert.That(BackupRunSummary.ResultCode(false, false), Is.EqualTo("error"));
        }

        [Test]
        public void ManualMessage_ExplainsWarning()
        {
            Assert.That(BackupRunSummary.ManualMessage("ok"), Is.EqualTo("执行成功。"));
            Assert.That(BackupRunSummary.ManualMessage("warn"), Is.EqualTo("执行成功，通知发送失败。"));
            Assert.That(BackupRunSummary.ManualMessage("error"), Is.EqualTo("执行失败。"));
        }
    }
}

