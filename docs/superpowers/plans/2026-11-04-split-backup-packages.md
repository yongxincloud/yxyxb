# 分卷备份包与运行统计 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把超限 ZIP 切成可校验、可补传、可自动组装的独立 ZIP 分卷，并让运行统计、邮件、结果判定和清理都以整个备份单元为单位。

**Architecture:** `YxyXb.Core` 新增清单、分卷生成器和恢复组装器。任务先扫描并记录数量，再用现有 `ZipPacker` 生成完整 ZIP；超限时生成 store 模式分卷和内嵌清单，再由外部清单记录最终分卷哈希。上传器按文件记录逐通道状态，恢复页只接受一个包含全部手工下载文件的目录。

**Tech Stack:** .NET Framework 4.8，C# 7.3，NUnit 3，SQLite，SharpZipLib 1.4.2，WPF，现有云存储适配器。

**Spec:** `docs/superpowers/specs/2026-11-04-split-backup-packages-design.md`

## Global Constraints

- 默认分卷上限：`4294967296` 字节（4 GiB）。
- 合法设置范围：`67108864` 到 `1099511627776` 字节（64 MiB 到 1 TiB）。
- 任务值 `0` 表示继承全局值；大于 `0` 表示覆盖。
- 分卷包装预留 `1048576` 字节；payload 上限为设置值减该预留。
- 外层清单：`<package>.manifest.json`；分卷内嵌条目：`manifest.json`。
- 分卷 payload 条目：`payload/<package>.partNN.bin`，使用 SharpZipLib store 模式。
- 分卷和外部清单按 `BackupId` 作为一个备份单元；上传、恢复、立即删除和过期删除都不拆散整个单元。
- 发现文件数不等于打包文件数时，即使上传全部成功，任务数据状态仍为失败。
- 邮件失败记录为 `warn`，不改变数据成功状态，不阻止进入下一轮计划。
- 恢复不提供云下载；只选择手工下载目录，输出到恢复父目录下新的 `restore-yyyyMMdd-HHmmss` 子目录。
- 不覆盖已有文件，不允许 ZIP 路径穿越。
- 单 ZIP 和历史 `pending_json` 保持兼容。
- 当前工作树已有其它删除和修改；本计划只创建或修改明确文件，不恢复无关删除。
- 提交命令：`git -c user.name=yxy -c user.email=yxy@local commit`。

## File Map

| 文件 | 职责 |
| --- | --- |
| `src/YxyXb.Core/Backup/PackageManifest.cs` | 清单、分卷描述、运行统计、大小解析 |
| `src/YxyXb.Core/Backup/PackageSplitter.cs` | 拆卷、校验、目录扫描、组装 |
| `src/YxyXb.Core/Backup/ZipPacker.cs` | 安全全量解压 |
| `src/YxyXb.Core/Backup/SourceWalker.cs` | 发现/打包/跳过数量和源大小 |
| `src/YxyXb.Core/Backup/PendingBackup.cs` | 运行统计、分卷单元、逐文件状态 |
| `src/YxyXb.Core/Backup/PendingFiles.cs` | 按文件补传分卷和清单 |
| `src/YxyXb.Core/Backup/DistChannel.cs` | 单文件上传、远端大小/校验验证、结果状态 |
| `src/YxyXb.Core/Backup/FileBackupRunner.cs` | 文件任务统计、分卷、严格成功判定 |
| `src/YxyXb.Core/Transfer/MailUtil.cs` | 三个统计变量和 `warn` 语义 |
| `src/YxyXb.Core/Backup/PurgeRunner.cs` | 按备份单元清理 |
| `src/YxyXb.Contracts/Dto/*.cs` | 全局及三个任务的 `PartSizeBytes` |
| `src/YxyXb.Core/Data/Schema*.cs` | 存储列和迁移 |
| `src/YxyXb.Core/Store/*TaskStore.cs`、`SettingsStore.cs` | 配置和任务覆盖值 |
| `src/YxyXb.Client/Pages/SettingsPage.cs` | 全局上限输入 |
| `src/YxyXb.Client/Dialogs/*TaskDialog*` | 三个任务覆盖值 |
| `src/YxyXb.Client/Pages/RestorePage.cs` | 文件目录恢复和数据库目录入口 |
| `src/YxyXb.Mssql/MssqlRunner.cs`、`MssqlRestore.cs` | MSSQL 拆卷和恢复 |
| `src/YxyXb.MySql/MysqlRunner.cs`、`MysqlRestore.cs` | MySQL 拆卷和恢复 |
| `src/YxyXb.Contracts/Cloud/*.cs` 及五个适配器 | 对象键、远端校验、单元清理 |
| `tests/YxyXb.Core.Tests/Package*.cs` | 清单、拆卷、组装、清理测试 |
| `tests/YxyXb.Core.Tests/SourceWalkerTests.cs` | 严格统计测试 |
| `tests/YxyXb.Core.Tests/MailUtilTests.cs` | 邮件变量测试 |

