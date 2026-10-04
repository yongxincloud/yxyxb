# 任务完成后邮件发送策略 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**状态：** 已落地（main：`e3b37d7` `8b6e303` `99835df`）。

**Goal:** SMTP 配好后，EMAIL 页签用总开关和下拉（失败/成功/所有）控制发信；任务仍须勾选「完成后发邮件」。

**Architecture:** 两字段进现有 `SmtpSettingsDto`（xfer JSON）。`MailUtil.SmtpReady` / `ShouldSend` 集中判定。`DistChannel.Finish` 与文件/MSSQL/MySQL Runner 的失败 catch 都调用 `ShouldSend`，禁止只改成功路径。设置页按主机+发件人+收件人是否非空显示控件。

**Tech Stack:** .NET Framework 4.8，NUnit，WPF `SettingsPage`，`SettingsStore.GetXfer/SaveXfer`。

## Global Constraints

- 发信四条件同时成立：SMTP 已配（Host、From、To 都不空）、`SendAfterTask`、`SendWhen` 对上结果、任务 `DoEmail`。
- `SendWhen` 仅 `fail` | `success` | `all`；缺省或非法当 `fail`。
- 总开关默认关；勾上后默认 `fail`。
- 发信失败只写日志，不把已成功备份改成失败。`DoDeleteNow`：未实际尝试发信时 `mailOk` 保持 true。
- 不改邮件模板、不加表、不改网站、任务勾选文案不变。
- 提交：`git -c user.name=yxy -c user.email=yxy@local commit`（不改 git config）。

---

### Task 1: ShouldSend 判定与 DTO

**Files:**
- Modify: `src/Yxy.Contracts/Dto/AppSettingsDto.cs`（`SmtpSettingsDto` 末尾）
- Modify: `src/Yxy.Core/Transfer/MailUtil.cs`
- Test: `tests/Yxy.Core.Tests/PhaseETests.cs`

**Interfaces:**
- Consumes: 现有 `SmtpSettingsDto`
- Produces: `SendAfterTask` `bool`；`SendWhen` `string`；`MailUtil.SmtpReady(SmtpSettingsDto settings)`；`MailUtil.ShouldSend(SmtpSettingsDto settings, bool doEmail, bool success)`；`MailUtil.NormalizeWhen(string value)` 返回 `fail`/`success`/`all`

- [ ] **Step 1: Write the failing test**

在 `PhaseETests` 增加：

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter ShouldSend_RequiresReadySwitchWhenAndTask`

Expected: FAIL（无 `ShouldSend` 或属性不存在）

- [ ] **Step 3: Write minimal implementation**

`SmtpSettingsDto` 增加：

```csharp
public bool SendAfterTask { get; set; }
public string SendWhen { get; set; }
```

`MailUtil` 增加：

```csharp
public static bool SmtpReady(SmtpSettingsDto settings)
{
    return settings != null
        && !string.IsNullOrWhiteSpace(settings.Host)
        && !string.IsNullOrWhiteSpace(settings.From)
        && !string.IsNullOrWhiteSpace(settings.To);
}

public static string NormalizeWhen(string value)
{
    if (string.Equals(value, "success", StringComparison.OrdinalIgnoreCase)) return "success";
    if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)) return "all";
    return "fail";
}

