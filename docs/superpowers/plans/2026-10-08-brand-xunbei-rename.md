# 品牌更名用心云·循备 / YxyXb Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将产品从「用心云循备」全面更名为对外「用心云·循备」、路径「用心云循备」、工程与服务 `YxyXb.*` / `YxyXbService`，并按备份提交 → 三波次落地。

**Architecture:** 先拆分并固化 `AppPaths` 品牌常量（显示名 / 路径名 / 服务名），清扫用户可见文案与 Doc；再改 Windows 服务内部名；最后批量重命名命名空间、项目、解决方案与仓库目录。旧用户路径与 `yxy.db` 不自动搬迁，仅做读兼容。

**Tech Stack:** .NET Framework 4.8（Client/Engine/Core）、ASP.NET Core 8（Api）、Vue 3 + Vitest（Web）、NUnit、SQLite、Windows Service（`sc.exe`）。

## Global Constraints

- Spec：`docs/superpowers/specs/2026-10-08-brand-xunbei-rename-design.md`
- 对外正式全称必须为 **用心云·循备**（中间点 U+00B7），不得用英文句点 `.` 或空格版作为正式全称。
- 路径 / 客户标识默认 / 百度文件夹默认必须为 **用心云循备**（无点无空格）。
- 「循备」不得单独作为窗口/页面主标题。
- 客户端主题下拉项「用心云」（Tag `yongxin`）保留为公司皮肤名，不改为产品全称。
- Logo `alt` 与页面 title 用「用心云·循备」。
- 已装用户「用心云循备」目录、旧百度数据、旧服务名：**不自动迁移**；升级靠文档与读兼容。
- 改名前必须先完成 Task 0 备份提交；未配置 git `user.name`/`user.email` 时先由维护者配置（代理不得擅自 `git config`）。
- 工作区若已有无关改动（如百度模块），备份提交前与维护者确认是否一并纳入；本计划默认「纳入当前全部已跟踪修改 + 已暂存 design spec」，未跟踪大文件另议。
- 历史 `docs/superpowers/plans/*` / 旧 spec 内「用心云循备」可随波次 1 链接更新一并替换；不必改写 git 历史。

## File Structure

| 文件 / 目录 | 职责 |
| --- | --- |
| `src/Yxy.Core/AppPaths.cs` → 波次 3 后 `YxyXb.Core` | 品牌与路径常量、`DatabaseFile` 兼容 |
| `src/Yxy.Core/Runtime/StartupHelper.cs` | 开机启动注册表值名 + 清理旧值 |
| `src/Yxy.Core/Service/WindowsServiceControl.cs` | 服务安装显示名/描述/内部名 |
| `src/Yxy.Core/Data/SchemaSql.cs` / `MediaTaskStore.cs` | 介质默认目录名 |
| `src/Yxy.Contracts/Cloud/CloudObjectKeys.cs` | 云对象前缀默认 |
| `src/Yxy.Baidu/BaiduAppConfig.cs` | 百度应用文件夹默认 |
| `src/Yxy.Media/Disks/VolumeMarkerStore.cs` | marker 新旧路径读写 |
| `src/Yxy.Media/MediaRunner.cs` / `MediaService.cs` | 介质目标目录默认 |
| `src/Yxy.Mssql/*` | 工作目录默认名 |
| `src/Yxy.Client/**` | 窗口/托盘/文案 |
| `src/Yxy.Engine/**` | 服务类与控制台文案 |
| `src/Yxy.Api/**` | 邮件主题等 |
| `src/Yxy.Web/src/brand.js`（新建） | Web 品牌字符串单源 |
| `src/Yxy.Web/index.html` + `views/*` | 官网/门户文案 |
| `Doc/用心云·循备-*.md` → `Doc/用心云·循备-*.md` | 用户/运维文档 |
| `deploy/linux/install.sh` | 部署路径与文案（波次 3） |
| `Yxy.sln` → `YxyXb.sln` | 解决方案 |
| `tests/**` | 常量与路径断言、兼容用例 |

---

### Task 0: Git 身份确认与备份提交

**Files:**
- 无代码；可能提交当前工作区（含已暂存 `docs/superpowers/specs/2026-10-08-brand-xunbei-rename-design.md`）

**Interfaces:**
- Consumes: 维护者确认「现在提交备份」及是否纳入现有脏工作区
- Produces: 备份 commit（message 含 `backup before brand rename`）

