using System;
using System.IO;
using NUnit.Framework;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core.Backup;
using Yxy.Core.Data;
using Yxy.Core.Store;
using Yxy.Core.Transfer;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public sealed class PhaseETests
    {
        [Test]
        public void Mail_UsesSubjectBodyAndFooter()
        {
            string subject;
            string body;
            MailUtil.Compose(new SmtpSettingsDto
            {
                Subject = "备份{结果}-{任务}",
                Body = "内容 {详情}",
                Footer = "结尾 {时间}"
            }, "日报", true, "已完成", out subject, out body);
            Assert.That(subject, Is.EqualTo("备份成功-日报"));
            Assert.That(body, Does.StartWith("内容 已完成"));
            Assert.That(body, Does.Contain("结尾 "));
        }

        [Test]
        public void ShouldSend_RequiresReadySwitchWhenAndTask()
        {
            var smtp = new SmtpSettingsDto { Host = "h", From = "a@b.c", To = "d@e.f", SendAfterTask = true, SendWhen = "fail" };
            Assert.That(MailUtil.SmtpReady(smtp), Is.True);
            Assert.That(MailUtil.ShouldSend(smtp, true, false), Is.True);
            Assert.That(MailUtil.ShouldSend(smtp, true, true), Is.False);
            smtp.SendWhen = "success";
            Assert.That(MailUtil.ShouldSend(smtp, true, true), Is.True);
            Assert.That(MailUtil.ShouldSend(smtp, true, false), Is.False);
            smtp.SendWhen = "all";
            Assert.That(MailUtil.ShouldSend(smtp, true, true), Is.True);
            Assert.That(MailUtil.ShouldSend(smtp, true, false), Is.True);
            smtp.SendAfterTask = false;
            Assert.That(MailUtil.ShouldSend(smtp, true, false), Is.False);
            smtp.SendAfterTask = true;
            Assert.That(MailUtil.ShouldSend(smtp, false, false), Is.False);
            Assert.That(MailUtil.ShouldSend(new SmtpSettingsDto { Host = "h", SendAfterTask = true, SendWhen = "all" }, true, false), Is.False);
            Assert.That(MailUtil.NormalizeWhen("x"), Is.EqualTo("fail"));
            Assert.That(MailUtil.NormalizeWhen(null), Is.EqualTo("fail"));
        }

        [Test]
        public void SmtpXfer_KeepsSendPolicy()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-mail-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    SettingsStore.SaveXfer(db, "smtp", new SmtpSettingsDto
                    {
                        Host = "h",
                        From = "a@b.c",
                        To = "d@e.f",
                        SendAfterTask = true,
                        SendWhen = "all"
                    });
                    var loaded = SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp");
                    Assert.That(loaded.SendAfterTask, Is.True);
                    Assert.That(MailUtil.NormalizeWhen(loaded.SendWhen), Is.EqualTo("all"));
                }
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Test]
        public void CloudMask_HidesMiddle()
        {
            Assert.That(CloudMask.Mask("abcdefghijkl"), Is.EqualTo("abcd****ijkl"));
            Assert.That(CloudMask.Mask(""), Is.EqualTo(""));
        }

        [Test]
        public void ObjectKey_UsesProductPrefix()
        {
            var key = CloudObjectKeys.ForZip("用心云备份", "file", "日报", @"D:\a\x.zip");
            Assert.That(key, Does.StartWith("用心云备份/file/日报/"));
            Assert.That(key, Does.EndWith("x.zip"));
            Assert.That(CloudObjectKeys.ProductPrefix("用心云备份"), Is.EqualTo("用心云备份/"));
        }

        [Test]
        public void LocalPurge_DeletesOldZipOnly()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-purge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var oldZip = Path.Combine(root, "old.zip");
            var newZip = Path.Combine(root, "new.zip");
            File.WriteAllText(oldZip, "1");
            File.WriteAllText(newZip, "2");
            File.SetLastWriteTimeUtc(oldZip, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(newZip, DateTime.UtcNow);
            try
            {
                var n = LocalPurge.DeleteOldZips(root, 7, DateTime.UtcNow);
                Assert.That(n, Is.EqualTo(1));
                Assert.That(File.Exists(oldZip), Is.False);
                Assert.That(File.Exists(newZip), Is.True);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void PurgeDue_OncePerDayAfterTime()
        {
            var now = new DateTime(2026, 9, 24, 1, 0, 0);
            Assert.That(PurgeRunner.IsDue(now, "00:30", null), Is.True);
            Assert.That(PurgeRunner.IsDue(now, "02:00", null), Is.False);
            Assert.That(PurgeRunner.IsDue(now, "00:30", new DateTime(2026, 9, 24, 0, 40, 0, DateTimeKind.Local).ToUniversalTime()), Is.False);
        }

        [Test]
        public void AccountReady_NeedsAllFields()
        {
            var acc = new CloudAccountDto { Enabled = true, Bucket = "b", AccessKey = "ak" };
            Assert.That(acc.IsReady, Is.False);
            acc.SecretKey = "sk";
            Assert.That(acc.IsReady, Is.True);
        }
    }
}
