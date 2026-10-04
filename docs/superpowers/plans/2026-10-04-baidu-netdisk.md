# 百度网盘分片上传与定时删除 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 客户授权自己的百度网盘后，任务可把 zip 分片上传到应用目录，并按天删除本产品前缀下的过期包。

**Architecture:** 新增 `Yxy.Baidu` 实现 `ICloudStorage`。OAuth 只在客户端用固定 `127.0.0.1` 回调；token 加密进 `cloud_accounts`。引擎只读本机 token、走 superfile2。删除接入现有 `PurgeRunner`。不引用 `Yxy.Qiniu`。

**Tech Stack:** .NET Framework 4.8、NUnit、HttpWebRequest、HttpListener、SQLite、WPF。

## Global Constraints

- 产品统一百度应用；AppKey/Secret 只在客户端 `BaiduApp.json`，仓库只提交 `BaiduApp.example.json`。
- token 只存本机 `cloud_accounts.fields_protected`；网站不参与。
- 任务只选一个云（七牛或百度）。
- 网盘路径：`/apps/{AppFolderName}/{CloudObjectKeys 对象名}`，默认 AppFolderName 为 `用心云备份`。
- 分片 4MB、同时 2 路；上传上下限与七牛共用；上限 0 不限速。
- 同一次任务内分片重试 3 次；不跨进程续 `uploadid`。
- 删除与七牛并列：`purge_baidu`、`baidu_retain_days` 默认 7；只删产品前缀。
- 回调固定 `http://127.0.0.1:17890/baidu/`（须与开放平台登记一致）；端口占用则失败，不换随机端口。
- 日志不写 AppSecret、不写 token。
- 腾讯云 / 天翼云仍占位。

## File map

| 文件 | 职责 |
| --- | --- |
| `src/Yxy.Contracts/Cloud/CloudProviders.cs` | 增加 `Baidu = "baidu"` |
| `src/Yxy.Contracts/Cloud/CloudObjectKeys.cs` | `BaiduRemotePath` |
| `src/Yxy.Contracts/Cloud/CloudChoice.cs` | 从就绪列表解析任务选中的 provider |
| `src/Yxy.Contracts/Dto/CloudAccountDto.cs` | token 字段、`IsBaiduReady`；`IsReady` 仍表示七牛 |
| `src/Yxy.Core/Store/CloudAccountStore.cs` | 按 provider 持久化字段；`EnabledProviders` |
| `src/Yxy.Core/Data/SchemaSql.cs` + `SchemaMigrate.cs` + `SettingsStore` + `AppSettingsDto` | 百度删除列 |
| `src/Yxy.Baidu/*` | 配置、OAuth、HTTP、分片、`BaiduStorage` |
| `src/Yxy.Cloud/CloudStorageFactory.cs` | 创建百度适配器 |
| `src/Yxy.Core/Backup/PurgeRunner.cs` | 百度删除分支 |
| `src/Yxy.Client/Pages/CloudPage.cs` | 左侧列表 + 百度授权表单 |
| `src/Yxy.Client/Pages/SettingsPage.cs` | 删百度勾选 |
| 文件/MSSQL/MySQL 任务对话框 + `TasksPage.cs` | 按就绪列表选云 |
| `tests/Yxy.Core.Tests/*`、`tests/Yxy.Baidu.Tests/*` | 契约、存储、分片、假 HTTP |

---

### Task 1: 契约（provider、路径、就绪、任务选择）

**Files:**
- Modify: `src/Yxy.Contracts/Cloud/CloudProviders.cs`
- Modify: `src/Yxy.Contracts/Cloud/CloudObjectKeys.cs`
- Modify: `src/Yxy.Contracts/Dto/CloudAccountDto.cs`
- Create: `src/Yxy.Contracts/Cloud/CloudChoice.cs`
- Test: `tests/Yxy.Core.Tests/BaiduContractTests.cs`

**Interfaces:**
- Consumes: 现有 `CloudObjectKeys.ForZip`
- Produces: `CloudProviders.Baidu` 为 `"baidu"`；`CloudObjectKeys.BaiduRemotePath(string appFolder, string objectKey)`；`CloudAccountDto.IsBaiduReady`；`CloudAccountDto.IsReady` 仍为七牛；`CloudChoice.Resolve(IList<string> ready, bool doCloud, string selected)` 返回 `string` 或 `null`

- [ ] **Step 1: Write the failing test**

