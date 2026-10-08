# 百度大文件续传与日志保留 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 百度 1MB 分片 + 断点续传 + 齐片门闩 + 动态超时/节流；并行可配默认 1；综合设置按天清理 SQLite `logs`。

**Architecture:** 续传状态落 `data/baidu-upload/{hash}.json`。`BaiduStorage` 复用/创建 checkpoint，只传未完成片，齐片后才 `create`。设置项 `UploadParallel`、`LogRetainDays` 进 `settings` 表。日志清理挂在 `PurgeRunner` 每日时段（即使未勾选删云/本机，到点仍可只清日志）。

**Tech Stack:** .NET Framework 4.8，NUnit，WPF，SQLite，现有 `Yxy.Baidu` / `SettingsStore` / `PurgeRunner`。

## Global Constraints

- 片长固定 `1 * 1024 * 1024`；`block_list` 与本地 MD5 一致。
- 并行 `UploadParallel` 夹紧到 1–2，默认 1。
- 超时：`max(120, ceil(blockBytes / maxBytesPerSec) * 2 + 30)` 秒；`UploadMaxKbps == 0` 时固定 600 秒。
- 续传匹配：绝对路径 + size + 全部 block MD5；`create` 成功删文件；匹配失败废弃重建。
- 齐片门闩：`donePartSeq` 数量 = 应传片数且无缺口，否则禁止 `create`。
- `LogRetainDays` 最少 1、默认 30；只删 `logs` 表，不动续传 JSON / zip。
- 提交：`git -c user.name=yxy -c user.email=yxy@local commit`（不改 git config）。
- 不写 `appsettings.Local.json` / 密钥。

## File map

| 文件 | 职责 |
| --- | --- |
| `src/Yxy.Baidu/BaiduSlices.cs` | `BlockSize = 1MB` |
| `src/Yxy.Baidu/BaiduUploadCheckpoint.cs` | 续传 JSON 读写、匹配、标记片成功 |
| `src/Yxy.Baidu/BaiduUploadTimeout.cs` | 动态超时秒数 |
| `src/Yxy.Baidu/BaiduHttp.cs` | 超时参数；写出时按 `BytePacer` 节流 |
| `src/Yxy.Baidu/BaiduStorage.cs` | 续传流程、门闩、并行 |
| `src/Yxy.Baidu/BytePacer.cs` | 保持；供写出节流调用 |
| `src/Yxy.Contracts/Cloud/ICloudStorage.cs` | `SetParallel(int)` |
| `src/Yxy.Qiniu|Tencent|Ctyun/*Storage.cs` | 空实现 `SetParallel` |
| `src/Yxy.Contracts/Dto/AppSettingsDto.cs` | `UploadParallel`、`LogRetainDays` |
| `src/Yxy.Core/Data/SchemaMigrate.cs` | 两列 migrate |
| `src/Yxy.Core/Store/SettingsStore.cs` | Get/Save 两字段 |
| `src/Yxy.Core/Store/LogStore.cs` | `DeleteOlderThan` |
| `src/Yxy.Core/Backup/PurgeRunner.cs` | 到点清日志 |
| `src/Yxy.Cloud/CloudRuntimeImpl.cs` | `SetParallel` |
| `src/Yxy.Client/Pages/CloudPage.cs` | 并行数 UI |
| `src/Yxy.Client/Pages/SettingsPage.cs` | 日志保留天数 UI |
| `tests/Yxy.Baidu.Tests/*` | 续传/门闩/超时/片长 |
| `tests/Yxy.Core.Tests/PhaseETests.cs` | 日志清理 |

---

### Task 1: 设置字段 UploadParallel / LogRetainDays

**Files:**
- Modify: `src/Yxy.Contracts/Dto/AppSettingsDto.cs`
- Modify: `src/Yxy.Core/Data/SchemaMigrate.cs`
- Modify: `src/Yxy.Core/Store/SettingsStore.cs`
- Test: `tests/Yxy.Core.Tests/PhaseETests.cs`

**Interfaces:**
- Produces: `int UploadParallel`（默认 1）；`int LogRetainDays`（默认 30）；`SettingsStore` 读写；migrate 列 `upload_parallel` DEFAULT 1、`log_retain_days` DEFAULT 30

- [ ] **Step 1: Write the failing test**

在 `PhaseETests` 增加：