- [ ] **Step 1: 确认 git 身份可用**

Run（PowerShell）:

```powershell
git -C D:\AICode\Cursor\yongxinyunbak config user.name
git -C D:\AICode\Cursor\yongxinyunbak config user.email
```

Expected: 两者均非空。若为空，停止；请维护者自行配置后再继续（代理不得执行 `git config`）。

- [ ] **Step 2: 列出将纳入备份的变更并请维护者确认**

Run:

```powershell
git -C D:\AICode\Cursor\yongxinyunbak status
```

与维护者确认：是否把当前 modified 百度/客户端文件 + 未跟踪 `Doc/*服务器更新说明.md` 一并纳入备份提交。

- [ ] **Step 3: 备份提交**

仅在维护者明确同意后：

```powershell
cd D:\AICode\Cursor\yongxinyunbak
git add -A
git commit -m "chore: backup before brand rename to XinBei / YxyXb"
git log -1 --oneline
git status
```

Expected: 新 commit 存在；工作区干净（或仅剩维护者明确排除的文件）。

- [ ] **Step 4: 记录备份 SHA**

将 `git rev-parse HEAD` 写进本 plan 文件顶部备注（一行即可），便于回滚。

---

### Task 1: AppPaths 常量拆分（波次 1 核心）

**Files:**
- Modify: `src/Yxy.Core/AppPaths.cs`
- Create: `tests/Yxy.Core.Tests/AppPathsBrandTests.cs`
- Modify: `src/Yxy.Core/Store/SettingsStore.cs`（`SoftwareId` 默认改用 `ProductPathName`）
- Modify: `src/Yxy.Core/Data/SqliteDb.cs`（`@sid` 用 `ProductPathName`）

