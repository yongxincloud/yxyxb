using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Transfer;

namespace YxyXb.Core.Tests
{
    public class MailUtilTests
    {
        [Test]
        public void Compose_RendersRunStatisticsAndKeepsLegacyVariables()
        {
            var settings = new SmtpSettingsDto
            {
                Subject = "{任务}-{文件统计}",
                Body = "{结果}|{包统计}|{大小统计}|{客户标识}|{详情}"
            };
            var stats = new MailRunStats
            {
                FileStats = "发现 10，打包 9，跳过 1",
                PackageStats = "共 1 个包，上传成功 1/1",
                SizeStats = "源 2.0 KB，压缩后 1.0 KB，已上传 0 B"
            };

            string subject;
            string body;
            MailUtil.Compose(settings, "文件任务", true, "说明", "客户A", stats, out subject, out body);

            Assert.That(subject, Is.EqualTo("文件任务-发现 10，打包 9，跳过 1"));
            Assert.That(body, Is.EqualTo("成功|共 1 个包，上传成功 1/1|源 2.0 KB，压缩后 1.0 KB，已上传 0 B|客户A|说明"));
        }

        [Test]
        public void Compose_OldOverload_StillUsesEmptyStatistics()
        {
            var settings = new SmtpSettingsDto
            {
                Subject = "旧标题",
                Body = "旧正文"
            };

            string subject;
            string body;
            MailUtil.Compose(settings, "任务", false, "详情", "客户", out subject, out body);

            Assert.That(subject, Is.EqualTo("旧标题"));
            Assert.That(body, Is.EqualTo("旧正文"));
        }
    }
}