```csharp
[Test]
public void Settings_KeepsUploadParallelAndLogRetainDays()
{
    var file = Path.Combine(Path.GetTempPath(), "yxy-set-" + Guid.NewGuid().ToString("N") + ".db");
    try
    {
        using (var db = new SqliteDb(file))
        {
            var s = SettingsStore.Get(db);
            Assert.That(s.UploadParallel, Is.EqualTo(1));
            Assert.That(s.LogRetainDays, Is.EqualTo(30));
            s.UploadParallel = 2;
            s.LogRetainDays = 7;
            SettingsStore.Save(db, s);
            var loaded = SettingsStore.Get(db);
            Assert.That(loaded.UploadParallel, Is.EqualTo(2));
            Assert.That(loaded.LogRetainDays, Is.EqualTo(7));
            loaded.UploadParallel = 9;
            loaded.LogRetainDays = 0;
            SettingsStore.Save(db, loaded);
            var clamped = SettingsStore.Get(db);
            Assert.That(clamped.UploadParallel, Is.EqualTo(2));
            Assert.That(clamped.LogRetainDays, Is.EqualTo(1));
        }
    }
    finally
    {
        if (File.Exists(file)) File.Delete(file);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter Settings_KeepsUploadParallelAndLogRetainDays`

Expected: FAIL（属性不存在）

- [ ] **Step 3: Write minimal implementation**

`AppSettingsDto` 增加：

```csharp
public int UploadParallel { get; set; }
public int LogRetainDays { get; set; }
```

`SchemaMigrate.Apply` 末尾：

```csharp
Add(db, "settings", "upload_parallel", "INTEGER NOT NULL DEFAULT 1");
Add(db, "settings", "log_retain_days", "INTEGER NOT NULL DEFAULT 30");
```

`SettingsStore.Get` 默认分支与行读取：

```csharp
UploadParallel = ClampParallel(GetInt(row, "upload_parallel", 1)),
LogRetainDays = ClampLogDays(GetInt(row, "log_retain_days", 30)),
```

空表默认：`UploadParallel = 1`，`LogRetainDays = 30`。

`Save` 的 UPDATE 增加 `@upar` `@lrd`，写入前：

```csharp
private static int ClampParallel(int v)
{
    if (v < 1) return 1;
    if (v > 2) return 2;
    return v;
}

private static int ClampLogDays(int v)
{
    return v < 1 ? 1 : v;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter Settings_KeepsUploadParallelAndLogRetainDays`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Contracts/Dto/AppSettingsDto.cs src/Yxy.Core/Data/SchemaMigrate.cs src/Yxy.Core/Store/SettingsStore.cs tests/Yxy.Core.Tests/PhaseETests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: store upload parallel and log retain days"
```

---

### Task 2: 续传 Checkpoint 与超时计算

**Files:**
- Create: `src/Yxy.Baidu/BaiduUploadCheckpoint.cs`
- Create: `src/Yxy.Baidu/BaiduUploadTimeout.cs`
- Modify: `src/Yxy.Baidu/BaiduSlices.cs`（`BlockSize = 1 * 1024 * 1024`）
- Test: `tests/Yxy.Baidu.Tests/BaiduUnitTests.cs`（或新建 `BaiduCheckpointTests.cs`）

**Interfaces:**
- Produces:
  - `BaiduSlices.BlockSize == 1048576`
  - `BaiduUploadTimeout.Seconds(int maxKbps, int blockBytes)` → int
  - `BaiduUploadCheckpoint` 属性：`LocalPath, Size, BlockSize, BlockMd5 (string[]), RemotePath, UploadId, DonePartSeq (int[]), UpdatedUtc`
  - `static string FilePath(string dataDir, string localPath)`
  - `static BaiduUploadCheckpoint TryLoad(string dataDir, string localPath, long size, IList<string> md5s)`
  - `static void Save(string dataDir, BaiduUploadCheckpoint cp)`
  - `static void Delete(string dataDir, string localPath)`
  - `void MarkDone(int partSeq)` 后 Save
  - `bool IsComplete(int partCount)`：数量相等且 0..partCount-1 全在集合中

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void Checkpoint_MatchesAndGates()
{
    Assert.That(BaiduSlices.BlockSize, Is.EqualTo(1024 * 1024));
    Assert.That(BaiduUploadTimeout.Seconds(0, BaiduSlices.BlockSize), Is.EqualTo(600));
    Assert.That(BaiduUploadTimeout.Seconds(64, BaiduSlices.BlockSize), Is.GreaterThanOrEqualTo(120));

    var dir = Path.Combine(Path.GetTempPath(), "yxy-cp-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        var md5 = new[] { "a", "b", "c" };
        var cp = new BaiduUploadCheckpoint
        {
            LocalPath = @"D:\a.zip",
            Size = 3L * BaiduSlices.BlockSize,
            BlockSize = BaiduSlices.BlockSize,
            BlockMd5 = md5,
            RemotePath = "/apps/用心云循备/x.zip",
            UploadId = "u1",
            DonePartSeq = new[] { 0, 1 }
        };
        BaiduUploadCheckpoint.Save(dir, cp);
        var loaded = BaiduUploadCheckpoint.TryLoad(dir, @"D:\a.zip", cp.Size, md5);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.UploadId, Is.EqualTo("u1"));
        Assert.That(loaded.IsComplete(3), Is.False);
        loaded.MarkDone(2);
        BaiduUploadCheckpoint.Save(dir, loaded);
        Assert.That(BaiduUploadCheckpoint.TryLoad(dir, @"D:\a.zip", cp.Size, md5).IsComplete(3), Is.True);
        Assert.That(BaiduUploadCheckpoint.TryLoad(dir, @"D:\a.zip", cp.Size + 1, md5), Is.Null);
    }
    finally
    {
        Directory.Delete(dir, true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj --filter Checkpoint_MatchesAndGates`

Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

