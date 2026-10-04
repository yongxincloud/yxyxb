# 任务完成后邮件发送策略

**日期：** 2026-10-04  
**状态：** 待审阅  
**范围：** 设置 EMAIL 页签、引擎发信判定。不改网站。

## 目标

SMTP 配好后，在 EMAIL 页签控制「要不要发、什么时候发」。单个任务仍要勾「完成后发邮件」才会发。

## 已确认

- 总开关 + 条件在 EMAIL 页签；任务勾选保留。两边都开才发。
- SMTP 未配好：页签不出现总开关和下拉。
- 配好的条件：主机、发件人、收件人都不空。
- 下拉三项：失败 / 成功 / 所有。勾上总开关后默认「失败」。
- 总开关默认关闭。

## 行为

引擎发信当且仅当：

1. SMTP 已配（同上）。
2. `SendAfterTask` 为真。
3. `SendWhen` 对上本次结果：`fail` 仅失败，`success` 仅成功，`all` 都发。
4. 该任务 `DoEmail` 为真。

「成功」：备份与已启用的网络 / FTP / 云都成功（与现有 `distOk` 一致）。过程抛错或 `distOk == false` 算失败。

发信失败只追加日志（「邮件失败：…」），不把已成功的备份改成失败。`DoDeleteNow` 仍要求已启用的邮件发出去，否则不删本地包。

任务对话框勾选文案不变。SMTP 未配或总开关关时，勾了也不发。

EMAIL 页签：总开关「任务执行后发送邮件」；勾上后显示下拉「失败 / 成功 / 所有」。保存仍走现有 SMTP 保存按钮。

## 存储

写进现有 `smtp` 传输 JSON（`SmtpSettingsDto`），不加列：

- `SendAfterTask`：bool，默认 false。
- `SendWhen`：`fail` | `success` | `all`，缺省或非法当 `fail`。

## 代码落点

- `SmtpSettingsDto` 两字段。
- `MailUtil` 或新小函数：`ShouldSend(smtp, doEmail, success)`。
- `DistChannel.Finish` 与各 Runner 失败分支（如 `FileBackupRunner` catch）都走该判定，禁止只改成功路径。
- `SettingsPage` EMAIL 页：按是否配好显示/隐藏；Load/Save 读写两字段。
- 测试：未配不发；总开关关不发；任务未勾不发；fail/success/all 各一条。

## 不做

- 不按任务覆盖发送条件。
- 不改邮件正文模板（仍用现有主题/正文/页脚）。
- 不改网站。