Create `tests/Yxy.Core.Tests/BaiduContractTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public sealed class BaiduContractTests
    {
        [Test]
        public void ProviderKey_IsBaidu()
        {
            Assert.That(CloudProviders.Baidu, Is.EqualTo("baidu"));
        }

        [Test]
        public void RemotePath_UsesAppsFolderAndObjectKey()
        {
            var key = CloudObjectKeys.ForZip("用心云备份", "file", "日报", @"D:\a\x.zip");
            var path = CloudObjectKeys.BaiduRemotePath("用心云备份", key);
            Assert.That(path, Is.EqualTo("/apps/用心云备份/" + key));
        }

        [Test]
        public void BaiduReady_NeedsEnabledAndRefreshToken()
        {
            var acc = new CloudAccountDto { Enabled = true };
            Assert.That(acc.IsBaiduReady, Is.False);
            acc.RefreshToken = "rt";
            Assert.That(acc.IsBaiduReady, Is.True);
            Assert.That(acc.IsReady, Is.False);
        }

        [Test]
        public void Choice_PicksSelectedWhenReady()
        {
            var ready = new List<string> { "qiniu", "baidu" };
            Assert.That(CloudChoice.Resolve(ready, true, "baidu"), Is.EqualTo("baidu"));
            Assert.That(CloudChoice.Resolve(ready, false, "baidu"), Is.Null);
            Assert.That(CloudChoice.Resolve(new List<string>(), true, "baidu"), Is.Null);
            Assert.That(CloudChoice.Resolve(ready, true, "tencent"), Is.EqualTo("qiniu"));
            Assert.That(CloudChoice.Resolve(new List<string> { "baidu" }, true, null), Is.EqualTo("baidu"));
        }
    }
}
```