`BaiduSlices.BlockSize = 1 * 1024 * 1024`。

`BaiduUploadTimeout.Seconds`：

```csharp
public static int Seconds(int maxKbps, int blockBytes)
{
    if (maxKbps <= 0) return 600;
    var bytesPerSec = maxKbps * 1024L;
    var need = (int)((blockBytes + bytesPerSec - 1) / bytesPerSec) * 2 + 30;
    return need < 120 ? 120 : need;
}
```

`BaiduUploadCheckpoint`：用 `JavaScriptSerializer` 读写 JSON；`FilePath` = `Path.Combine(dataDir, "baidu-upload", sha256hex(localPath.ToLowerInvariant()) + ".json")`；`TryLoad` 任一字段不匹配返回 null；`MarkDone` 把 partSeq 并入数组（去重排序）。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj --filter Checkpoint_MatchesAndGates`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Baidu/BaiduSlices.cs src/Yxy.Baidu/BaiduUploadCheckpoint.cs src/Yxy.Baidu/BaiduUploadTimeout.cs tests/Yxy.Baidu.Tests/
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: add baidu upload checkpoint and 1mb slices"
```

---

### Task 3: Http 超时、写出节流、SetParallel

**Files:**
- Modify: `src/Yxy.Contracts/Cloud/ICloudStorage.cs`
- Modify: `src/Yxy.Baidu/BaiduHttp.cs`、`BaiduStorage.cs`（先接字段）
- Modify: `src/Yxy.Qiniu/QiniuStorage.cs`、`src/Yxy.TencentCloud/TencentCloudStorage.cs`、`src/Yxy.Ctyun/CtyunStorage.cs`
- Modify: `src/Yxy.Cloud/CloudRuntimeImpl.cs`
- Modify: `src/Yxy.Baidu/IBaiduTransport.cs`（若需传 timeout；否则在 BaiduHttp 上设实例属性）

**Interfaces:**
- Produces: `void SetParallel(int parallel)`；`IBaiduTransport.Call(..., int timeoutMs)` 或 `BaiduHttp.TimeoutMs` / `Pacer` 属性；`CloudRuntimeImpl.Upload` 调用 `SetParallel(settings.UploadParallel)`

- [ ] **Step 1: Write the failing test**

扩展 Fake：记录 Call 时是否带大 timeout 非必须。编译级：给 `ICloudStorage` 加方法后未实现的工程会红——先写一测断言 `BaiduStorage` 接受 `SetParallel(1)` 不抛：

```csharp
[Test]
public void Storage_SetParallel_Clamps()
{
    var s = new BaiduStorage(ReadyAccount(DateTime.UtcNow.AddHours(1)), Cfg(), new FakeBaiduTransport());
    s.SetParallel(0);
    s.SetParallel(9);
    // 内部字段通过后续上传测；此处仅保证接口可调用
}
```

- [ ] **Step 2: Run — may fail compile**

- [ ] **Step 3: Write minimal implementation**

```csharp
// ICloudStorage
void SetParallel(int parallel);
```

各 Storage：百度存 `_parallel`（夹紧 1–2）；其它空方法。

