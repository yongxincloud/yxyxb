using System;
using System.Net;
using System.Net.Mail;
using YxyXb.Contracts.Dto;

namespace YxyXb.Core.Transfer
{
    public static class MailUtil
    {
        public const string DefaultSubject = "{结果} - {任务}";

        public const string DefaultBody = "任务：{任务}\r\n结果：{结果}\r\n时间：{时间}\r\n{详情}";

        public static void Test(SmtpSettingsDto settings)
        {
            SendResult(settings, "测试", true, "这是一封测试邮件。");
        }

        public static bool SmtpReady(SmtpSettingsDto settings)
        {
            return settings != null
                && !string.IsNullOrWhiteSpace(settings.Host)
                && !string.IsNullOrWhiteSpace(settings.From)
                && !string.IsNullOrWhiteSpace(settings.To);
        }

        public static string NormalizeWhen(string value)
        {
            if (string.Equals(value, "success", StringComparison.OrdinalIgnoreCase))
            {
                return "success";
            }

            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            {
                return "all";
            }

            return "fail";
        }

        public static bool ShouldSend(SmtpSettingsDto settings, bool doEmail, bool success)
        {
            if (!doEmail || !SmtpReady(settings) || !settings.SendAfterTask)
            {
                return false;
            }

            var when = NormalizeWhen(settings.SendWhen);
            if (when == "all")
            {
                return true;
            }

            if (when == "success")
            {
                return success;
            }

            return !success;
        }

        public static void SendResult(SmtpSettingsDto settings, string taskName, bool ok, string detail)
        {
            string subject;
            string body;
            Compose(settings, taskName, ok, detail, out subject, out body);
            Send(settings, subject, body);
        }

        public static void Compose(SmtpSettingsDto settings, string taskName, bool ok, string detail, out string subject, out string body)
        {
            var result = ok ? "成功" : "失败";
            var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var subjectText = settings == null || string.IsNullOrWhiteSpace(settings.Subject) ? DefaultSubject : settings.Subject.Trim();
            var bodyText = settings == null || string.IsNullOrWhiteSpace(settings.Body) ? DefaultBody : settings.Body;
            subject = Fill(subjectText, taskName, result, time, detail);
            body = Fill(bodyText, taskName, result, time, detail);
            var footer = settings == null || string.IsNullOrWhiteSpace(settings.Footer) ? "" : settings.Footer;
            footer = Fill(footer, taskName, result, time, detail);
            if (!string.IsNullOrEmpty(footer))
            {
                body = body.TrimEnd() + "\r\n\r\n" + footer;
            }
        }

        private static string Fill(string template, string taskName, string result, string time, string detail)
        {
            return (template ?? "")
                .Replace("{任务}", taskName ?? "")
                .Replace("{结果}", result ?? "")
                .Replace("{时间}", time ?? "")
                .Replace("{详情}", detail ?? "");
        }

        public static void Send(SmtpSettingsDto settings, string subject, string body)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Host) || string.IsNullOrEmpty(settings.To))
            {
                throw new InvalidOperationException("未配置邮件（SMTP / 收件人）。");
            }

            var port = settings.Port <= 0 ? 25 : settings.Port;
            using (var client = new SmtpClient(settings.Host, port))
            {
                client.EnableSsl = settings.Ssl;
                client.Timeout = 30000;
                if (!string.IsNullOrEmpty(settings.User))
                {
                    client.Credentials = new NetworkCredential(settings.User, settings.Password ?? "");
                }

                var from = string.IsNullOrEmpty(settings.From) ? settings.User : settings.From;
                if (string.IsNullOrEmpty(from))
                {
                    throw new InvalidOperationException("请填写发件人。");
                }

                using (var message = new MailMessage(from, settings.To, subject, body))
                {
                    client.Send(message);
                }
            }
        }
    }
}