public static bool ShouldSend(SmtpSettingsDto settings, bool doEmail, bool success)
{
    if (!doEmail || !SmtpReady(settings) || !settings.SendAfterTask) return false;
    var when = NormalizeWhen(settings.SendWhen);
    if (when == "all") return true;
    if (when == "success") return success;
    return !success;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter ShouldSend_RequiresReadySwitchWhenAndTask`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Contracts/Dto/AppSettingsDto.cs src/Yxy.Core/Transfer/MailUtil.cs tests/Yxy.Core.Tests/PhaseETests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: decide task result mail with smtp policy"
```

---

### Task 2: Finish 与各 Runner 失败分支

**Files:**
- Modify: `src/Yxy.Core/Backup/DistChannel.cs`（`Finish`）
- Modify: `src/Yxy.Core/Backup/FileBackupRunner.cs`（catch 发信）
- Modify: `src/Yxy.Mssql/MssqlRunner.cs`（catch 发信）
- Modify: `src/Yxy.MySql/MysqlRunner.cs`（catch 发信）
- Test: `tests/Yxy.Core.Tests/PhaseETests.cs`（可再加 roundtrip：SaveXfer/GetXfer 保留两字段；若已有 sqlite 测试夹具则用 `Yxy.Core.Tests` 现有 temp db 写法）

**Interfaces:**
- Consumes: Task 1 `ShouldSend`
- Produces: `Finish` 仅在 `ShouldSend(smtp, doEmail, distOk)` 时调用 `SendResult`；三处 catch 用 `ShouldSend(smtp, task.DoEmail, false)` 再 `SendResult`

- [ ] **Step 1: Write the failing test**

若项目已有 `SqliteDb` 临时库测试（如 `BaiduStoreTests` / `MysqlStoreTests`），增加：

```csharp
[Test]
public void SmtpXfer_KeepsSendPolicy()
{
    var dir = Path.Combine(Path.GetTempPath(), "yxy-mail-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var db = new SqliteDb(Path.Combine(dir, "t.db"));
    try
    {
        db.EnsureSchema();
        SettingsStore.SaveXfer(db, "smtp", new SmtpSettingsDto
        {
            Host = "h", From = "a@b.c", To = "d@e.f", SendAfterTask = true, SendWhen = "all"
        });
        var loaded = SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp");
        Assert.That(loaded.SendAfterTask, Is.True);
        Assert.That(MailUtil.NormalizeWhen(loaded.SendWhen), Is.EqualTo("all"));
    }
    finally
    {
        db.Dispose();
        Directory.Delete(dir, true);
    }
}
```

`EnsureSchema` 若方法名不同，改成该测试项目里建库的同一调用（看 `MysqlStoreTests`）。

无法对 `Finish` 做真 SMTP：行为靠 `ShouldSend` + 代码审查三处 catch。本测试只锁 JSON 持久化。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter SmtpXfer_KeepsSendPolicy`

Expected: FAIL 或字段读回 false（若 DTO 已有字段但未 roundtrip 则应 PASS——若 PASS 则本步记下已绿，仍改 Runner）

- [ ] **Step 3: Write minimal implementation**

`DistChannel.Finish` 发信块改为：

```csharp
var smtp = SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp");
if (MailUtil.ShouldSend(smtp, doEmail, distOk))
{
    try
    {
        MailUtil.SendResult(smtp, taskName, distOk, log.ToString());
        log.Append(" 已发邮件。");
    }
    catch (Exception ex)
    {
        mailOk = false;
        log.Append(" 邮件失败：").Append(ex.Message).Append("。");
    }
}
```

三处 catch（文件 / MSSQL / MySQL）把 `if (task.DoEmail)` 换成：

```csharp
var smtp = SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp");
if (MailUtil.ShouldSend(smtp, task.DoEmail, false))
{
    try
    {
        MailUtil.SendResult(smtp, task.Name, false, log.ToString());
    }
    catch
    {
    }
}
```

MSSQL/MySQL 已有 `using Yxy.Core.Transfer` 则不必再加。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter "ShouldSend_RequiresReadySwitchWhenAndTask|SmtpXfer_KeepsSendPolicy"`

Expected: PASS。再 `dotnet build src/Yxy.Engine/Yxy.Engine.csproj src/Yxy.Mssql/Yxy.Mssql.csproj src/Yxy.MySql/Yxy.MySql.csproj`

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Core/Backup/DistChannel.cs src/Yxy.Core/Backup/FileBackupRunner.cs src/Yxy.Mssql/MssqlRunner.cs src/Yxy.MySql/MysqlRunner.cs tests/Yxy.Core.Tests/PhaseETests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: honor smtp send policy in backup mail"
```

---

### Task 3: EMAIL 页签总开关与下拉

**Files:**
- Modify: `src/Yxy.Client/Pages/SettingsPage.cs`（字段、`MailTab`、`Load`、`ReadSmtp`）
- Test: 无 UI 自动化。编译覆盖。判定逻辑已在 Task 1。

**Interfaces:**
- Consumes: Task 1 字段与 `SmtpReady`/`NormalizeWhen`
- Produces: 勾选「任务执行后发送邮件」；勾上后显示下拉失败/成功/所有；SMTP 未就绪则两者 `Collapsed`

- [ ] **Step 1: Write the failing test**

无新测试。确认 Task 1 过滤仍绿。

- [ ] **Step 2: Run test to verify it fails**

跳过（无新红测）。直接实现 UI。

- [ ] **Step 3: Write minimal implementation**

字段：

```csharp
private readonly CheckBox _sendAfterTask;
private readonly ComboBox _sendWhen;
private readonly TextBlock _sendWhenLabel;
```

构造里：

```csharp
_sendAfterTask = new CheckBox { Content = "任务执行后发送邮件", Margin = new Thickness(0, 12, 0, 8) };
_sendWhenLabel = Label("发送条件");
_sendWhen = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
_sendWhen.Items.Add(new ComboBoxItem { Content = "失败", Tag = "fail" });
_sendWhen.Items.Add(new ComboBoxItem { Content = "成功", Tag = "success" });
_sendWhen.Items.Add(new ComboBoxItem { Content = "所有", Tag = "all" });
_sendAfterTask.Checked += delegate { SyncMailPolicyUi(); };
_sendAfterTask.Unchecked += delegate { SyncMailPolicyUi(); };
_smtpHost.LostFocus += delegate { SyncMailPolicyUi(); };
_smtpFrom.LostFocus += delegate { SyncMailPolicyUi(); };
_smtpTo.LostFocus += delegate { SyncMailPolicyUi(); };
```

`MailTab` 在标题/内容说明之前插入 `_sendAfterTask`、`_sendWhenLabel`、`_sendWhen`。

`Load` 在填完 smtp 文本后：

```csharp
_sendAfterTask.IsChecked = smtp.SendAfterTask;
SelectTag(_sendWhen, MailUtil.NormalizeWhen(smtp.SendWhen));
SyncMailPolicyUi();
```

若页面无 `SelectTag`，按 `SettingsPage` 里 `SelectTheme` 同样循环 `ComboBoxItem.Tag`。

`ReadSmtp` 增加：

```csharp
SendAfterTask = _sendAfterTask.IsChecked == true,
SendWhen = MailUtil.NormalizeWhen(TagOf(_sendWhen, "fail")),
```

`TagOf` 可与云备份页相同：选中项 Tag，空则 `fail`。

```csharp
private void SyncMailPolicyUi()
{
    var ready = MailUtil.SmtpReady(new SmtpSettingsDto
    {
        Host = (_smtpHost.Text ?? "").Trim(),
        From = (_smtpFrom.Text ?? "").Trim(),
        To = (_smtpTo.Text ?? "").Trim()
    });
    var vis = ready ? Visibility.Visible : Visibility.Collapsed;
    _sendAfterTask.Visibility = vis;
    var showWhen = ready && _sendAfterTask.IsChecked == true;
    _sendWhenLabel.Visibility = showWhen ? Visibility.Visible : Visibility.Collapsed;
    _sendWhen.Visibility = showWhen ? Visibility.Visible : Visibility.Collapsed;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```
dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter "ShouldSend|SmtpXfer"
dotnet build src/Yxy.Client/Yxy.Client.csproj
```

Expected: 测试 PASS，Client 0 错误。

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Client/Pages/SettingsPage.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: show task mail policy on the email settings tab"
```

---

## Self-review

1. Spec coverage: 四条件 → Task 1；Finish+三 catch → Task 2；页签显示/保存 → Task 3；JSON 存储 → Task 1+2；删除规则 → Finish 仅在真正发信失败时 `mailOk=false`。
2. 无 TBD。
3. 名称一致：`SendAfterTask`、`SendWhen`、`SmtpReady`、`ShouldSend`、`NormalizeWhen`。