Keep existing `PhaseETests.AccountReady_NeedsAllFields` unchanged.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter BaiduContractTests`  
Working directory: `d:\AICode\Cursor\yongxinyunbak`  
Expected: FAIL（`Baidu` / `BaiduRemotePath` / `IsBaiduReady` / `CloudChoice` 不存在）

- [ ] **Step 3: Write minimal implementation**

`CloudProviders.cs` 增加 `public const string Baidu = "baidu";`

`CloudObjectKeys.cs` 增加：

```csharp
public static string BaiduRemotePath(string appFolder, string objectKey)
{
    var folder = Safe(string.IsNullOrWhiteSpace(appFolder) ? "用心云备份" : appFolder);
    var key = (objectKey ?? "").Replace('\\', '/').TrimStart('/');
    return "/apps/" + folder + "/" + key;
}
```

`CloudAccountDto` 增加 `AccessToken`、`RefreshToken`、`TokenExpiryUtc`（`DateTime?`）、`DisplayName`，以及：

```csharp
public bool IsBaiduReady
{
    get { return Enabled && !string.IsNullOrWhiteSpace(RefreshToken); }
}
```

Create `CloudChoice.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Yxy.Contracts.Cloud
{
    public static class CloudChoice
    {
        public static string Resolve(IList<string> ready, bool doCloud, string selected)
        {
            if (!doCloud || ready == null || ready.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(selected))
            {
                for (var i = 0; i < ready.Count; i++)
                {
                    if (string.Equals(ready[i], selected, StringComparison.OrdinalIgnoreCase))
                    {
                        return ready[i];
                    }
                }
            }

            return ready[0];
        }

        public static string Label(string provider)
        {
            if (string.Equals(provider, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase))
            {
                return "百度网盘";
            }

            if (string.Equals(provider, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase))
            {
                return "七牛云";
            }

            return provider ?? "";
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter "BaiduContractTests|AccountReady_NeedsAllFields"`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Contracts/Cloud/CloudProviders.cs src/Yxy.Contracts/Cloud/CloudObjectKeys.cs src/Yxy.Contracts/Cloud/CloudChoice.cs src/Yxy.Contracts/Dto/CloudAccountDto.cs tests/Yxy.Core.Tests/BaiduContractTests.cs
git commit -m "feat: add baidu cloud contract types"
```

---

### Task 2: 本机账号与自动删除设置

**Files:**
- Modify: `src/Yxy.Core/Store/CloudAccountStore.cs`
- Modify: `src/Yxy.Core/Data/SchemaSql.cs`
- Modify: `src/Yxy.Core/Data/SchemaMigrate.cs`
- Modify: `src/Yxy.Contracts/Dto/AppSettingsDto.cs`
- Modify: `src/Yxy.Core/Store/SettingsStore.cs`
- Test: `tests/Yxy.Core.Tests/BaiduStoreTests.cs`

**Interfaces:**
- Consumes: Task 1 的 DTO 字段
- Produces: `CloudAccountStore.Save` 对 `baidu` 行写入 token；对 `qiniu` 行仍写 Bucket/AK/SK/Zone 且互不覆盖。`EnabledProviders(SqliteDb)` 返回就绪的 `qiniu`/`baidu`。`AppSettingsDto.PurgeBaidu`、`BaiduRetainDays`。`IsQiniuReady` 保持。新增 `IsBaiduReady(SqliteDb db)`。

- [ ] **Step 1: Write the failing test**

Create `tests/Yxy.Core.Tests/BaiduStoreTests.cs`。用临时 sqlite：

```csharp
using System;
using System.IO;
using NUnit.Framework;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;
using Yxy.Core.Store;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public sealed class BaiduStoreTests
    {
        [Test]
        public void Save_RoundTripsBaiduTokenWithoutWipingQiniu()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-baidu-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Qiniu,
                        Enabled = true,
                        Bucket = "b",
                        AccessKey = "ak",
                        SecretKey = "sk",
                        Zone = "z0"
                    });
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Baidu,
                        Enabled = true,
                        RefreshToken = "rt-1",
                        AccessToken = "at-1",
                        TokenExpiryUtc = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                        DisplayName = "张三"
                    });
                    var qiniu = CloudAccountStore.Get(db, CloudProviders.Qiniu);
                    var baidu = CloudAccountStore.Get(db, CloudProviders.Baidu);
                    Assert.That(qiniu.SecretKey, Is.EqualTo("sk"));
                    Assert.That(baidu.RefreshToken, Is.EqualTo("rt-1"));
                    Assert.That(baidu.DisplayName, Is.EqualTo("张三"));
                    Assert.That(CloudAccountStore.IsBaiduReady(db), Is.True);
                    var ready = CloudAccountStore.EnabledProviders(db);
                    Assert.That(ready, Does.Contain(CloudProviders.Qiniu));
                    Assert.That(ready, Does.Contain(CloudProviders.Baidu));
                    var settings = SettingsStore.Get(db);
                    Assert.That(settings.BaiduRetainDays, Is.EqualTo(7));
                    Assert.That(settings.PurgeBaidu, Is.False);
                    settings.PurgeBaidu = true;
                    settings.BaiduRetainDays = 3;
                    SettingsStore.Save(db, settings);
                    settings = SettingsStore.Get(db);
                    Assert.That(settings.PurgeBaidu, Is.True);
                    Assert.That(settings.BaiduRetainDays, Is.EqualTo(3));
                }
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter BaiduStoreTests`  
Expected: FAIL（Save 丢掉 token；无 `IsBaiduReady`/`PurgeBaidu`）

- [ ] **Step 3: Write minimal implementation**

`CloudAccountStore.Save` 的加密 JSON 必须包含该行全部字段：`Bucket, AccessKey, SecretKey, Zone, AccessToken, RefreshToken, TokenExpiryUtc, DisplayName`。`Get` 反序列化后这些字段都在。

增加：

```csharp
public static bool IsBaiduReady(SqliteDb db)
{
    return Get(db, CloudProviders.Baidu).IsBaiduReady;
}

public static IList<string> EnabledProviders(SqliteDb db)
{
    var list = new List<string>();
    if (IsQiniuReady(db)) list.Add(CloudProviders.Qiniu);
    if (IsBaiduReady(db)) list.Add(CloudProviders.Baidu);
    return list;
}
```

`SchemaSql` 的 `settings` 增加 `baidu_retain_days INTEGER NOT NULL DEFAULT 7` 与 `purge_baidu INTEGER NOT NULL DEFAULT 0`。`SchemaMigrate.Apply` 对已有库 `Add` 这两列。

`AppSettingsDto` 增加 `int BaiduRetainDays`、`bool PurgeBaidu`。`SettingsStore.Get/Save` 读写这两项（`GetInt` 缺列时已有 fallback）。Save 的 UPDATE 增加 `@brd`、`@pb`。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter "BaiduStoreTests|BaiduContractTests"`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Core/Store/CloudAccountStore.cs src/Yxy.Core/Data/SchemaSql.cs src/Yxy.Core/Data/SchemaMigrate.cs src/Yxy.Contracts/Dto/AppSettingsDto.cs src/Yxy.Core/Store/SettingsStore.cs tests/Yxy.Core.Tests/BaiduStoreTests.cs
git commit -m "feat: persist baidu tokens and purge settings"
```

---

### Task 3: `Yxy.Baidu` 配置、分片、OAuth URL

**Files:**
- Create: `src/Yxy.Baidu/Yxy.Baidu.csproj`
- Create: `src/Yxy.Baidu/BaiduApp.example.json`
- Create: `src/Yxy.Baidu/BaiduAppConfig.cs`
- Create: `src/Yxy.Baidu/BaiduSlices.cs`
- Create: `src/Yxy.Baidu/BaiduOAuth.cs`
- Create: `src/Yxy.Baidu/BytePacer.cs`
- Create: `tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj`
- Create: `tests/Yxy.Baidu.Tests/BaiduUnitTests.cs`
- Modify: `Yxy.sln`
- Modify: `.gitignore`（增加 `BaiduApp.json`）

**Interfaces:**
- Consumes: 无网络
- Produces: `BaiduAppConfig` 属性 `AppKey, AppSecret, RedirectUri, AppFolderName`；`Load(string path)`；缺文件或 Key 空抛 `InvalidOperationException("未配置百度应用")`。默认 RedirectUri `http://127.0.0.1:17890/baidu/`，默认 AppFolderName `用心云备份`。`BaiduSlices.BlockSize = 4 * 1024 * 1024`；`Md5HexList(string localPath)` 返回小写 hex 列表。`BaiduOAuth.AuthorizeUrl(BaiduAppConfig, string state)`；`TryParseCallback(string url, string expectedState, out string code, out string error)`。`BytePacer` 与七牛相同算法，复制到本项目。

- [ ] **Step 1: Write the failing test**

先加 `Yxy.Baidu` / `Yxy.Baidu.Tests` 工程（可先空类让测试编译失败在断言）。csproj 参照 `Yxy.Qiniu`（net48 + `Yxy.Contracts` + `System.Web.Extensions`）。测试工程参照 `Yxy.Core.Tests`，引用 `Yxy.Baidu`。

`Yxy.sln`：GUID `{B1000001-0000-4000-8000-00000000000B}` → `src\Yxy.Baidu\Yxy.Baidu.csproj`；`{C1000001-0000-4000-8000-000000000004}` → `tests\Yxy.Baidu.Tests\Yxy.Baidu.Tests.csproj`。补 Debug/Release 的 ActiveCfg/Build.0，NestedProjects 分别挂到 src/tests 文件夹。

`BaiduUnitTests.cs`：

```csharp
using System.IO;
using System.Text;
using NUnit.Framework;
using Yxy.Baidu;

namespace Yxy.Baidu.Tests
{
    [TestFixture]
    public sealed class BaiduUnitTests
    {
        [Test]
        public void Slices_FourMbBoundaries()
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-slice-" + System.Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, new byte[4 * 1024 * 1024 + 1]);
            try
            {
                var list = BaiduSlices.Md5HexList(path);
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list[0].Length, Is.EqualTo(32));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Config_MissingFile_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => BaiduAppConfig.Load(@"C:\no-such-baidu-app.json"));
            Assert.That(ex.Message, Is.EqualTo("未配置百度应用"));
        }

        [Test]
        public void OAuth_BuildsAuthorizeUrlAndParsesCallback()
        {
            var cfg = new BaiduAppConfig
            {
                AppKey = "ak",
                AppSecret = "sk",
                RedirectUri = "http://127.0.0.1:17890/baidu/",
                AppFolderName = "用心云备份"
            };
            var url = BaiduOAuth.AuthorizeUrl(cfg, "st-1");
            Assert.That(url, Does.Contain("client_id=ak"));
            Assert.That(url, Does.Contain("response_type=code"));
            Assert.That(url, Does.Contain("scope=basic,netdisk"));
            string code;
            string error;
            Assert.That(BaiduOAuth.TryParseCallback("http://127.0.0.1:17890/baidu/?code=abc&state=st-1", "st-1", out code, out error), Is.True);
            Assert.That(code, Is.EqualTo("abc"));
            Assert.That(BaiduOAuth.TryParseCallback("http://127.0.0.1:17890/baidu/?code=abc&state=nope", "st-1", out code, out error), Is.False);
        }
    }
}
```

`BaiduApp.example.json`：

```json
{
  "AppKey": "",
  "AppSecret": "",
  "RedirectUri": "http://127.0.0.1:17890/baidu/",
  "AppFolderName": "用心云备份"
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj`  
Expected: FAIL（类型不存在或方法未实现）

- [ ] **Step 3: Write minimal implementation**

`BaiduAppConfig.Load`：文件不存在或 `AppKey`/`AppSecret` 空白 → `未配置百度应用`。`RedirectUri` 空则用默认并保证以 `/` 结尾。`AppFolderName` 空则 `用心云备份`。

`BaiduSlices.Md5HexList`：按 4MB 读文件，每片 MD5 小写 hex。

`BaiduOAuth.AuthorizeUrl`：`https://openapi.baidu.com/oauth/2.0/authorize?response_type=code&client_id={Uri.EscapeDataString(AppKey)}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope=basic,netdisk&state={Uri.EscapeDataString(state)}`

`TryParseCallback`：解析 query 的 `code`/`state`/`error`；state 必须等于 `expectedState`；有 code 才成功。

`BytePacer`：从 `Yxy.Qiniu/QiniuHttp.cs` 的 `BytePacer` 原样复制到 `Yxy.Baidu/BytePacer.cs`（internal）。

`.gitignore` 增加一行 `BaiduApp.json`。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Baidu tests/Yxy.Baidu.Tests Yxy.sln .gitignore
git commit -m "feat: add baidu config slices and oauth urls"
```

---

### Task 4: 工厂与 `BaiduStorage` 骨架

**Files:**
- Create: `src/Yxy.Baidu/BaiduStorage.cs`
- Create: `src/Yxy.Baidu/IBaiduTransport.cs`
- Modify: `src/Yxy.Cloud/Yxy.Cloud.csproj`
- Modify: `src/Yxy.Cloud/CloudStorageFactory.cs`
- Modify: `tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj`（增加对 `Yxy.Cloud` 的 ProjectReference）
- Modify: `tests/Yxy.Baidu.Tests/BaiduUnitTests.cs`

**Interfaces:**
- Consumes: Task 1 `CloudProviders.Baidu`；Task 3 配置
- Produces: `BaiduStorage : ICloudStorage`，`ProviderKey == "baidu"`，`IsImplemented == true`。`Configure(CloudAccountDto)`。`Action<CloudAccountDto> PersistAccount`。内部构造 `BaiduStorage(CloudAccountDto, BaiduAppConfig, IBaiduTransport)` 给测试。`IBaiduTransport` 方法：`string Call(string method, string url, string accessToken, byte[] body, string contentType)`。`CloudStorageFactory.Create("baidu", account)` 返回已 Configure 的 `BaiduStorage`。未授权时 `TestConnection`/`Upload`/`DeleteExpired` 抛 `InvalidOperationException("请先在「云备份 → 百度网盘」授权。")`。

- [ ] **Step 1: Write the failing test**

在 `BaiduUnitTests` 增加：

```csharp
[Test]
public void Factory_CreatesImplementedBaidu()
{
    var storage = Yxy.Cloud.CloudStorageFactory.Create(Yxy.Contracts.Cloud.CloudProviders.Baidu);
    Assert.That(storage.IsImplemented, Is.True);
    Assert.That(storage.ProviderKey, Is.EqualTo("baidu"));
    var ex = Assert.Throws<InvalidOperationException>(() => storage.TestConnection());
    Assert.That(ex.Message, Does.Contain("授权"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj --filter Factory_CreatesImplementedBaidu`  
Expected: FAIL（未知云服务商或未实现）

- [ ] **Step 3: Write minimal implementation**

`CloudStorageFactory` 在 qiniu 分支后：

```csharp
if (string.Equals(providerKey, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase))
{
    var baidu = new BaiduStorage();
    if (account != null) baidu.Configure(account);
    return baidu;
}
```

`Yxy.Cloud.csproj` 增加 `<ProjectReference Include="..\Yxy.Baidu\Yxy.Baidu.csproj" />`。

`BaiduStorage` 实现接口：未 `IsBaiduReady` 时三个操作都抛上述中文异常。`Upload`/`DeleteExpired`/`TestConnection` 本任务可先只做就绪检查（完整 HTTP 在 Task 5）。`SetSpeed` 保存 `_maxKbps`。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Baidu src/Yxy.Cloud tests/Yxy.Baidu.Tests
git commit -m "feat: register baidu storage in the cloud factory"
```

---

### Task 5: 假传输上的分片上传、删除、续期

**Files:**
- Create: `src/Yxy.Baidu/BaiduHttp.cs`
- Modify: `src/Yxy.Baidu/BaiduStorage.cs`
- Modify: `src/Yxy.Baidu/BaiduOAuth.cs`（增加 `ApplyTokenJson` 解析）
- Create: `tests/Yxy.Baidu.Tests/FakeBaiduTransport.cs`
- Create: `tests/Yxy.Baidu.Tests/BaiduStorageTests.cs`
- Modify: `src/Yxy.Cloud/CloudRuntimeImpl.cs`（给 `BaiduStorage.PersistAccount` 接到 `CloudAccountStore.Save`）

**Interfaces:**
- Consumes: Task 3 分片；Task 2 Persist
- Produces: 真实 `BaiduHttp` 实现 `IBaiduTransport`（TLS 1.2、`Authorization: Bearer` 不用于百度——access_token 放 query）。上传：`precreate` → 非秒传则最多 2 路 `superfile2` → `create`。秒传：`precreate` JSON 含 `"return_type":2` 则跳过传片仍 `create` 或直接成功（若 JSON 已表示完成则不再 create）。删除：列出 `/apps/{folder}/{prefix}` 下文件，mtime 或文件名时间 `< olderThanUtc` 则删。access 过期（`TokenExpiryUtc` 空或 `UtcNow.AddMinutes(5)` 之后）先 POST `https://openapi.baidu.com/oauth/2.0/token?grant_type=refresh_token&refresh_token=&client_id=&client_secret=`，解析后更新账号并调用 `PersistAccount`。HTTP 401 则续期一次再试，仍失败则抛「请重新授权」。

百度 URL（写死在 `BaiduHttp`）：

- precreate: `POST https://pan.baidu.com/rest/2.0/xpan/file?method=precreate&access_token=`
- form: `path,size,isdir=0,autoinit=1,rtype=1,block_list`（block_list 为 JSON 数组字符串）
- 分片: `POST https://d.pcs.baidu.com/rest/2.0/pcs/superfile2?method=upload&type=tmpfile&access_token=&path=&uploadid=&partseq=`
- create: `POST https://pan.baidu.com/rest/2.0/xpan/file?method=create&access_token=`
- list: `GET https://pan.baidu.com/rest/2.0/xpan/file?method=list&dir=&start=&limit=1000&access_token=`
- delete: `POST https://pan.baidu.com/rest/2.0/xpan/file?method=filemanager&opera=delete&access_token=`，form `async=0&filelist=`
- uinfo: `GET https://pan.baidu.com/rest/2.0/xpan/nas?method=uinfo&access_token=`

错误 JSON `errno != 0` 抛 `InvalidOperationException("百度 HTTP errno=...")`。

时间：列表项 `server_mtime` 为 unix 秒；没有则从文件名解析 `yyyyMMdd-HHmmss`（与 `QiniuStorage.ParseStampFromKey` 相同逻辑，复制到 `BaiduStorage` 私有方法）。

- [ ] **Step 1: Write the failing test**

`FakeBaiduTransport` 记录 Calls 列表；`precreate` 返回 `{"errno":0,"uploadid":"u1","return_type":1}`；`superfile2` 返回 `{"errno":0}`；`create` 返回 `{"errno":0}`；`list` 返回两个文件，一个旧一个新；`filemanager` 返回 `{"errno":0}`；token 刷新返回 `{"access_token":"at-new","refresh_token":"rt-new","expires_in":2592000}`。

`BaiduStorageTests`：

1. 本地 5 字节文件 Upload：至少各一次 precreate 与 create；Calls 里 superfile2 出现；`path` 以 `/apps/用心云备份/` 开头。
2. `return_type=2` 时不出现 superfile2。
3. DeleteExpired：prefix `用心云备份/`，older 为昨天；只回调旧对象 key；新对象不删。
4. `TokenExpiryUtc = UtcNow.AddMinutes(-1)` 时 Upload 先出现 `grant_type=refresh_token`；PersistAccount 收到 `at-new`。
5. 无 refresh：Upload 抛授权异常。

用内部构造传入 Fake 与 `BaiduAppConfig { AppFolderName = "用心云备份", AppKey="k", AppSecret="s" }`。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj --filter BaiduStorageTests`  
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

实现 `BaiduHttp.EnsureTls12`（同七牛）、`Call` 用 `HttpWebRequest`，query 带 token。分片循环用 `Parallel.For(..., MaxDegreeOfParallelism = 2)`，单片 3 次重试，间隔 `400 * (attempt+1)` ms，`BytePacer.Take`。`CloudRuntimeImpl.CreateReady` 在返回前：

```csharp
var baidu = storage as BaiduStorage;
if (baidu != null)
{
    baidu.PersistAccount = delegate(CloudAccountDto updated)
    {
        CloudAccountStore.Save(db, updated);
    };
}
```

注意 `CreateReady` 需要把 `db` 闭包进去。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Baidu src/Yxy.Cloud/CloudRuntimeImpl.cs tests/Yxy.Baidu.Tests
git commit -m "feat: upload and purge baidu objects over superfile2"
```

---

### Task 6: PurgeRunner 接百度

**Files:**
- Modify: `src/Yxy.Core/Backup/PurgeRunner.cs`
- Test: `tests/Yxy.Core.Tests/BaiduStoreTests.cs`（增加 `PurgeDue_IncludesBaiduOnlyFlag` 只测入口条件需要抽方法，或测 `ExecuteDue` 在 `LastPurgeUtc` 今天时直接 false）

**Interfaces:**
- Consumes: `settings.PurgeBaidu`、`BaiduRetainDays`、`CloudProviders.Baidu`、`CloudRuntime.DeleteExpired`
- Produces: 无任何删除勾选时若只勾百度也会进入；百度分支日志文案「百度删除调用成功，共 N 个。」；单对象成功仍「删除调用成功：对象名」

- [ ] **Step 1: Write the failing test**

无法在无引擎宿主下打真网。把「是否有删除项」抽成：

```csharp
public static bool HasAnyPurge(AppSettingsDto settings)
```

测试：

```csharp
[Test]
public void HasAnyPurge_TrueWhenOnlyBaidu()
{
    var s = new AppSettingsDto { PurgeBaidu = true };
    Assert.That(PurgeRunner.HasAnyPurge(s), Is.True);
    Assert.That(PurgeRunner.HasAnyPurge(new AppSettingsDto()), Is.False);
}
```

`ExecuteDue` 开头改用 `HasAnyPurge(settings)`。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter HasAnyPurge`  
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

`HasAnyPurge`：`PurgeLocal || PurgeNetwork || PurgeFtp || PurgeQiniu || PurgeBaidu`。

`ExecuteDue` 在七牛分支后增加对称的百度分支（`remote && settings.PurgeBaidu`），`older = DateTime.UtcNow.AddDays(-settings.BaiduRetainDays)`，provider `CloudProviders.Baidu`，汇总字符串用「百度」不是「七牛」。`BaiduRetainDays <= 0` 时按 7。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter "HasAnyPurge|PurgeDue"`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Core/Backup/PurgeRunner.cs tests/Yxy.Core.Tests/BaiduStoreTests.cs
git commit -m "feat: purge expired baidu backups on schedule"
```

---

### Task 7: 云备份页、设置页、任务选云

**Files:**
- Modify: `src/Yxy.Client/Pages/CloudPage.cs`
- Modify: `src/Yxy.Client/Pages/SettingsPage.cs`
- Modify: `src/Yxy.Client/Pages/TasksPage.cs`
- Modify: `src/Yxy.Client/Dialogs/FileTaskDialog.xaml`
- Modify: `src/Yxy.Client/Dialogs/FileTaskDialog.xaml.cs`
- Modify: `src/Yxy.Client/Dialogs/MssqlTaskDialog.xaml`
- Modify: `src/Yxy.Client/Dialogs/MssqlTaskDialog.xaml.cs`
- Modify: `src/Yxy.Client/Dialogs/MysqlTaskDialog.cs`
- Modify: `src/Yxy.Client/Yxy.Client.csproj`（复制 example json）
- Modify: `src/Yxy.Engine/Yxy.Engine.csproj`（同上）
- Create: `src/Yxy.Baidu/BaiduOAuth.Listen.cs` 或在 `BaiduOAuth.cs` 增加 `CompleteInBrowser(BaiduAppConfig cfg, int timeoutMs, Action<string> openUrl)`：监听 `cfg.RedirectUri`，`HttpListener.Prefixes.Add(RedirectUri)`，失败提示「端口被占用」；`Process.Start` 打开授权 URL；成功返回填好 token 的 `CloudAccountDto` 片段（AccessToken/RefreshToken/TokenExpiryUtc/DisplayName）。超时 180000 ms。
- Test: `tests/Yxy.Baidu.Tests/BaiduUnitTests.cs` 增加 `TryParseCallback` 已有；再测端口占用无法在 CI 稳定测，跳过。增加 `CloudChoice` 已在 Task 1。本任务对对话框构造：把 `bool qiniuReady` 改为 `IList<string> readyProviders`，`DoCloud`/`CloudProvider` 走 `CloudChoice.Resolve`。

**Interfaces:**
- Consumes: Task 2 `EnabledProviders`；Task 5 存储；Task 3 OAuth
- Produces: 左侧「百度网盘（本期可用）」；选中后授权区；设置页删百度；任务下拉七牛/百度；`BaiduApp.example.json` 复制到 Client/Engine 输出目录

- [ ] **Step 1: Write the failing test**

扩展 Task 1 已有 `CloudChoice` 即可覆盖对话框解析。新增：

```csharp
[Test]
public void Choice_MysqlAndFileUseSameResolve()
{
    Assert.That(CloudChoice.Resolve(new List<string> { "baidu" }, true, "qiniu"), Is.EqualTo("baidu"));
}
```

放在 `BaiduContractTests`。对话框无法做 UI 自动化：实现后用编译覆盖。

- [ ] **Step 2: Run test to verify it fails** 若断言已在 Task 1 等价覆盖，本步确认 `dotnet test --filter Choice_MysqlAndFileUseSameResolve` 先红再绿。

- [ ] **Step 3: Write minimal implementation**

`CloudPage`：

- 列表插入百度项，顺序：七牛、百度、腾讯、天翼。
- `live` = qiniu 或 baidu。
- 选中百度：隐藏空间/AK/SK/区域，显示授权状态、`授权百度`/`重新授权`、启用、上下限、测试、保存。
- 授权：`BaiduAppConfig.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaiduApp.json"))`；`CompleteInBrowser`；合并 Enabled、token 字段后 `CloudAccountStore.Save`。
- 保存百度：只写 Enabled + 保留已有 token，不把七牛表单写进百度行。
- 测试：`CloudStorageFactory.Create(baidu, acc).TestConnection()`（uinfo）。
- 选中七牛：现有逻辑，不要读百度表单。
- 标题改为凭据只在本页；百度用授权，七牛用空间密钥。
- 上下限仍写入 `SettingsStore`（两个服务商共用）。

`SettingsPage`：`_purgeBaidu` 文案「删百度网盘本产品过期对象」，`_retainBaidu`，Load/Save 接 `PurgeBaidu`/`BaiduRetainDays`。

`FileTaskDialog`/`MssqlTaskDialog` 构造改为 `IList<string> readyProviders`。清空 `CmbCloud` 再按 ready 填 `CloudChoice.Label`。无就绪：勾选禁用，文案「云上传（请先在「云备份」保存并启用一个云）」。OK 时 `CloudProvider = CloudChoice.Resolve(ready, checked, selectedTag)`。

`TasksPage`：`new FileTaskDialog(..., CloudAccountStore.EnabledProviders(_boot.Db))`，MSSQL 同样。MySQL：给 `MysqlTaskDialog` 增加 `readyProviders`、云勾选+下拉，Accept 写入 `DoCloud`/`CloudProvider`。

Client/Engine csproj：

```xml
<None Include="..\Yxy.Baidu\BaiduApp.example.json" Link="BaiduApp.example.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

`CompleteInBrowser`：`HttpListener` 只绑定配置里的 RedirectUri；`Start` 抛异常则包成「端口被占用」。授权页同意后写一行简单 HTML「可以关闭此页」给浏览器。用 `JavaScriptSerializer` 解析 token JSON 的 `access_token, refresh_token, expires_in`。`expires_in` 秒加到 `DateTime.UtcNow` 得 `TokenExpiryUtc`。uinfo 的 `baidu_name` 或 `netdisk_name` 写入 `DisplayName`。

- [ ] **Step 4: Run test to verify it passes**

Run:

```
dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj
dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj
dotnet build src/Yxy.Client/Yxy.Client.csproj src/Yxy.Engine/Yxy.Engine.csproj
```

Expected: 测试 PASS，Client/Engine 编译成功。

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Client src/Yxy.Engine src/Yxy.Baidu tests/Yxy.Core.Tests/BaiduContractTests.cs
git commit -m "feat: authorize baidu in the cloud page and task picker"
```

---

### Task 8: 手工验收清单（不写新代码除非上一步漏了）

**Files:** 无强制代码。若 `TestConnection` 未调 uinfo，补在 `BaiduStorage.TestConnection`。

**Interfaces:** 无

- [ ] **Step 1: 对照规格勾验收（真机，有 `BaiduApp.json`）**

1. 云备份页能选百度；未配置 json 时授权提示「未配置百度应用」。
2. 授权成功后显示脱敏用户名；任务下拉出现百度。
3. 跑一个小文件任务，网盘出现 `/apps/用心云备份/...zip`。
4. 勾删百度、保留天数 7；日志格式符合规格。
5. 七牛原路径仍可用。日志无 token。

- [ ] **Step 2: 全量测试**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj`  
Expected: PASS

- [ ] **Step 3: Commit** 仅当有漏补代码。否则跳过。

```bash
git add -u
git commit -m "fix: finish baidu connection test against uinfo"
```

---

## Self-review

1. Spec coverage: 统一应用与本机 Secret → Task 3/7；localhost 固定端口 → Task 7；token 落库 → Task 2；单选云 → Task 1/7；路径 `/apps/用心云备份/` → Task 1/5；4MB 2 路限速 → Task 3/5；删除并列 → Task 2/6；工厂 ICloudStorage → Task 4；测试不连真网 → Task 1–6；网站不参与 → 无 API 任务。
2. 无 TBD。`return_type=2` 秒传路径已写死。
3. 名称一致：`IsBaiduReady`、`CloudProviders.Baidu`、`BaiduRemotePath`、`PersistAccount`、`HasAnyPurge`。