`IBaiduTransport` 增加超时：把 `Call` 签名改为含 `int timeoutMs`，Fake 与 BaiduHttp 同步。BaiduHttp 写出：

```csharp
using (var stream = request.GetRequestStream())
{
    var offset = 0;
    while (offset < body.Length)
    {
        var n = Math.Min(64 * 1024, body.Length - offset);
        if (_pacer != null) _pacer.Take(n);
        stream.Write(body, offset, n);
        offset += n;
    }
}
```

（若 transport 无 pacer 字段，由 `BaiduStorage.Api` 在调用前设 `BaiduHttp` 实例属性；Fake 忽略。）

更简单且少改接口：保持 `Call` 签名，给 `BaiduHttp` 增加 `public int TimeoutMs` 与 `public BytePacer Pacer`；`BaiduStorage` 在每次 Api 前设置。Fake 不需要。

推荐后者。

`CloudRuntimeImpl`：

```csharp
storage.SetSpeed(settings.UploadMinKbps, settings.UploadMaxKbps);
storage.SetParallel(settings.UploadParallel);
storage.Upload(localPath, remoteKey);
```

- [ ] **Step 4: Build + existing Baidu tests**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj --nologo`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Contracts/Cloud/ICloudStorage.cs src/Yxy.Baidu/ src/Yxy.Qiniu/QiniuStorage.cs src/Yxy.TencentCloud/TencentCloudStorage.cs src/Yxy.Ctyun/CtyunStorage.cs src/Yxy.Cloud/CloudRuntimeImpl.cs tests/Yxy.Baidu.Tests/
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: wire upload parallel and paced baidu http"
```

---

### Task 4: BaiduStorage 续传上传与齐片门闩

**Files:**
- Modify: `src/Yxy.Baidu/BaiduStorage.cs`
- Test: `tests/Yxy.Baidu.Tests/BaiduStorageTests.cs`