---

### Task 1: 清单、大小上限和纯数据模型

**Files:**
- Create: `src/YxyXb.Core/Backup/PackageManifest.cs`
- Create: `tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj`
- Create: `tests/YxyXb.Core.Tests/PackageManifestTests.cs`

**Interfaces:**
- Consumes: `JsonUtil`
- Produces: `BackupRunStats`、`PackagePartManifest`、`BackupPackageManifest`、`PackageManifestCodec`、`PackageLimits`
- `PackageLimits.Resolve(long globalBytes, long taskBytes)` 选择任务值、全局值或默认值
- `PackageLimits.NormalizeGlobal(long bytes)` 将全局值限制在 `64 MiB` 到 `1 TiB`

- [ ] **Step 1: Write the failing test**

创建只包含新测试的 `net48` NUnit 工程：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>YxyXb.Core.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\YxyXb.Core\YxyXb.Core.csproj" />
    <ProjectReference Include="..\..\src\YxyXb.Contracts\YxyXb.Contracts.csproj" />
  </ItemGroup>
</Project>
```

```csharp
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PackageManifestTests
    {
        [Test]
        public void Resolve_UsesTaskThenGlobalThenDefault()
        {
            Assert.That(PackageLimits.Resolve(8L << 30, 2L << 30), Is.EqualTo(2L << 30));
            Assert.That(PackageLimits.Resolve(8L << 30, 0), Is.EqualTo(8L << 30));
            Assert.That(PackageLimits.Resolve(0, 0), Is.EqualTo(PackageLimits.DefaultBytes));
        }

        [Test]
        public void Manifest_JsonRoundTripKeepsParts()
        {
            var source = new BackupPackageManifest
            {
                FormatVersion = 1,
                BackupId = "20261104-153000-7f3a2c11",
                PackageName = "a.zip",
                TaskName = "财务",
                CreatedUtc = "2026-11-04T07:30:00.0000000Z",
                PartSizeLimitBytes = PackageLimits.DefaultBytes,
                ArchiveBytes = 9000000,
                ArchiveSha256 = "abc",
                SourceFileCount = 10,
                PackedFileCount = 9,
                SkippedFileCount = 1,
                SourceBytes = 8000,
                Parts = new[]
                {
                    new PackagePartManifest
                    {
                        Index = 1,
                        FileName = "a.part01.zip",
                        PayloadEntryName = "payload/a.part01.bin",
                        PayloadBytes = 9000000,
                        PayloadSha256 = "def",
                        WrappedBytes = 9001000,
                        WrappedSha256 = "ghi"
                    }
                }
            };
            var copy = PackageManifestCodec.Parse(PackageManifestCodec.ToJson(source));
            Assert.That(copy.BackupId, Is.EqualTo(source.BackupId));
            Assert.That(copy.Parts[0].PayloadSha256, Is.EqualTo("def"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PackageManifestTests`

Expected: FAIL，类型不存在。

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class BackupRunStats
{
    public int DiscoveredFiles { get; set; }
    public int PackedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public long SourceBytes { get; set; }
}

public sealed class PackagePartManifest
{
    public int Index { get; set; }
    public string FileName { get; set; }
    public string PayloadEntryName { get; set; }
    public long PayloadBytes { get; set; }
    public string PayloadSha256 { get; set; }
    public long WrappedBytes { get; set; }
    public string WrappedSha256 { get; set; }
}

public sealed class BackupPackageManifest
{
    public int FormatVersion { get; set; }
    public string BackupId { get; set; }
    public string PackageName { get; set; }
    public string TaskName { get; set; }
    public string CreatedUtc { get; set; }
    public long PartSizeLimitBytes { get; set; }
    public long ArchiveBytes { get; set; }
    public string ArchiveSha256 { get; set; }
    public int SourceFileCount { get; set; }
    public int PackedFileCount { get; set; }
    public int SkippedFileCount { get; set; }
    public long SourceBytes { get; set; }
    public PackagePartManifest[] Parts { get; set; }
}

public static class PackageManifestCodec
{
    public static string ToJson(BackupPackageManifest value)
    {
        return JsonUtil.ToJson(value);
    }

    public static BackupPackageManifest Parse(string json)
    {
        var value = JsonUtil.FromJson<BackupPackageManifest>(json);
        if (value == null || value.FormatVersion != 1 || value.Parts == null || value.Parts.Length == 0)
            throw new InvalidOperationException("分卷清单格式不支持。");
        return value;
    }
}

public static class PackageLimits
{
    public const long DefaultBytes = 4L * 1024 * 1024 * 1024;
    public const long MinimumBytes = 64L * 1024 * 1024;
    public const long MaximumBytes = 1024L * 1024 * 1024 * 1024;
    public const long ManifestReserveBytes = 1024L * 1024;

    public static long Resolve(long globalBytes, long taskBytes)
    {
        if (taskBytes > 0) return NormalizeTask(taskBytes);
        return globalBytes > 0 ? NormalizeGlobal(globalBytes) : DefaultBytes;
    }

    public static long NormalizeGlobal(long bytes)
    {
        return bytes < MinimumBytes ? MinimumBytes : bytes > MaximumBytes ? MaximumBytes : bytes;
    }

    public static long NormalizeTask(long bytes)
    {
        if (bytes == 0) return 0;
        if (bytes < MinimumBytes || bytes > MaximumBytes)
            throw new ArgumentOutOfRangeException("bytes", "分卷大小必须在 64 MiB 到 1 TiB 之间。");
        return bytes;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PackageManifestTests`

Expected: PASS。

- [ ] **Step 5: Commit**

```powershell
git add src/YxyXb.Core/Backup/PackageManifest.cs tests/YxyXb.Core.Tests
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: add split package manifest model"
```

---

### Task 2: 分卷生成、校验、组装和安全解压

**Files:**
- Create: `src/YxyXb.Core/Backup/PackageSplitter.cs`
- Modify: `src/YxyXb.Core/Backup/ZipPacker.cs`
- Create: `tests/YxyXb.Core.Tests/PackageSplitterTests.cs`

**Interfaces:**
- Produces: `SplitPackageResult`
- `PackageSplitter.Split(fullZipPath, taskName, limitBytes, stats, backupIdFactory)` 超限返回结果，否则返回 `null`
- `PackageSplitter.Assemble(inputDirectory, workDirectory)` 返回组装后的 ZIP 路径
- `ZipPacker.ExtractAllSafe(zipPath, password, destinationDirectory)` 安全解压所有文件

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PackageSplitterTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-split-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void SplitAndAssemble_RoundTripsBytes()
        {
            var full = Path.Combine(_root, "a.zip");
            var bytes = new byte[3 * 1024 * 1024];
            new Random(7).NextBytes(bytes);
            File.WriteAllBytes(full, bytes);
            var result = PackageSplitter.Split(full, "task", 2L * 1024 * 1024,
                new BackupRunStats { DiscoveredFiles = 1, PackedFiles = 1 },
                delegate { return "b1"; });
            Assert.That(result.PartPaths.Length, Is.GreaterThanOrEqualTo(3));
            var work = Path.Combine(_root, "work");
            Directory.CreateDirectory(work);
            Assert.That(File.ReadAllBytes(PackageSplitter.Assemble(result.PackageDirectory, work)),
                Is.EqualTo(bytes));
        }

        [Test]
        public void Assemble_MissingPart_Fails()
        {
            var full = Path.Combine(_root, "a.zip");
            File.WriteAllBytes(full, new byte[3 * 1024 * 1024]);
            var result = PackageSplitter.Split(full, "task", 2L * 1024 * 1024,
                new BackupRunStats(), delegate { return "b2"; });
            File.Delete(result.PartPaths[1]);
            var ex = Assert.Throws<InvalidOperationException>(
                delegate { PackageSplitter.Assemble(result.PackageDirectory, _root); });
            Assert.That(ex.Message, Does.Contain("缺少分卷"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PackageSplitterTests`

Expected: FAIL，`PackageSplitter` 不存在。

- [ ] **Step 3: Write minimal implementation**

固定目录和分卷结构：

```csharp
public sealed class SplitPackageResult
{
    public string BackupId { get; set; }
    public string PackageDirectory { get; set; }
    public string ManifestPath { get; set; }
    public string[] PartPaths { get; set; }
}

public static SplitPackageResult Split(
    string fullZipPath, string taskName, long limitBytes,
    BackupRunStats stats, Func<string> backupIdFactory)
{
    var size = new FileInfo(fullZipPath).Length;
    if (size <= limitBytes) return null;
    var backupId = backupIdFactory();
    var packageName = Path.GetFileName(fullZipPath);
    var baseName = Path.GetFileNameWithoutExtension(packageName);
    var dir = Path.Combine(Path.GetDirectoryName(fullZipPath), baseName + ".split-" + backupId);
    Directory.CreateDirectory(dir);
    var payloadLimit = limitBytes - PackageLimits.ManifestReserveBytes;
    var ranges = PackageManifestCodec.PlanRanges(size, payloadLimit);
    var manifest = PackageManifestCodec.BuildManifest(
        backupId, packageName, taskName, limitBytes, size,
        PackageManifestCodec.FileSha256(fullZipPath), stats, ranges);
    manifest.Parts = PackageManifestCodec.WritePartArchives(fullZipPath, dir, manifest);
    var manifestPath = Path.Combine(dir, baseName + ".manifest.json");
    File.WriteAllText(manifestPath, PackageManifestCodec.ToJson(manifest));
    PackageManifestCodec.VerifyUnit(dir, manifest);
    return new SplitPackageResult
    {
        BackupId = backupId,
        PackageDirectory = dir,
        ManifestPath = manifestPath,
        PartPaths = Array.ConvertAll(manifest.Parts,
            part => Path.Combine(dir, part.FileName))
    };
}
```

`WritePartArchives` 对每个区间使用 `ZipOutputStream.SetLevel(0)`，先写 payload，再写内嵌 `manifest.json`。第一遍只计算 payload SHA，第二遍写分卷，最后计算 `WrappedBytes/WrappedSha256`。内嵌清单把自身分卷的 `WrappedBytes` 设为 `0`、`WrappedSha256` 设为 `""`，其余内容相同。

`Assemble` 先调用 `ScanUnit`，拒绝多组 `BackupId`、缺卷、重复序号、清单版本不支持；按 `Index` 拼接 payload 到临时 ZIP，边写边计算 SHA，最后比较 `ArchiveSha256`。

`ExtractAllSafe` 对每个条目执行：

```csharp
var dest = Path.GetFullPath(Path.Combine(destinationDirectory, entry.Name));
var root = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar)
    + Path.DirectorySeparatorChar;
if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("压缩包路径越界");
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PackageSplitterTests`

Expected: PASS。

- [ ] **Step 5: Commit**

```powershell
git add src/YxyXb.Core/Backup/PackageManifest.cs src/YxyXb.Core/Backup/PackageSplitter.cs src/YxyXb.Core/Backup/ZipPacker.cs tests/YxyXb.Core.Tests/PackageSplitterTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: split and assemble backup package volumes"
```

---

### Task 3: 配置存储和任务覆盖值

**Files:**
- Modify: `src/YxyXb.Contracts/Dto/AppSettingsDto.cs`
- Modify: `src/YxyXb.Contracts/Dto/FileTaskDto.cs`
- Modify: `src/YxyXb.Contracts/Dto/MssqlDtos.cs`
- Modify: `src/YxyXb.Contracts/Dto/MysqlDtos.cs`
- Modify: `src/YxyXb.Core/Data/SchemaSql.cs`
- Modify: `src/YxyXb.Core/Data/SchemaMigrate.cs`
- Modify: `src/YxyXb.Core/Store/SettingsStore.cs`
- Modify: `src/YxyXb.Core/Store/FileTaskStore.cs`
- Modify: `src/YxyXb.Core/Store/MssqlTaskStore.cs`
- Modify: `src/YxyXb.Core/Store/MysqlTaskStore.cs`
- Modify: `src/YxyXb.Client/Pages/SettingsPage.cs`
- Modify: `src/YxyXb.Client/Dialogs/FileTaskDialog.xaml(.cs)`
- Modify: `src/YxyXb.Client/Dialogs/MssqlTaskDialog.xaml(.cs)`
- Modify: `src/YxyXb.Client/Dialogs/MysqlTaskDialog.cs`
- Create: `tests/YxyXb.Core.Tests/PackageSettingsStoreTests.cs`

**Interfaces:**
- Produces: `AppSettingsDto.PartSizeBytes` 和三个任务 DTO 的同名属性
- 任务值 `0` 表示继承；全局值保存时经过 `NormalizeGlobal`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void SettingsAndTask_KeepPartSizeBytes()
{
    var task = new FileTaskDto { PartSizeBytes = 2L << 30 };
    Assert.That(PackageLimits.Resolve(8L << 30, task.PartSizeBytes),
        Is.EqualTo(2L << 30));
    Assert.That(PackageLimits.Resolve(8L << 30, 0), Is.EqualTo(8L << 30));
}
```

使用 `SqliteDb` 的测试还要分别插入、读取、更新 `settings`、`file_tasks`、`mssql_tasks`、`mysql_tasks`，断言 `PartSizeBytes` 往返一致。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PackageSettingsStoreTests`

Expected: FAIL，DTO 无属性。

- [ ] **Step 3: Write minimal implementation**

DTO 增加：

```csharp
public long PartSizeBytes { get; set; }
```

`SchemaSql` 的设置表增加 `part_size_bytes INTEGER NOT NULL DEFAULT 4294967296`；三个任务表增加 `part_size_bytes INTEGER NOT NULL DEFAULT 0`。

`SchemaMigrate.Apply` 增加：

```csharp
Add(db, "settings", "part_size_bytes", "INTEGER NOT NULL DEFAULT 4294967296");
Add(db, "file_tasks", "part_size_bytes", "INTEGER NOT NULL DEFAULT 0");
Add(db, "mssql_tasks", "part_size_bytes", "INTEGER NOT NULL DEFAULT 0");
Add(db, "mysql_tasks", "part_size_bytes", "INTEGER NOT NULL DEFAULT 0");
```

`SettingsStore` 默认值使用 `PackageLimits.DefaultBytes`，保存使用 `NormalizeGlobal`。三个任务 Store 读取和保存 `part_size_bytes`，大于 `0` 时使用 `NormalizeTask`。

设置页在“备份存储”增加“单个分卷上限（GiB）”：

```csharp
s.PartSizeBytes = PackageLimits.NormalizeGlobal((long)Math.Round(gib * 1073741824d));
```

三个任务对话框增加“分卷上限（GB，0=跟随全局）”；保存：

```csharp
task.PartSizeBytes = gib == 0 ? 0 : PackageLimits.NormalizeTask((long)Math.Round(gib * 1073741824d));
```

- [ ] **Step 4: Run test and build**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PackageSettingsStoreTests`

Run: `dotnet build src/YxyXb.Client/YxyXb.Client.csproj --nologo`

Expected: PASS，Client build 0 errors。

- [ ] **Step 5: Commit**

```powershell
git add src/YxyXb.Contracts src/YxyXb.Core/Data src/YxyXb.Core/Store src/YxyXb.Client tests/YxyXb.Core.Tests/PackageSettingsStoreTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: configure backup volume limits"
```

---

### Task 4: 严格扫描、分卷任务、逐文件补传和邮件

**Files:**
- Modify: `src/YxyXb.Core/Backup/SourceWalker.cs`
- Modify: `src/YxyXb.Core/Backup/PendingBackup.cs`
- Modify: `src/YxyXb.Core/Backup/PendingFiles.cs`
- Modify: `src/YxyXb.Core/Backup/DistChannel.cs`
- Modify: `src/YxyXb.Core/Backup/FileBackupRunner.cs`
- Modify: `src/YxyXb.Core/Transfer/MailUtil.cs`
- Modify: `src/YxyXb.Contracts/Cloud/ICloudStorage.cs`
- Create: `src/YxyXb.Contracts/Cloud/CloudObjectInfo.cs`
- Modify: `src/YxyXb.Core/Cloud/CloudRuntime.cs`
- Modify: `src/YxyXb.Cloud/CloudRuntimeImpl.cs`
- Modify: 五个云存储适配器
- Create: `tests/YxyXb.Core.Tests/SourceWalkerTests.cs`
- Create: `tests/YxyXb.Core.Tests/PendingUploadTests.cs`
- Create: `tests/YxyXb.Core.Tests/MailUtilTests.cs`

**Interfaces:**
- `SourceScanResult`：`Files`、`SkippedPaths`、`Stats`
- `PendingUploadFile`：`LocalPath`、`NetworkOk`、`FtpOk`、`CloudOk`
- `PendingItem.UploadFiles`、`PendingRecord.BackupId`、`PendingRecord.RunStats`
- `CloudObjectInfo`：`Size`、`Checksum`、`ChecksumKind`
- `ICloudStorage.Stat(remoteKey)` 和 `ICloudRuntime.Verify(...)`
- `MailRunStats`：`FileStats`、`PackageStats`、`SizeStats`

- [ ] **Step 1: Write the failing tests**

`SourceWalkerTests` 创建 `a.txt` 和锁定且独占打开的 `b.txt`，断言：

```csharp
Assert.That(result.Stats.DiscoveredFiles, Is.EqualTo(2));
Assert.That(result.Stats.PackedFiles, Is.EqualTo(1));
Assert.That(result.Stats.SkippedFiles, Is.EqualTo(1));
Assert.That(result.Stats.SourceBytes, Is.EqualTo(4));
```

`PendingUploadTests` 创建两个分卷和一个清单，断言只需补传未完成文件；清单位于 `UploadFiles` 最后。

`MailUtilTests`：

```csharp
var stats = new MailRunStats
{
    FileStats = "发现 100，打包 99，跳过 1",
    PackageStats = "共 3 个分卷，上传成功 3/3",
    SizeStats = "源 10 GB，压缩后 4 GB，已上传 4 GB"
};
MailUtil.Compose(settings, "任务A", true, "ok", "sid", stats,
    out subject, out body);
Assert.That(body, Does.Contain("上传成功 3/3"));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter "FullyQualifiedName~SourceWalkerTests|FullyQualifiedName~PendingUploadTests|FullyQualifiedName~MailUtilTests"`

Expected: FAIL，新模型和变量不存在。

- [ ] **Step 3: Write minimal implementation**

`SourceWalker.ScanLocal`：

```csharp
public static SourceScanResult ScanLocal(string root)
{
    var files = new List<SourceFile>();
    var skipped = new List<string>();
    var stats = new BackupRunStats();
    Walk(root, root, files, skipped, stats);
    stats.PackedFiles = files.Count;
    stats.SkippedFiles = skipped.Count;
    return new SourceScanResult { Files = files, SkippedPaths = skipped, Stats = stats };
}
```

每个发现的文件先增加 `DiscoveredFiles`；成功读取后累加 `SourceBytes`；打开、取长度或读取失败加入跳过列表。原 `WalkLocal` 保留并返回 `ScanLocal(root).Files`。

`FileBackupRunner` 在打包后：

```csharp
var limit = PackageLimits.Resolve(settings.PartSizeBytes, task.PartSizeBytes);
var split = PackageSplitter.Split(
    zipPath, task.Name, limit, scan.Stats,
    delegate { return PackageManifestCodec.NewBackupId(now); });
if (split != null)
{
    File.Delete(zipPath);
    PendingFiles.SetSplitItem(packed, split, scan.Stats);
}
var dataOk = distOk && scan.Stats.SkippedFiles == 0;
```

分卷目录写入 `PendingItem.UploadFiles`。每个文件带独立的 network、FTP、cloud 成功标记；上传顺序是全部分卷后外部清单。单 ZIP 继续构造一条历史兼容记录。

上传云对象后调用 `ICloudRuntime.Verify`。七牛使用 `/stat` 的 `fsize` 和 `hash`；百度在父目录 `list` 中匹配文件名、大小和块摘要；阿里云、腾讯云、天翼云使用 HEAD 获取 `Content-Length` 和 `ETag`，返回 `CloudObjectInfo`。大小不一致即失败。

邮件：

```csharp
.Replace("{文件统计}", stats == null ? "" : stats.FileStats ?? "")
.Replace("{包统计}", stats == null ? "" : stats.PackageStats ?? "")
.Replace("{大小统计}", stats == null ? "" : stats.SizeStats ?? "")
```

保留旧 `SendResult/Compose` 重载并传入空 `MailRunStats`，测试邮件照常工作。

- [ ] **Step 4: Run tests and build**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter "FullyQualifiedName~SourceWalkerTests|FullyQualifiedName~PendingUploadTests|FullyQualifiedName~MailUtilTests"`

Run: `dotnet build src/YxyXb.Engine/YxyXb.Engine.csproj --nologo`

Expected: PASS，Engine build 0 errors。

- [ ] **Step 5: Commit**

```powershell
git add src/YxyXb.Core src/YxyXb.Contracts/Cloud src/YxyXb.Cloud src/YxyXb.Qiniu src/YxyXb.Baidu src/YxyXb.Aliyun src/YxyXb.TencentCloud src/YxyXb.Ctyun tests/YxyXb.Core.Tests
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: upload and verify split package volumes"
```

---

### Task 5: 文件恢复页和数据库目录恢复

**Files:**
- Create: `src/YxyXb.Core/Backup/FileRestoreService.cs`
- Modify: `src/YxyXb.Client/Pages/RestorePage.cs`
- Modify: `src/YxyXb.Mssql/MssqlRestore.cs`
- Modify: `src/YxyXb.MySql/MysqlRestore.cs`
- Create: `tests/YxyXb.Core.Tests/FileRestoreServiceTests.cs`

**Interfaces:**
- `FileRestoreService.Restore(inputDirectory, outputParent, password)` → `FileRestoreResult`
- `FileRestoreResult`：`OutputDirectory`、`FileCount`、`Bytes`
- 恢复页“文件”页签提供输入目录、恢复父目录、密码和开始按钮
- MSSQL/MySQL 输入目录先组装为临时 ZIP，再复用现有恢复函数

- [ ] **Step 1: Write the failing test**

创建包含 `a.txt` 的 ZIP，拆到 `downloads`，删除完整 ZIP，调用：

```csharp
var result = FileRestoreService.Restore(downloads, outputParent, "");
Assert.That(Directory.Exists(result.OutputDirectory), Is.True);
Assert.That(File.ReadAllText(Path.Combine(result.OutputDirectory, "a.txt")),
    Is.EqualTo("hello"));
Assert.That(result.OutputDirectory, Does.Contain("restore-"));
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~FileRestoreServiceTests`

Expected: FAIL，`FileRestoreService` 不存在。

- [ ] **Step 3: Write minimal implementation**

```csharp
public static FileRestoreResult Restore(
    string inputDirectory, string outputParent, string password)
{
    var work = Path.Combine(Path.GetTempPath(), "yxy-assemble-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(work);
    var assembled = PackageSplitter.Assemble(inputDirectory, work);
    var output = PackageManifestCodec.CreateUniqueTimestampDirectory(outputParent);
    try
    {
        ZipPacker.ExtractAllSafe(assembled, password, output);
        var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
        return new FileRestoreResult
        {
            OutputDirectory = output,
            FileCount = files.Length,
            Bytes = files.Sum(PathFileLength)
        };
    }
    catch
    {
        TryDelete(output);
        throw;
    }
    finally
    {
        TryDelete(work);
    }
}
```

`RestorePage` 的第一个页签改为“文件”。用户选择一个目录；程序调用 `PackageSplitter.ScanUnit`，只允许一个 `BackupId`。如果外部清单不存在，从任一分卷内嵌清单恢复。输出路径使用 `restore-yyyyMMdd-HHmmss`，重复时追加 `-01`、`-02`。

MSSQL/MySQL 保留直接选择单 ZIP 的功能，同时支持目录。目录模式先组装，再把临时 ZIP 传给现有 `ExtractFirst`。

- [ ] **Step 4: Run test and build**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~FileRestoreServiceTests`

Run: `dotnet build src/YxyXb.Client/YxyXb.Client.csproj --nologo`

Expected: PASS，Client build 0 errors。

- [ ] **Step 5: Commit**

```powershell
git add src/YxyXb.Core/Backup/FileRestoreService.cs src/YxyXb.Client/Pages/RestorePage.cs src/YxyXb.Mssql/MssqlRestore.cs src/YxyXb.MySql/MysqlRestore.cs tests/YxyXb.Core.Tests/FileRestoreServiceTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: restore manually downloaded backup volumes"
```

---

### Task 6: MSSQL/MySQL 拆卷、整单元清理和最终验收

**Files:**
- Modify: `src/YxyXb.Mssql/MssqlRunner.cs`
- Modify: `src/YxyXb.MySql/MysqlRunner.cs`
- Modify: `src/YxyXb.Core/Backup/PurgeRunner.cs`
- Modify: `src/YxyXb.Contracts/Cloud/CloudObjectKeys.cs`
- Modify: 五个云存储适配器
- Modify: `src/YxyXb.Core/Transfer/UncShare.cs`
- Modify: `src/YxyXb.Core/Transfer/FtpUtil.cs`
- Create: `tests/YxyXb.Core.Tests/PurgeUnitTests.cs`

**Interfaces:**
- `CloudObjectKeys.ForPackageDirectory(softwareId, kind, taskName, backupId)` → 分卷单元云前缀
- `BackupUnitPurge.IsExpiredUnit(directory, olderThanUtc)` 判断整单元是否过期
- `BackupUnitPurge.DeleteLocalUnit(directory)` 在全部文件可删时整体清理
- MSSQL/MySQL 每个成功导出的数据库 ZIP 使用同一 `PackageSplitter`

- [ ] **Step 1: Write the failing cleanup test**

```csharp
[Test]
public void LocalUnit_IsDeletedOnlyWhenEveryFileIsExpired()
{
    var dir = Path.Combine(Path.GetTempPath(), "yxy-unit-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var old = Path.Combine(dir, "a.part01.zip");
    var fresh = Path.Combine(dir, "a.manifest.json");
    File.WriteAllText(old, "x");
    File.WriteAllText(fresh, "{}");
    File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-10));
    File.SetLastWriteTimeUtc(fresh, DateTime.UtcNow);
    Assert.That(BackupUnitPurge.IsExpiredUnit(dir, DateTime.UtcNow.AddDays(-5)), Is.False);
    File.SetLastWriteTimeUtc(fresh, DateTime.UtcNow.AddDays(-10));
    Assert.That(BackupUnitPurge.IsExpiredUnit(dir, DateTime.UtcNow.AddDays(-5)), Is.True);
    Directory.Delete(dir, true);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PurgeUnitTests`

Expected: FAIL，`BackupUnitPurge` 不存在。

- [ ] **Step 3: Write minimal implementation**

数据库 Runner 对每个成功导出的 ZIP 执行：

```csharp
var stats = new BackupRunStats
{
    DiscoveredFiles = dbNames.Count,
    PackedFiles = succeededDatabases,
    SkippedFiles = dbNames.Count - succeededDatabases,
    SourceBytes = exportedBytes
};
var split = PackageSplitter.Split(
    zipPath, task.Name,
    PackageLimits.Resolve(settings.PartSizeBytes, task.PartSizeBytes),
    stats, delegate { return PackageManifestCodec.NewBackupId(now); });
```

分卷成功后删除完整 ZIP，并通过 `PendingFiles.SetSplitItem` 保存分卷与清单路径。`RunStats` 写入 `PendingRecord`，邮件对数据库任务显示“数据库/导出/跳过”。

`BackupUnitPurge.IsExpiredUnit` 读取目录中的 `<package>.manifest.json`，要求清单声明的全部分卷都存在，并比较每个文件 `LastWriteTimeUtc`。任何文件未过期或缺失时返回 `false`。`DeleteLocalUnit` 先删分卷，最后删清单，再删除空目录；任一删除失败返回错误。

本机、UNC、FTP 和五个云适配器都先按目录前缀分组，只有整组过期时删除；先删分卷，最后删清单。单 ZIP 保持原规则。云对象前缀：

```csharp
public static string ForPackageDirectory(
    string softwareId, string kind, string taskName, string backupId)
{
    return ProductPrefix(softwareId)
        + Safe(string.IsNullOrEmpty(kind) ? "file" : kind) + "/"
        + Safe(string.IsNullOrEmpty(taskName) ? "task" : taskName) + "/"
        + Safe(backupId);
}
```

- [ ] **Step 4: Run complete verification**

```powershell
dotnet build src/YxyXb.Core/YxyXb.Core.csproj --nologo
dotnet build src/YxyXb.Mssql/YxyXb.Mssql.csproj --nologo
dotnet build src/YxyXb.MySql/YxyXb.MySql.csproj --nologo
dotnet build src/YxyXb.Client/YxyXb.Client.csproj --nologo
dotnet build src/YxyXb.Engine/YxyXb.Engine.csproj --nologo
dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --nologo
```

Expected: 五个 build 均为 0 errors，全部测试 PASS。

- [ ] **Step 5: Manual acceptance and commit**

手工验证：

1. 用 8 MiB 上限生成 20 MiB 测试文件，得到至少 3 个分卷，完整 ZIP 已删除。
2. 下载全部分卷与外部清单到新目录，恢复后文件字节一致。
3. 删除第二卷，恢复提示缺少分卷。
4. 修改第二卷一个字节，恢复提示哈希不一致。
5. 锁定一个源文件，任务失败，邮件显示发现、打包、跳过数量。
6. 模拟邮件失败，任务进入下一轮，日志显示“备份成功，通知失败”。
7. 把自动删除时间调到所有分卷之后，确认整套分卷和清单一起删除。

```powershell
git add src tests/YxyXb.Core.Tests
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: complete split package backup workflow"
```

---

## Self-Review

- [ ] 规格中的拆分、内嵌清单、4 GiB 默认值、任务覆盖、严格文件数、远端校验、邮件变量、目录恢复、时间戳输出和整单元清理均对应到任务。
- [ ] 搜索计划中的空白占位词，结果为空。
- [ ] 搜索 `PartSizeBytes`、`BackupRunStats`、`PackageSplitter`、`PendingUploadFile`、`MailRunStats`、`FileRestoreService`、`BackupUnitPurge`，确认跨任务命名一致。
- [ ] 不恢复当前工作树中无关的已删除测试或部署文件。