**Interfaces:**
- Consumes: 无
- Produces（`Yxy.Core.AppPaths`）:
  - `ProductDisplayName` = `"用心云·循备"`
  - `ProductPathName` = `"用心云循备"`
  - `ProductShortName` = `"循备"`
  - `ServiceName` 暂仍 `"YxyXbService"`（波次 2 再改）
  - `ServiceDisplayName` = `"用心云·循备 Service"`
  - `DefaultBakDirectory` = `@"D:\yxyxb\bak"`
  - `DatabaseFileNameNew` = `"yxyxb.db"`；`DatabaseFileNameLegacy` = `"yxy.db"`
  - `DatabaseFile` getter：data 目录存在 legacy 则用 legacy，否则用 new
  - 保留 `ProductName` 作为 `[Obsolete]` 别名指向 `ProductDisplayName` **或** 删除并用编译错误驱动替换——本任务选择：**删除 `ProductName`，全库改为 Display/Path**（Task 2 扫完）

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Yxy.Core.Tests/AppPathsBrandTests.cs
using NUnit.Framework;
using Yxy.Core;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public class AppPathsBrandTests
    {
        [Test]
        public void Display_And_Path_Names_Match_Brand_Spec()
        {
            Assert.That(AppPaths.ProductDisplayName, Is.EqualTo("用心云·循备"));
            Assert.That(AppPaths.ProductPathName, Is.EqualTo("用心云循备"));
            Assert.That(AppPaths.ProductShortName, Is.EqualTo("循备"));
            Assert.That(AppPaths.ServiceDisplayName, Is.EqualTo("用心云·循备 Service"));
            Assert.That(AppPaths.DefaultBakDirectory, Is.EqualTo(@"D:\yxyxb\bak"));
            Assert.That(AppPaths.ServiceName, Is.EqualTo("YxyXbService"));
        }

        [Test]
        public void DatabaseFile_Prefers_Legacy_When_Present()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-brand-" + Guid.NewGuid().ToString("N"));
            try
            {
                AppPaths.SetRootForTests(root);
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var legacy = Path.Combine(AppPaths.DataDirectory, "yxy.db");
                File.WriteAllText(legacy, "x");
                Assert.That(AppPaths.DatabaseFile, Is.EqualTo(legacy));
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void DatabaseFile_Uses_New_When_No_Legacy()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-brand-" + Guid.NewGuid().ToString("N"));
            try
            {
                AppPaths.SetRootForTests(root);
                Directory.CreateDirectory(AppPaths.DataDirectory);
                Assert.That(AppPaths.DatabaseFile, Is.EqualTo(Path.Combine(AppPaths.DataDirectory, "yxyxb.db")));
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Core.Tests\Yxy.Core.Tests.csproj --filter FullyQualifiedName~AppPathsBrandTests
```

Expected: FAIL（缺少新常量或旧值仍为「用心云循备」）。

- [ ] **Step 3: Write minimal implementation**

将 `AppPaths.cs` 改为（保留现有 `InstallDirectory` / `SetRootForTests` / `EnsureDirectories` 逻辑，仅替换常量与 `DatabaseFile`）：

```csharp
public const string ProductDisplayName = "用心云·循备";
public const string ProductPathName = "用心云循备";
public const string ProductShortName = "循备";
public const string ServiceName = "YxyXbService";
public const string ServiceDisplayName = "用心云·循备 Service";
public const string DefaultBakDirectory = @"D:\yxyxb\bak";
public const string DatabaseFileNameLegacy = "yxy.db";
public const string DatabaseFileNameNew = "yxyxb.db";

public static string DatabaseFile
{
    get
    {
        var legacy = Path.Combine(DataDirectory, DatabaseFileNameLegacy);
        if (File.Exists(legacy))
        {
            return legacy;
        }
        return Path.Combine(DataDirectory, DatabaseFileNameNew);
    }
}
```

`SettingsStore` / `SqliteDb` 中默认 `software_id` 改为 `AppPaths.ProductPathName`。

- [ ] **Step 4: Run tests to verify they pass**

```powershell
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Core.Tests\Yxy.Core.Tests.csproj --filter FullyQualifiedName~AppPathsBrandTests
```

Expected: PASS。

- [ ] **Step 5: Commit**

```powershell
git add src/Yxy.Core/AppPaths.cs src/Yxy.Core/Store/SettingsStore.cs src/Yxy.Core/Data/SqliteDb.cs tests/Yxy.Core.Tests/AppPathsBrandTests.cs
git commit -m "refactor: split AppPaths brand display and path constants"
```

---

### Task 2: Core / Contracts / Baidu / Media / Mssql 默认路径字符串

**Files:**
- Modify: `src/Yxy.Contracts/Cloud/CloudObjectKeys.cs`
- Modify: `src/Yxy.Baidu/BaiduAppConfig.cs`
- Modify: `src/Yxy.Baidu/BaiduApp.example.json`
- Modify: `src/Yxy.Core/Data/SchemaSql.cs`
- Modify: `src/Yxy.Core/Store/MediaTaskStore.cs`
- Modify: `src/Yxy.Media/MediaRunner.cs`
- Modify: `src/Yxy.Media/MediaService.cs`
- Modify: `src/Yxy.Media/Disks/VolumeMarkerStore.cs`
- Modify: `src/Yxy.Mssql/MssqlRunner.cs`
- Modify: `src/Yxy.Mssql/MssqlRestore.cs`
- Modify: `tests/Yxy.Core.Tests/PhaseETests.cs`
- Modify: `tests/Yxy.Core.Tests/FileBackupTests.cs`
- Modify: `tests/Yxy.Core.Tests/BaiduContractTests.cs`
- Modify: `tests/Yxy.Baidu.Tests/BaiduStorageTests.cs`
- Modify: `tests/Yxy.Baidu.Tests/BaiduUnitTests.cs`

**Interfaces:**
- Consumes: `AppPaths.ProductPathName`
- Produces: 所有新默认中文路径为「用心云循备」；`VolumeMarkerStore` 读旧写新

- [ ] **Step 1: Write / update failing assertions in Baidu + PhaseE tests**

将测试中的 `"用心云·循备"` 全部改为 `"用心云循备"`（路径语义）。`BaiduAppConfig.DefaultAppFolderName` 期望值改为 `"用心云循备"`。

- [ ] **Step 2: Run filtered tests — expect fail if production constants still old**

```powershell
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Baidu.Tests\Yxy.Baidu.Tests.csproj --filter "FullyQualifiedName~Baidu"
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Core.Tests\Yxy.Core.Tests.csproj --filter "FullyQualifiedName~PhaseE|FullyQualifiedName~FileBackup|FullyQualifiedName~BaiduContract"
```

- [ ] **Step 3: Implement production string updates**

规则：

1. 硬编码 `"用心云·循备"` 且表示**路径/文件夹/客户标识默认** → `AppPaths.ProductPathName` 或字面 `"用心云循备"`（库默认 SQL 字面量用 `'用心云循备'`）。
2. `CloudObjectKeys` / `BaiduAppConfig.DefaultAppFolderName` → `"用心云循备"`。
3. `VolumeMarkerStore`：

```csharp
public static string MarkerPath(string root)
{
    return Path.Combine(root.TrimEnd('\\'), ".yxyxb", "volume.marker.json");
}

public static string AltMarkerPath(string root)
{
    return Path.Combine(root.TrimEnd('\\'), AppPaths.ProductPathName, ".yxyxb", "volume.marker.json");
}

// 额外旧路径（只读）
public static string LegacyMarkerPath(string root)
{
    return Path.Combine(root.TrimEnd('\\'), ".yxybackup", "volume.marker.json");
}

public static string LegacyAltMarkerPath(string root)
{
    return Path.Combine(root.TrimEnd('\\'), "用心云·循备", ".yxybackup", "volume.marker.json");
}
```

`Read` / `Remove` 扫描：新路径 + legacy 路径。`Write` **只写** `MarkerPath` 与 `AltMarkerPath`（新名）。

4. `MediaService` 盘内数据目录默认改为 `ProductPathName`。
5. `MssqlRunner` CommonAppData 子目录改为 `ProductPathName`。

- [ ] **Step 4: Run tests — expect PASS**

同上 `dotnet test` 命令。

- [ ] **Step 5: Commit**

```powershell
git add src/Yxy.Contracts src/Yxy.Baidu src/Yxy.Core src/Yxy.Media src/Yxy.Mssql tests
git commit -m "refactor: use 用心云循备 for default path and cloud folder names"
```

---

### Task 3: 客户端 / 引擎显示文案与开机启动（波次 1）

**Files:**
- Modify: `src/Yxy.Client/MainWindow.xaml`
- Modify: `src/Yxy.Client/MainWindow.xaml.cs`
- Modify: `src/Yxy.Client/App.xaml.cs`
- Modify: `src/Yxy.Client/Dialogs/LockWindow.cs`
- Modify: `src/Yxy.Client/Dialogs/MediaTaskDialog.xaml`
- Modify: `src/Yxy.Client/Dialogs/MediaTaskDialog.xaml.cs`
- Modify: `src/Yxy.Client/Pages/CloudPage.cs`
- Modify: `src/Yxy.Core/Runtime/StartupHelper.cs`
- Modify: `src/Yxy.Core/Service/WindowsServiceControl.cs`（仅 description / display name 字符串；`ServiceName` 仍旧）
- Modify: `src/Yxy.Engine/Program.cs`
- Modify: `src/Yxy.Engine/YxyXbService.cs`（若含显示文案）

**Interfaces:**
- Consumes: `AppPaths.ProductDisplayName`、`ProductPathName`、`ServiceDisplayName`
- Produces: UI 全称「用心云·循备」；启动项新值名；启用启动时删除旧值名「用心云循备」

- [ ] **Step 1: Write failing test for StartupHelper cleanup**

```csharp
// tests/Yxy.Core.Tests/StartupHelperTests.cs
[Test]
public void Apply_Writes_New_Value_And_Removes_Legacy()
{
    // 使用 CurrentUser Run key；测试后必须删除测试写入的值
    const string legacy = "用心云·循备";
    const string modern = "用心云·循备";
    var exe = @"C:\Temp\Yxy.Client.exe";
    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
    {
        key.SetValue(legacy, "\"old\"");
    }
    StartupHelper.Apply(true, exe);
    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
    {
        Assert.That(key.GetValue(modern), Is.EqualTo("\"" + exe + "\""));
        Assert.That(key.GetValue(legacy), Is.Null);
    }
    StartupHelper.Apply(false, exe);
}
```

若在 CI/无注册表权限环境不稳定，改为抽 `IRegistryRunKey`——**本仓库保持直接写 Registry**；本地跑通即可，注明测试标记 `[Category("Registry")]`，过滤失败时人工验收。

- [ ] **Step 2: Implement StartupHelper**

```csharp
private const string ValueName = "用心云·循备";
private const string LegacyValueName = "用心云·循备";

public static void Apply(bool enabled, string exePath)
{
    using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey))
    {
        if (key == null) return;
        if (enabled)
        {
            key.SetValue(ValueName, "\"" + exePath + "\"");
            if (key.GetValue(LegacyValueName) != null)
                key.DeleteValue(LegacyValueName, false);
        }
        else
        {
            if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
            if (key.GetValue(LegacyValueName) != null) key.DeleteValue(LegacyValueName, false);
        }
    }
}
```

- [ ] **Step 3: Replace client/engine display strings**

- `MainWindow.xaml` `Title` 与侧栏标题 → `用心云·循备`（XAML 可字面量；code-behind 托盘用 `AppPaths.ProductDisplayName`）。
- `CloudPage` 提示中「我的应用数据 / …」→ `用心云循备`。
- `MediaTaskDialog` 默认文件夹 → `AppPaths.ProductPathName`。
- `WindowsServiceControl` description → `"用心云·循备本机任务引擎"`；create 时 binPath 仍指向现有 `Yxy.Engine.exe`；DisplayName 用 `AppPaths.ServiceDisplayName`。
- `Program.cs` 控制台行 → `"用心云·循备引擎（控制台模式）。按 Enter 停止。"`。

- [ ] **Step 4: Build client projects**

```powershell
dotnet build D:\AICode\Cursor\yongxinyunbak\src\Yxy.Client\Yxy.Client.csproj
dotnet build D:\AICode\Cursor\yongxinyunbak\src\Yxy.Engine\Yxy.Engine.csproj
```

Expected: 0 Error。

- [ ] **Step 5: Commit**

```powershell
git commit -am "feat: show 用心云·循备 in client UI and startup entry"
```

（若有未暂存无关文件，改为精确 `git add` 路径。）

---

### Task 4: API 邮件主题 + Web brand 常量（波次 1）

**Files:**
- Create: `src/Yxy.Web/src/brand.js`
- Modify: `src/Yxy.Web/index.html`
- Modify: `src/Yxy.Web/src/views/HomeView.vue`
- Modify: `src/Yxy.Web/src/views/RegisterView.vue`
- Modify: `src/Yxy.Web/src/views/LoginView.vue`
- Modify: `src/Yxy.Web/src/views/NotFoundView.vue`
- Modify: `src/Yxy.Web/src/views/DownloadView.vue`（若含产品名）
- Modify: `src/Yxy.Web/src/views/PortalLayout.vue` / `VendorLayout.vue` / `VendorLoginView.vue`（logo alt）
- Modify: `src/Yxy.Api/Services/AccountService.cs`
- Modify: `src/Yxy.Api/Controllers/OrderController.cs`
- Modify: `src/Yxy.Api/Controllers/VendorController.cs`
- Modify: `tests/Yxy.Api.Tests/AlipayPayTests.cs`
- Create: `src/Yxy.Web/src/brand.test.js`

**Interfaces:**
- Consumes: 无
- Produces: `brand.js` 导出：

```js
export const PRODUCT_DISPLAY_NAME = '用心云·循备'
export const PRODUCT_PATH_NAME = '用心云循备'
export const PRODUCT_SHORT_NAME = '循备'
```

- [ ] **Step 1: Write Vitest for brand.js**

```js
// src/Yxy.Web/src/brand.test.js
import { test, expect } from 'vitest'
import { PRODUCT_DISPLAY_NAME, PRODUCT_PATH_NAME } from './brand.js'

test('brand display uses middle dot', () => {
  expect(PRODUCT_DISPLAY_NAME).toBe('用心云·循备')
  expect(PRODUCT_DISPLAY_NAME).toContain('\u00B7')
  expect(PRODUCT_PATH_NAME).toBe('用心云循备')
})
```

- [ ] **Step 2: Run test — fail before file exists**

```powershell
cd D:\AICode\Cursor\yongxinyunbak\src\Yxy.Web
npm test -- brand.test.js
```

- [ ] **Step 3: Add brand.js and wire views**

`index.html`：`<title>`、`<h1>`、`meta description` 使用「用心云·循备」。  
Vue 文案：把「用心云循备」改为「用心云·循备」；可 `import { PRODUCT_DISPLAY_NAME } from '../brand.js'` 或同目录相对路径。  
Logo `alt`：`用心云·循备`。  
API：

```csharp
// AccountService
await _mail.SendAsync(email, "用心云·循备验证码", ...);
// OrderController subjects
var subject = order.OrderType == 2 ? "用心云·循备试用" : "用心云·循备标准版";
// VendorController
"用心云·循备发信测试"
```

`AlipayPayTests` 期望主题同步。

- [ ] **Step 4: Run Web + API tests**

```powershell
cd D:\AICode\Cursor\yongxinyunbak\src\Yxy.Web; npm test -- brand.test.js
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Api.Tests\Yxy.Api.Tests.csproj --filter FullyQualifiedName~Alipay
```

Expected: PASS。

- [ ] **Step 5: Commit**

```powershell
git add src/Yxy.Web src/Yxy.Api tests/Yxy.Api.Tests
git commit -m "feat: rebrand web and mail copy to 用心云·循备"
```

---

### Task 5: Doc 改名与链接更新（波次 1）

**Files:**
- Rename: `Doc/用心云·循备-*.md` → `Doc/用心云·循备-*.md`
- Modify: 所有指向旧文件名的链接（`Doc/*`、`docs/superpowers/**`、`deploy/linux/install.sh` 注释）

**Interfaces:**
- Produces: 仓库内无路径 `Doc/用心云·循备-` 文件名残留

Doc 清单（改名前）：

1. `用心云·循备-PRD.md`
2. `用心云·循备-开发计划.md`
3. `用心云·循备-本期成果预览.md`
4. `用心云·循备-操作手册.md`
5. `用心云·循备-后台系统开发需求文档.md`
6. `用心云·循备-配置手册.md`
7. `用心云·循备-Linux部署手册.md`
8. `用心云·循备-Windows部署手册.md`
9. `用心云·循备-2026-10-07-服务器更新说明.md`

- [ ] **Step 1: Rename files (git mv)**

```powershell
cd D:\AICode\Cursor\yongxinyunbak\Doc
Get-ChildItem -Filter "用心云·循备-*.md" | ForEach-Object {
  $new = $_.Name -replace '^用心云·循备-','用心云·循备-'
  git mv -- $_.Name $new
}
```

- [ ] **Step 2: Replace in-file product strings and links**

对 `Doc/` 与 `docs/superpowers/`：

- 链接文本/路径 `用心云·循备-` → `用心云·循备-`
- 叙述性产品全称「用心云循备」→「用心云·循备」
- 路径示例「用心云循备」目录 →「用心云循备」
- 默认 `D:\yxyxb\bak` → `D:\yxyxb\bak`
- 服务显示名 →「用心云·循备 Service」
- **波次 1 文档中服务内部名仍可写 `YxyXbService`**，并加一句「波次 2 将改为 `YxyXbService`」——或直接提前写成新名并注明「代码波次 2 对齐」。本计划选择：**文档在波次 1 已写 `YxyXbService`，并在操作手册增加「若仍为 YxyXbService 请先卸载再安装」**。

- [ ] **Step 3: Grep gate**

```powershell
rg -n "用心云·循备-" D:\AICode\Cursor\yongxinyunbak\Doc D:\AICode\Cursor\yongxinyunbak\docs
rg -n "用心云·循备" D:\AICode\Cursor\yongxinyunbak\src D:\AICode\Cursor\yongxinyunbak\tests
```

Expected: `Doc`/`docs` 无旧文件名前缀；`src`/`tests` 仅允许：`VolumeMarkerStore` legacy 常量、`StartupHelper` legacy 值名、测试注释标明的兼容夹具。

- [ ] **Step 4: Commit**

```powershell
git add Doc docs deploy
git commit -m "docs: rename manuals to 用心云·循备 and update brand copy"
```

---

### Task 6: 波次 1 验收闸门

**Files:** 无新文件

- [ ] **Step 1: Full test pass (desktop + api + web brand)**

```powershell
dotnet test D:\AICode\Cursor\yongxinyunbak\Yxy.sln
cd D:\AICode\Cursor\yongxinyunbak\src\Yxy.Web; npm test
```

Expected: 全绿（若个别 Registry 测试跳过，需注明）。

- [ ] **Step 2: Manual smoke checklist**

1. 启动客户端：标题/托盘为「用心云·循备」。
2. 综合设置客户标识留空保存后为「用心云循备」。
3. 官网 `index.html` title 为「用心云·循备」。

- [ ] **Step 3: Tag wave checkpoint（可选）**

```powershell
git tag wave1-brand-copy
```

---

### Task 7: 服务内部名 `YxyXbService`（波次 2）

**Files:**
- Modify: `src/Yxy.Core/AppPaths.cs`（`ServiceName`）
- Modify: `src/Yxy.Engine/YxyXbService.cs`（类可暂不改名，或改名为 `YxyXbService`；若改类名同步 `Program.cs`）
- Modify: `tests/Yxy.Core.Tests/AppPathsBrandTests.cs`
- Modify: `Doc/用心云·循备-操作手册.md`（卸载旧服务步骤）

**Interfaces:**
- Produces: `AppPaths.ServiceName = "YxyXbService"`

- [ ] **Step 1: Update failing test expectation**

```csharp
Assert.That(AppPaths.ServiceName, Is.EqualTo("YxyXbService"));
```

- [ ] **Step 2: Run test — expect FAIL**

```powershell
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Core.Tests\Yxy.Core.Tests.csproj --filter FullyQualifiedName~Display_And_Path
```

- [ ] **Step 3: Change ServiceName and service class wiring**

```csharp
public const string ServiceName = "YxyXbService";
```

`WindowsServiceControl` 已引用 `AppPaths.ServiceName`，无需改安装逻辑。  
操作手册增加：

```markdown
升级服务名：若「服务」里仍有 YxyXbService，在客户端首页先停止并卸载，再安装（新内部名 YxyXbService）。
也可用管理员命令：
sc stop YxyXbService
sc delete YxyXbService
```

- [ ] **Step 4: Build + test**

```powershell
dotnet build D:\AICode\Cursor\yongxinyunbak\src\Yxy.Engine\Yxy.Engine.csproj
dotnet test D:\AICode\Cursor\yongxinyunbak\tests\Yxy.Core.Tests\Yxy.Core.Tests.csproj --filter FullyQualifiedName~AppPathsBrandTests
```

- [ ] **Step 5: Commit**

```powershell
git commit -am "feat: rename Windows service to YxyXbService"
```

---

### Task 8: 命名空间与项目重命名为 `YxyXb.*`（波次 3）

**Files:**
- 几乎全部 `src/**`、`tests/**`、`Yxy.sln`
- Modify: 所有 `ProjectReference`、`using`、`namespace`、`RootNamespace`、`AssemblyName`

**Interfaces:**
- Produces: 磁盘目录与命名空间均为 `YxyXb.*`；解决方案 `YxyXb.sln`；exe `YxyXb.Client.exe` / `YxyXb.Engine.exe`

- [ ] **Step 1: Freeze tree — ensure wave 2 green**

```powershell
dotnet test D:\AICode\Cursor\yongxinyunbak\Yxy.sln
```

- [ ] **Step 2: Mechanical rename (PowerShell)**

在仓库根执行（先 dry-run 打印映射）：

```powershell
$root = "D:\AICode\Cursor\yongxinyunbak"
# 1) 重命名项目目录 src/Yxy.* -> src/YxyXb.* ；tests 同理
Get-ChildItem "$root\src","$root\tests" -Directory -Filter "Yxy.*" | ForEach-Object {
  $dest = Join-Path $_.Parent.FullName ($_.Name -replace '^Yxy','YxyXb')
  git mv -- $_.FullName $dest
}
# 2) 重命名 csproj / 关键服务文件
Get-ChildItem "$root\src","$root\tests" -Recurse -Include *.csproj,*.cs,*.xaml,*.json,*.js,*.html,*.md,*.sln,*.sh |
  Where-Object { $_.Name -like 'Yxy.*' -or $_.Name -eq 'YxyXbService.cs' } | ForEach-Object {
    $newName = $_.Name -replace '^Yxy\.','YxyXb.' -replace 'YxyXbService','YxyXbService'
    if ($newName -ne $_.Name) { git mv -- $_.FullName (Join-Path $_.DirectoryName $newName) }
  }
# 3) 内容替换（命名空间 / using / 项目引用 / exe 名）
# 使用 rg + 编辑器或脚本将 \bYxy\. -> YxyXb. ；YxyXbService 类名已在上步
# 注意：不要替换第三方或无关词；替换后检查 Baidu 等第三方 SDK 未被误伤
```

内容替换白名单字段：

| 查找 | 替换 |
| --- | --- |
| `namespace Yxy.` | `namespace YxyXb.` |
| `using Yxy.` | `using YxyXb.` |
| `Yxy.Core`（路径/项目名） | `YxyXb.Core` |
| `Yxy.Client.exe` | `YxyXb.Client.exe` |
| `Yxy.Engine.exe` | `YxyXb.Engine.exe` |
| `RootNamespace>Yxy.` | `RootNamespace>YxyXb.` |
| `Yxy.sln` | `YxyXb.sln` |

`AppPaths.InstallDirectory` 内检测的 exe 文件名同步为 `YxyXb.Engine.exe`。

- [ ] **Step 3: Rename solution**

```powershell
git mv Yxy.sln YxyXb.sln
# 编辑 YxyXb.sln 内项目路径
```

- [ ] **Step 4: Build and test**

```powershell
dotnet build D:\AICode\Cursor\yongxinyunbak\YxyXb.sln
dotnet test D:\AICode\Cursor\yongxinyunbak\YxyXb.sln
cd D:\AICode\Cursor\yongxinyunbak\src\YxyXb.Web; npm test
```

Expected: 0 Error，测试 PASS。

- [ ] **Step 5: Update deploy script paths**

`deploy/linux/install.sh`：

- 注释与文案：用心云·循备
- `WEB_SRC` → `$REPO_ROOT/src/YxyXb.Web/dist`
- 新装默认 `API_DEST=/opt/yxyxb/api`、`WEB_DEST=/opt/yxyxb/web`、`DATA_DIR=/var/lib/yxyxb`、`SERVICE_NAME=yxyxb-api`（**已有生产机不自动迁**；手册写明）

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "refactor: rename solution and namespaces to YxyXb"
```

---

### Task 9: 仓库目录 `yongxinyunbak` → `yxyxb`（波次 3 末）

**Files:**
- 父目录重命名；更新本机快捷方式/CI 检出路径（若有）

- [ ] **Step 1: 确认工作区干净且无进程锁定仓库文件**

- [ ] **Step 2: 重命名目录**

```powershell
cd D:\AICode\Cursor
Rename-Item -Path yongxinyunbak -NewName yxyxb
```

若存在 git remote，按托管方流程处理仓库改名；本地 `git remote -v` 核对。

- [ ] **Step 3: 在新路径验证**

```powershell
dotnet test D:\AICode\Cursor\yxyxb\YxyXb.sln
```

- [ ] **Step 4: Commit（若改名本身不产生 commit，则提交文档中路径更新）**

更新仍写死 `yongxinyunbak` 的内部文档路径说明。

```powershell
git commit -am "docs: point paths to yxyxb repository directory"
```

---

### Task 10: 最终验收（对照 spec §7）

- [ ] **Step 1: Grep gates**

```powershell
rg -n "用心云·循备" D:\AICode\Cursor\yxyxb\src D:\AICode\Cursor\yxyxb\tests D:\AICode\Cursor\yxyxb\Doc
rg -n "YxyXbService" D:\AICode\Cursor\yxyxb
rg -n "\bYxy\." D:\AICode\Cursor\yxyxb\src D:\AICode\Cursor\yxyxb\tests D:\AICode\Cursor\yxyxb\YxyXb.sln
rg -n "yongxinyunbackup" D:\AICode\Cursor\yxyxb\src D:\AICode\Cursor\yxyxb\Doc
```

Allowed leftovers only:

- Legacy 字符串：`用心云循备` in `StartupHelper` / `VolumeMarkerStore` read paths
- 测试夹具若保留旧名，文件头注释 `# compat fixture: legacy brand`

- [ ] **Step 2: Spec checklist**

1. 用户可见「用心云循备」清零（legacy 除外）  
2. 正式全称「用心云·循备」  
3. 新默认「用心云循备」+ `D:\yxyxb\bak`  
4. `YxyXbService` / 「用心云·循备 Service」  
5. `YxyXb.*` 编译测试通过  
6. Doc 文件名为 `用心云·循备-*`

- [ ] **Step 3: Final commit if needed**

```powershell
git commit -am "chore: final brand rename acceptance fixes"
```

---

## Self-Review (plan vs spec)

| Spec 要求 | Task |
| --- | --- |
| 用心云·循备 显示名 | 1, 3, 4, 5 |
| 用心云循备 路径名 | 1, 2, 3 |
| 不迁移旧目录 | 2（VolumeMarker 只读旧）、全程约束 |
| `yxy.db` 兼容 | 1 |
| 备份提交 | 0 |
| 波次 1/2/3 | 1–6 / 7 / 8–9 |
| `YxyXbService` | 7 |
| `YxyXb.*` + 仓库 `yxyxb` | 8, 9 |
| Doc 改名 | 5 |
| 非目标：域名/公司主体/白标 | 未列入任务 |
| 主题「用心云」保留 | Global Constraints |

无 TBD 占位；常量名在 Task 1 定义，后续任务一致使用 `ProductDisplayName` / `ProductPathName` / `ServiceName`。