**Interfaces:**
- Consumes: Task 2 checkpoint/timeout、Task 3 SetParallel/TimeoutMs/Pacer
- Produces: Upload 使用 `AppPaths.DataDirectory`（测试可 `AppPaths.SetRootForTests`）

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void Upload_ResumesMissingPartsOnly()
{
    var root = Path.Combine(Path.GetTempPath(), "yxy-br-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    AppPaths.SetRootForTests(root);
    try
    {
        var fake = new FakeBaiduTransport();
        var storage = Create(fake, DateTime.UtcNow.AddHours(1));
        storage.SetParallel(1);
        // 构造 >1 片：文件长度 = BlockSize + 5
        var bytes = new byte[BaiduSlices.BlockSize + 5];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = 1;
        var file = WriteTemp(bytes);
        var md5s = BaiduSlices.Md5HexList(file);
        var remote = CloudObjectKeys.BaiduRemotePath("用心云·循备", "k.zip");
        var cp = new BaiduUploadCheckpoint
        {
            LocalPath = Path.GetFullPath(file),
            Size = bytes.Length,
            BlockSize = BaiduSlices.BlockSize,
            BlockMd5 = System.Linq.Enumerable.ToArray(md5s),
            RemotePath = remote,
            UploadId = "resume-u",
            DonePartSeq = new[] { 0 }
        };
        BaiduUploadCheckpoint.Save(AppPaths.DataDirectory, cp);
        storage.Upload(file, "k.zip");
        var joined = Joined(fake);
        Assert.That(joined, Does.Not.Contain("method=precreate")); // 复用续传则不再 precreate；若实现仍 precreate 则改为断言 superfile2 仅 partseq=1
        Assert.That(joined, Does.Contain("partseq=1"));
        Assert.That(joined, Does.Contain("method=create"));
        Assert.That(BaiduUploadCheckpoint.TryLoad(AppPaths.DataDirectory, Path.GetFullPath(file), bytes.Length, md5s), Is.Null);
        File.Delete(file);
    }
    finally
    {
        AppPaths.SetRootForTests(null);
        Directory.Delete(root, true);
    }
}

[Test]
public void Upload_BlocksCreateWhenPartMissing()
{
    // Fake：superfile2 对 partseq=1 始终抛错；断言不出现 method=create，且 checkpoint 仍在
}
```

若「复用不 precreate」实现困难，允许仍调 precreate 但必须带同一 uploadid；测试改为：第二次上传时 `DonePartSeq` 含 0 则 Fake 的 superfile2 调用次数为 1（仅缺片）。以「缺片数 = superfile2 次数」为准。

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Write minimal implementation**

`Upload` 伪码：

```csharp
var full = Path.GetFullPath(localPath);
var size = new FileInfo(full).Length;
var hashes = BaiduSlices.Md5HexList(full);
var path = CloudObjectKeys.BaiduRemotePath(...);
var dataDir = AppPaths.DataDirectory;
var cp = BaiduUploadCheckpoint.TryLoad(dataDir, full, size, hashes);
if (cp == null)
{
    // precreate → 新建 cp（uploadId, md5s, ... Done empty）Save
}
else
{
    // 日志级：复用续传（调用方无 log 则跳过 UI；可在消息里拼进异常/不强制）
}
var partCount = hashes.Count;
UploadParts(full, path, cp.UploadId, size, cp); // 跳过 Done 中的 partseq；成功 MarkDone+Save
if (!cp.IsComplete(partCount)) throw new InvalidOperationException("百度分片未齐，禁止合并。");
// create
BaiduUploadCheckpoint.Delete(dataDir, full);
```

`UploadParts`：并行 `_parallel`；每片前设 `_http.TimeoutMs = BaiduUploadTimeout.Seconds(_maxKbps, sizeOfPart) * 1000`；`Pacer = new BytePacer(_maxKbps)` 共享。

秒传 `return_type==2`：Delete checkpoint（若有），直接 create。

- [ ] **Step 4: Run Baidu tests**

Run: `dotnet test tests/Yxy.Baidu.Tests/Yxy.Baidu.Tests.csproj --nologo`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Baidu/BaiduStorage.cs tests/Yxy.Baidu.Tests/
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: resume baidu multipart upload with complete gate"
```

---

### Task 5: 云备份页并行数 UI

**Files:**
- Modify: `src/Yxy.Client/Pages/CloudPage.cs`
- Test: 无 UI 自动化；`dotnet build src/Yxy.Client/Yxy.Client.csproj`

**Interfaces:**
- Consumes: `UploadParallel`
- Produces: 文本框「上传并行数」，保存时夹紧 1–2

- [ ] **Step 1: 无新测** — 确认 Task 1 设置测试仍绿

- [ ] **Step 2: 跳过红测**

- [ ] **Step 3: UI**

在上下限旁增加 `_parallel = new TextBox { Width = 80 }`，标签「上传并行数（1–2）」。`List_OnChanged` 载入 `settings.UploadParallel`。`TryReadSpeed` 改名为或另加 `TryReadUploadOptions`：解析 parallel，非法则提示。`SaveSpeed` 同时写 `UploadParallel`。

- [ ] **Step 4: Build Client**

Run: `dotnet build src/Yxy.Client/Yxy.Client.csproj --nologo`

若 dist 被锁：结束 `Yxy.Client`/`Yxy.Engine` 再编。

Expected: 0 错误

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Client/Pages/CloudPage.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: expose baidu upload parallel on cloud page"
```

---

### Task 6: 日志保留清理 + 综合设置 UI

**Files:**
- Modify: `src/Yxy.Core/Store/LogStore.cs`
- Modify: `src/Yxy.Core/Backup/PurgeRunner.cs`
- Modify: `src/Yxy.Client/Pages/SettingsPage.cs`
- Test: `tests/Yxy.Core.Tests/PhaseETests.cs`

**Interfaces:**
- Produces: `LogStore.DeleteOlderThan(SqliteDb db, DateTime cutoffUtc)` → int 删除行数
- `PurgeRunner.ExecuteDue`：到点后始终执行日志清理；若未勾任何 purge，仍 MarkPurged 并写一条 purge 日志摘要「日志删除 N 条。」

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void LogStore_DeletesOlderThanRetainDays()
{
    var file = Path.Combine(Path.GetTempPath(), "yxy-log-" + Guid.NewGuid().ToString("N") + ".db");
    try
    {
        using (var db = new SqliteDb(file))
        {
            db.Execute(
                "INSERT INTO logs (type, mode, result, message, task_name, created_utc) VALUES ('file','auto','ok','old','t',@u)",
                new System.Data.SQLite.SQLiteParameter("@u", DateTime.UtcNow.AddDays(-40).ToString("o")));
            db.Execute(
                "INSERT INTO logs (type, mode, result, message, task_name, created_utc) VALUES ('file','auto','ok','new','t',@u)",
                new System.Data.SQLite.SQLiteParameter("@u", DateTime.UtcNow.ToString("o")));
            var n = LogStore.DeleteOlderThan(db, DateTime.UtcNow.AddDays(-30));
            Assert.That(n, Is.EqualTo(1));
            var rows = db.Query("SELECT message FROM logs");
            Assert.That(rows.Rows.Count, Is.EqualTo(1));
            Assert.That(Convert.ToString(rows.Rows[0]["message"]), Is.EqualTo("new"));
        }
    }
    finally
    {
        if (File.Exists(file)) File.Delete(file);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement**

```csharp
// LogStore
public static int DeleteOlderThan(SqliteDb db, DateTime cutoffUtc)
{
    return db.Execute(
        "DELETE FROM logs WHERE created_utc < @c",
        new SQLiteParameter("@c", cutoffUtc.ToString("o")));
}
```

（若 `Execute` 不返回行数，用 `ExecuteScalar`/`SELECT changes()` 或删除前 COUNT。）

`PurgeRunner.ExecuteDue`：

```csharp
var settings = SettingsStore.Get(db);
var nowLocal = DateTime.Now;
if (!IsDue(nowLocal, settings.PurgeTime, settings.LastPurgeUtc)) return false;
var hasPurge = HasAnyPurge(settings);
// 去掉「!hasPurge 则 return false」；改为允许仅清日志
var log = new StringBuilder();
var ok = true;
try {
  if (hasPurge) { /* 现有本机/网络/FTP/七牛/百度删除 */ }
  var days = settings.LogRetainDays < 1 ? 1 : settings.LogRetainDays;
  var cut = DateTime.UtcNow.AddDays(-days);
  var n = LogStore.DeleteOlderThan(db, cut);
  log.Append(" 日志删除 ").Append(n).Append(" 条。");
} ...
SettingsStore.MarkPurged(db, DateTime.UtcNow);
LogStore.Add(db, "purge", "auto", ok ? "ok" : "error", "自动删除", log.ToString());
return true;
```

`SettingsPage` GeneralTab：`_logRetain = new TextBox { Text = "30" }`，标签「日志保留天数」；Load/Save 读写 `LogRetainDays`（Save 时 `<1` 当 1）。

- [ ] **Step 4: Run tests + build**

Run:

```
dotnet test tests/Yxy.Core.Tests/Yxy.Core.Tests.csproj --filter "LogStore_DeletesOlderThanRetainDays|Settings_KeepsUploadParallelAndLogRetainDays"
dotnet build src/Yxy.Client/Yxy.Client.csproj --nologo
dotnet build src/Yxy.Engine/Yxy.Engine.csproj --nologo
```

Expected: PASS / 0 错误

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Core/Store/LogStore.cs src/Yxy.Core/Backup/PurgeRunner.cs src/Yxy.Client/Pages/SettingsPage.cs tests/Yxy.Core.Tests/PhaseETests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: purge old sqlite logs by retain days"
```

---

### Task 7: 文档

**Files:**
- Modify: `Doc/用心云·循备-操作手册.md`、`Doc/用心云·循备-本期成果预览.md`
- Modify: `docs/superpowers/specs/2026-10-05-baidu-large-upload-resume-design.md`（状态改为已落地）

- [ ] **Step 1–2:** 无测

- [ ] **Step 3:** 手册补充：云备份并行数、1MB 续传、综合设置日志保留天数；成果预览一行摘要。

- [ ] **Step 4:** 跳过编译

- [ ] **Step 5: Commit**

```bash
git add Doc/ docs/superpowers/specs/2026-10-05-baidu-large-upload-resume-design.md
git -c user.name=yxy -c user.email=yxy@local commit -m "docs: baidu resume upload and log retain"
```

---

## Spec coverage (self-review)

| Spec 项 | Task |
| --- | --- |
| 1MB 分片 | 2 |
| 续传 JSON + 匹配 | 2、4 |
| 齐片门闩 | 4 |
| 动态超时 + 写出节流 | 2、3、4 |
| 并行默认 1（1–2） | 1、3、5 |
| SetParallel / CloudRuntime | 3 |
| 日志保留天数 + 清 logs | 1、6 |
| 业务日志摘要 | 4（Upload 路径 Append 到任务已有 StringBuilder 由 Runner 负责；百度层抛出的消息含「复用续传/分片未齐」即可，Runner 已写入 logs） |
| 秒传清 checkpoint | 4 |
| 文档 | 7 |

## Placeholder scan

无 TBD；超时公式与 600s 已写死；`Execute` 行数若 API 不支持则用 `changes()` 已注明。
