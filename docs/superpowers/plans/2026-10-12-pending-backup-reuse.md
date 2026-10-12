# 未传完备份包复用 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 上传失败后的重试沿用同一个本地 zip，只补传未成功的通道；源变化或库到了下一轮计划时间时，先删掉未传完的旧包再打新包。

**Architecture:** 纯判断放在 `PendingBackup` / `SourceFingerprint`（不访问数据库和网盘）。任务行 `pending_json` 保存 zip 路径、文件指纹或库截止时间、三个通道是否已成功。三个 Runner 先问判断，再删除、打包、按通道补传。百度续传文件只在本地 zip 删除成功后，经 `ICloudRuntime.DiscardUpload` 丢掉。

**Tech Stack:** .NET Framework 4.8，NUnit，SQLite，现有 `ZipPacker` / `DistChannel` / `BaiduUploadCheckpoint` / `JsonUtil`。

**规格：** `docs/superpowers/specs/2026-10-12-pending-backup-reuse-design.md`

## Global Constraints

- 文件是否变化：相对路径、字节数、`LastWriteTimeUtc.Ticks` 的 SHA256。不算全文件校验。
- 压缩密码用 UTF-8 SHA256 比较。未设密码则摘要为空字符串。不把密码明文或摘要写入日志。
- JSON 键用 C# 属性名（`JavaScriptSerializer` 默认）：`SourceKind`、`SourcePath`、`InstanceId`、`DbNames`、`CycleUntilUtc`、`Items`、`Key`、`ZipPath`、`Fingerprint`、`ZipLevel`、`PasswordSha256`、`NetworkOk`、`FtpOk`、`CloudOk`。
- 库截止时间只在整份重新导出时写入：`ScheduleCalculator.NextAfter(周期, 间隔, 本次开始打包的 UTC)` 的 `"o"` 字符串。单次任务为空。失败重试不改它。
- 三个外发通道都未启用时不写 `pending_json`。
- `Decide` 只返回沿用或打新包（含要替换的项），不删除文件。`AllowPack(false)` 表示中止。
- 解析失败的 `pending_json` 当成没有记录，不删除对不上的磁盘文件。
- 不改媒体备份、百度分片算法、任务编辑对话框里的保存逻辑。保存任务的 `UPDATE` 不得清掉 `pending_json`。
- 不自动删除没有记录的历史 zip。
- 不恢复 `tests/` 里其它已删除的测试，只建本计划的测试。
- 提交命令：`git -c user.name=yxy -c user.email=yxy@local commit`（不改 git config）。
- 不写密钥、不提交 `appsettings.Local.json`。

## File map

| 文件 | 职责 |
| --- | --- |
| `src/YxyXb.Core/Backup/SourceFingerprint.cs` | 源指纹与密码摘要 |
| `src/YxyXb.Core/Backup/PendingBackup.cs` | 记录模型、解析、`DecideFile`、`DecideDatabase`、`AllowPack`、`ChannelsToSend` |
| `src/YxyXb.Core/Backup/PendingFiles.cs` | 删 zip / `.yxypart`、丢弃百度续传、按通道补传并回调保存 |
| `src/YxyXb.Contracts/Dto/FileTaskDto.cs` | `PendingJson` |
| `src/YxyXb.Contracts/Dto/MssqlDtos.cs` | `MssqlTaskDto.PendingJson` |
| `src/YxyXb.Contracts/Dto/MysqlDtos.cs` | `MysqlTaskDto.PendingJson` |
| `src/YxyXb.Core/Data/SchemaMigrate.cs` | 三表加 `pending_json` |
| `src/YxyXb.Core/Store/FileTaskStore.cs` | 读列、`SavePending`；`Update` 不碰该列 |
| `src/YxyXb.Core/Store/MssqlTaskStore.cs` | 同上，加 `ListByInstance` |
| `src/YxyXb.Core/Store/MysqlTaskStore.cs` | 同上，加 `ListByInstance` |
| `src/YxyXb.Core/Cloud/CloudRuntime.cs` | `DiscardUpload(string localPath)` |
| `src/YxyXb.Cloud/CloudRuntimeImpl.cs` | 调用 `BaiduUploadCheckpoint.Delete` |
| `src/YxyXb.Core/Backup/FileBackupRunner.cs` | 文件任务执行 |
| `src/YxyXb.Mssql/MssqlRunner.cs` | SQL Server 执行 |
| `src/YxyXb.MySql/MysqlRunner.cs` | MySQL 执行 |
| `src/YxyXb.Core/Store/MssqlInstanceStore.cs` | 删实例前清理未传完的包 |
| `src/YxyXb.Core/Store/MysqlInstanceStore.cs` | 同上 |
| `src/YxyXb.Client/Pages/TasksPage.cs` | 删除失败时提示，不刷新成已删 |
| `src/YxyXb.Client/Pages/InstancesPage.cs` | 同上 |
| `tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj` | 重建空测试工程 |
| `tests/YxyXb.Core.Tests/SourceFingerprintTests.cs` | 指纹 |
| `tests/YxyXb.Core.Tests/PendingBackupTests.cs` | 规格里的五类判断 |
| `tests/YxyXb.Core.Tests/PendingStoreTests.cs` | `pending_json` 经 `Update` 仍在 |

---

### Task 1: 源指纹

**Files:**
- Create: `tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj`
- Create: `tests/YxyXb.Core.Tests/SourceFingerprintTests.cs`
- Create: `src/YxyXb.Core/Backup/SourceFingerprint.cs`
- Test: `tests/YxyXb.Core.Tests/SourceFingerprintTests.cs`

`YxyXb.sln` 已引用 `tests\YxyXb.Core.Tests\YxyXb.Core.Tests.csproj`。不要再往解决方案里加一遍工程。

**Interfaces:**
- Consumes: 无
- Produces:
  - `FingerprintFile`：`string RelativePath`，`long Length`，`long LastWriteUtcTicks`
  - `SourceFingerprint.Compute(IList<FingerprintFile> files)` → 小写十六进制 SHA256
  - `SourceFingerprint.PasswordHash(string password)` → 未设密码返回 `""`，否则 UTF-8 SHA256 小写十六进制

- [ ] **Step 1: Write the failing test**

`tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj`：

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

`tests/YxyXb.Core.Tests/SourceFingerprintTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class SourceFingerprintTests
    {
        [Test]
        public void Compute_SameFilesAnyOrder_SameHash()
        {
            var a = new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "b.txt", Length = 2, LastWriteUtcTicks = 20 },
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 10 }
            };
            var b = new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 10 },
                new FingerprintFile { RelativePath = "b.txt", Length = 2, LastWriteUtcTicks = 20 }
            };
            Assert.That(SourceFingerprint.Compute(a), Is.EqualTo(SourceFingerprint.Compute(b)));
        }

        [Test]
        public void Compute_LengthOrTicksChange_DifferentHash()
        {
            var origin = SourceFingerprint.Compute(new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 10 }
            });
            var sized = SourceFingerprint.Compute(new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 2, LastWriteUtcTicks = 10 }
            });
            var timed = SourceFingerprint.Compute(new List<FingerprintFile>
            {
                new FingerprintFile { RelativePath = "a.txt", Length = 1, LastWriteUtcTicks = 11 }
            });
            Assert.That(sized, Is.Not.EqualTo(origin));
            Assert.That(timed, Is.Not.EqualTo(origin));
        }

        [Test]
        public void PasswordHash_EmptyIsBlank_ValueChangesHash()
        {
            Assert.That(SourceFingerprint.PasswordHash(null), Is.EqualTo(""));
            Assert.That(SourceFingerprint.PasswordHash(""), Is.EqualTo(""));
            Assert.That(SourceFingerprint.PasswordHash("甲"), Is.Not.EqualTo(SourceFingerprint.PasswordHash("乙")));
            Assert.That(SourceFingerprint.PasswordHash("甲"), Is.Not.EqualTo(""));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~SourceFingerprintTests`

Expected: FAIL（`SourceFingerprint` 不存在）

- [ ] **Step 3: Write minimal implementation**

`src/YxyXb.Core/Backup/SourceFingerprint.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace YxyXb.Core.Backup
{
    public sealed class FingerprintFile
    {
        public string RelativePath { get; set; }

        public long Length { get; set; }

        public long LastWriteUtcTicks { get; set; }
    }

    public static class SourceFingerprint
    {
        public static string Compute(IList<FingerprintFile> files)
        {
            var rows = new List<string>();
            if (files != null)
            {
                for (var i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var rel = file == null || file.RelativePath == null ? "" : file.RelativePath;
                    var length = file == null ? 0 : file.Length;
                    var ticks = file == null ? 0 : file.LastWriteUtcTicks;
                    rows.Add(rel + "|" + length.ToString(CultureInfo.InvariantCulture) + "|" + ticks.ToString(CultureInfo.InvariantCulture));
                }
            }

            rows.Sort(StringComparer.Ordinal);
            return Sha256Hex(string.Join("\n", rows.ToArray()));
        }

        public static string PasswordHash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "";
            }

            return Sha256Hex(password);
        }

        private static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~SourceFingerprintTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj tests/YxyXb.Core.Tests/SourceFingerprintTests.cs src/YxyXb.Core/Backup/SourceFingerprint.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: hash backup sources by path, size, and write time"
```

---

### Task 2: 沿用还是打新包

**Files:**
- Create: `src/YxyXb.Core/Backup/PendingBackup.cs`
- Create: `tests/YxyXb.Core.Tests/PendingBackupTests.cs`
- Test: `tests/YxyXb.Core.Tests/PendingBackupTests.cs`

**Interfaces:**
- Consumes: 无（本任务不调用 `SourceFingerprint`）
- Produces:
  - `PendingRecord`、`PendingItem`（属性名见 Global Constraints）
  - `PendingDecision`：`bool Reuse`，`bool ResetCycle`，`string Reason`，`PendingItem[] Replace`，`PendingItem[] Keep`
  - `PendingBackup.Parse(string json)`：空白或坏 JSON 返回 `null`
  - `PendingBackup.ToJson(PendingRecord record)`：`record == null` 返回 `null`
  - `PendingBackup.DecideFile(PendingRecord pending, FileSnapshot now, Func<string, bool> zipExists)`
  - `PendingBackup.DecideDatabase(PendingRecord pending, DatabaseSnapshot now, DateTime utcNow, Func<string, bool> zipExists)`
  - `FileSnapshot`：`SourceKind`，`SourcePath`，`Fingerprint`，`ZipLevel`，`PasswordSha256`
  - `DatabaseSnapshot`：`long InstanceId`，`string[] DbNames`，`int ZipLevel`，`string PasswordSha256`
  - `PendingBackup.AllowPack(bool zipDeleted)` → `zipDeleted`
  - `PendingBackup.ChannelsToSend(PendingItem item, bool doNetwork, bool doFtp, bool doCloud)` → `string[]`，元素为 `"network"` / `"ftp"` / `"cloud"`
  - `PendingBackup.RemoveItems(PendingRecord record, PendingItem[] remove)` 按 `ZipPath` 忽略大小写去掉项
  - `PendingBackup.Upsert(PendingRecord record, PendingItem item)` 按 `Key` 忽略大小写替换
  - `PendingBackup.CycleUntil(string cycleKind, int cycleInterval, DateTime packUtc)` → `NextAfter` 的 `"o"` 或 `null`
  - `PendingBackup.NeedsUpload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud)` → 当前启用的通道还有 false

- [ ] **Step 1: Write the failing test**

`tests/YxyXb.Core.Tests/PendingBackupTests.cs`：

```csharp
using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PendingBackupTests
    {
        [Test]
        public void DecideFile_SameFingerprint_Reuses()
        {
            var pending = FilePending("local", @"D:\src", "abc", 5, "hash", @"D:\bak\a.zip");
            var now = Snap("local", @"d:\src", "abc", 5, "hash");
            var decision = PendingBackup.DecideFile(pending, now, delegate(string path) { return true; });
            Assert.That(decision.Reuse, Is.True);
            Assert.That(decision.Replace.Length, Is.EqualTo(0));
            Assert.That(decision.Keep[0].ZipPath, Is.EqualTo(@"D:\bak\a.zip"));
        }

        [Test]
        public void DecideFile_SizeChangeOrMissingZipOrSource_Repacks()
        {
            var pending = FilePending("local", @"D:\src", "abc", 5, "hash", @"D:\bak\a.zip");
            var changed = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "zzz", 5, "hash"), delegate(string path) { return true; });
            Assert.That(changed.Reuse, Is.False);
            Assert.That(changed.Replace.Length, Is.EqualTo(1));

            var level = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "abc", 9, "hash"), delegate(string path) { return true; });
            Assert.That(level.Reuse, Is.False);

            var password = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "abc", 5, "other"), delegate(string path) { return true; });
            Assert.That(password.Reuse, Is.False);

            var path = PendingBackup.DecideFile(pending, Snap("local", @"E:\other", "abc", 5, "hash"), delegate(string p) { return true; });
            Assert.That(path.Reuse, Is.False);

            var kind = PendingBackup.DecideFile(pending, Snap("unc", @"D:\src", "abc", 5, "hash"), delegate(string p) { return true; });
            Assert.That(kind.Reuse, Is.False);

            var missing = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "abc", 5, "hash"), delegate(string p) { return false; });
            Assert.That(missing.Reuse, Is.False);
            Assert.That(missing.Replace[0].ZipPath, Is.EqualTo(@"D:\bak\a.zip"));
        }

        [Test]
        public void DecideFile_BadJson_DoesNotNameAZipToDelete()
        {
            Assert.That(PendingBackup.Parse("{"), Is.Null);
            var decision = PendingBackup.DecideFile(null, Snap("local", @"D:\src", "abc", 5, ""), delegate(string p) { return false; });
            Assert.That(decision.Reuse, Is.False);
            Assert.That(decision.Replace.Length, Is.EqualTo(0));
        }

        [Test]
        public void DecideDatabase_CycleInstanceAndMissingZip()
        {
            var until = new DateTime(2026, 10, 13, 2, 0, 0, DateTimeKind.Utc).ToString("o");
            var pending = DbPending(7, new[] { "A", "B" }, until, 5, "", @"D:\a.zip", @"D:\b.zip");
            var now = DbSnap(7, new[] { "B", "A" }, 5, "");
            var early = new DateTime(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);
            var reuse = PendingBackup.DecideDatabase(pending, now, early, delegate(string p) { return true; });
            Assert.That(reuse.Reuse, Is.True);

            var due = PendingBackup.DecideDatabase(pending, now, new DateTime(2026, 10, 13, 2, 0, 0, DateTimeKind.Utc), delegate(string p) { return true; });
            Assert.That(due.Reuse, Is.False);
            Assert.That(due.ResetCycle, Is.True);
            Assert.That(due.Replace.Length, Is.EqualTo(2));

            pending.CycleUntilUtc = null;
            var once = PendingBackup.DecideDatabase(pending, now, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), delegate(string p) { return true; });
            Assert.That(once.Reuse, Is.True);

            pending.CycleUntilUtc = until;
            var instance = PendingBackup.DecideDatabase(pending, DbSnap(8, new[] { "A", "B" }, 5, ""), early, delegate(string p) { return true; });
            Assert.That(instance.ResetCycle, Is.True);
            Assert.That(instance.Replace.Length, Is.EqualTo(2));

            var names = PendingBackup.DecideDatabase(pending, DbSnap(7, new[] { "A" }, 5, ""), early, delegate(string p) { return true; });
            Assert.That(names.ResetCycle, Is.True);

            var one = PendingBackup.DecideDatabase(pending, now, early, delegate(string p) { return !p.EndsWith("b.zip"); });
            Assert.That(one.Reuse, Is.False);
            Assert.That(one.ResetCycle, Is.False);
            Assert.That(one.Replace.Length, Is.EqualTo(1));
            Assert.That(one.Replace[0].Key, Is.EqualTo("B"));
        }

        [Test]
        public void ChannelsToSend_SkipsChannelAlreadyOk()
        {
            var item = new PendingItem { NetworkOk = true, FtpOk = false, CloudOk = false };
            var channels = PendingBackup.ChannelsToSend(item, true, false, true);
            Assert.That(channels, Is.EqualTo(new[] { "cloud" }));
            Assert.That(PendingBackup.NeedsUpload(item, true, false, true), Is.True);
            item.CloudOk = true;
            Assert.That(PendingBackup.NeedsUpload(item, true, false, true), Is.False);
            Assert.That(PendingBackup.NeedsUpload(item, false, false, false), Is.False);
        }

        [Test]
        public void AllowPack_FollowsDeleteResult()
        {
            Assert.That(PendingBackup.AllowPack(false), Is.False);
            Assert.That(PendingBackup.AllowPack(true), Is.True);
        }

        private static FileSnapshot Snap(string kind, string path, string fingerprint, int level, string passwordHash)
        {
            return new FileSnapshot
            {
                SourceKind = kind,
                SourcePath = path,
                Fingerprint = fingerprint,
                ZipLevel = level,
                PasswordSha256 = passwordHash
            };
        }

        private static PendingRecord FilePending(string kind, string path, string fingerprint, int level, string passwordHash, string zip)
        {
            return new PendingRecord
            {
                SourceKind = kind,
                SourcePath = path,
                Items = new[]
                {
                    new PendingItem
                    {
                        Key = "file",
                        ZipPath = zip,
                        Fingerprint = fingerprint,
                        ZipLevel = level,
                        PasswordSha256 = passwordHash
                    }
                }
            };
        }

        private static DatabaseSnapshot DbSnap(long instanceId, string[] names, int level, string passwordHash)
        {
            return new DatabaseSnapshot
            {
                InstanceId = instanceId,
                DbNames = names,
                ZipLevel = level,
                PasswordSha256 = passwordHash
            };
        }

        private static PendingRecord DbPending(long instanceId, string[] names, string until, int level, string passwordHash, string zipA, string zipB)
        {
            return new PendingRecord
            {
                InstanceId = instanceId,
                DbNames = names,
                CycleUntilUtc = until,
                Items = new[]
                {
                    new PendingItem { Key = "A", ZipPath = zipA, ZipLevel = level, PasswordSha256 = passwordHash },
                    new PendingItem { Key = "B", ZipPath = zipB, ZipLevel = level, PasswordSha256 = passwordHash }
                }
            };
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PendingBackupTests`

Expected: FAIL（`PendingBackup` 不存在）

- [ ] **Step 3: Write minimal implementation**

`src/YxyXb.Core/Backup/PendingBackup.cs` 全文：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using YxyXb.Core.Schedule;
using YxyXb.Core.Security;

namespace YxyXb.Core.Backup
{
    public sealed class PendingItem
    {
        public string Key { get; set; }

        public string ZipPath { get; set; }

        public string Fingerprint { get; set; }

        public int ZipLevel { get; set; }

        public string PasswordSha256 { get; set; }

        public bool NetworkOk { get; set; }

        public bool FtpOk { get; set; }

        public bool CloudOk { get; set; }
    }

    public sealed class PendingRecord
    {
        public string SourceKind { get; set; }

        public string SourcePath { get; set; }

        public long InstanceId { get; set; }

        public string[] DbNames { get; set; }

        public string CycleUntilUtc { get; set; }

        public PendingItem[] Items { get; set; }
    }

    public sealed class FileSnapshot
    {
        public string SourceKind { get; set; }

        public string SourcePath { get; set; }

        public string Fingerprint { get; set; }

        public int ZipLevel { get; set; }

        public string PasswordSha256 { get; set; }
    }

    public sealed class DatabaseSnapshot
    {
        public long InstanceId { get; set; }

        public string[] DbNames { get; set; }

        public int ZipLevel { get; set; }

        public string PasswordSha256 { get; set; }
    }

    public sealed class PendingDecision
    {
        public bool Reuse { get; set; }

        public bool ResetCycle { get; set; }

        public string Reason { get; set; }

        public PendingItem[] Replace { get; set; }

        public PendingItem[] Keep { get; set; }
    }

    public static class PendingBackup
    {
        public static PendingRecord Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var record = JsonUtil.FromJson<PendingRecord>(json);
                if (record == null || record.Items == null)
                {
                    return null;
                }

                return record;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToJson(PendingRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return JsonUtil.ToJson(record);
        }

        public static bool AllowPack(bool zipDeleted)
        {
            return zipDeleted;
        }

        public static string CycleUntil(string cycleKind, int cycleInterval, DateTime packUtc)
        {
            var next = ScheduleCalculator.NextAfter(cycleKind, cycleInterval, packUtc);
            if (!next.HasValue)
            {
                return null;
            }

            return next.Value.ToString("o");
        }

        public static string[] ChannelsToSend(PendingItem item, bool doNetwork, bool doFtp, bool doCloud)
        {
            var list = new List<string>();
            if (doNetwork && (item == null || !item.NetworkOk))
            {
                list.Add("network");
            }

            if (doFtp && (item == null || !item.FtpOk))
            {
                list.Add("ftp");
            }

            if (doCloud && (item == null || !item.CloudOk))
            {
                list.Add("cloud");
            }

            return list.ToArray();
        }

        public static bool NeedsUpload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud)
        {
            return ChannelsToSend(item, doNetwork, doFtp, doCloud).Length > 0;
        }

        public static PendingDecision DecideFile(PendingRecord pending, FileSnapshot now, Func<string, bool> zipExists)
        {
            var items = ItemsOf(pending);
            if (items.Length == 0)
            {
                return Repack("没有未传完的包", new PendingItem[0], false);
            }

            var item = items[0];
            var same = string.Equals(pending.SourceKind ?? "", now.SourceKind ?? "", StringComparison.OrdinalIgnoreCase)
                && string.Equals(pending.SourcePath ?? "", now.SourcePath ?? "", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Fingerprint ?? "", now.Fingerprint ?? "", StringComparison.Ordinal)
                && item.ZipLevel == now.ZipLevel
                && string.Equals(item.PasswordSha256 ?? "", now.PasswordSha256 ?? "", StringComparison.Ordinal);
            if (!same || !ZipStillThere(item, zipExists))
            {
                return Repack(same ? "本地包不在" : "源或压缩设置已变", items, false);
            }

            return new PendingDecision
            {
                Reuse = true,
                ResetCycle = false,
                Reason = "沿用未传完的包",
                Replace = new PendingItem[0],
                Keep = new[] { item }
            };
        }

        public static PendingDecision DecideDatabase(PendingRecord pending, DatabaseSnapshot now, DateTime utcNow, Func<string, bool> zipExists)
        {
            var items = ItemsOf(pending);
            if (items.Length == 0)
            {
                return Repack("没有未传完的包", new PendingItem[0], true);
            }

            if (pending.InstanceId != now.InstanceId || !SameNames(pending.DbNames, now.DbNames) || !SameZipSettings(items, now))
            {
                return Repack("实例、库列表或压缩设置已变", items, true);
            }

            DateTime until;
            if (!string.IsNullOrEmpty(pending.CycleUntilUtc)
                && DateTime.TryParse(pending.CycleUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until)
                && utcNow >= until)
            {
                return Repack("到了下一轮计划时间", items, true);
            }

            var missing = new List<PendingItem>();
            var keep = new List<PendingItem>();
            for (var i = 0; i < items.Length; i++)
            {
                if (ZipStillThere(items[i], zipExists))
                {
                    keep.Add(items[i]);
                }
                else
                {
                    missing.Add(items[i]);
                }
            }

            if (missing.Count == 0)
            {
                return new PendingDecision
                {
                    Reuse = true,
                    ResetCycle = false,
                    Reason = "沿用未传完的包",
                    Replace = new PendingItem[0],
                    Keep = keep.ToArray()
                };
            }

            return new PendingDecision
            {
                Reuse = false,
                ResetCycle = false,
                Reason = "缺少本地包",
                Replace = missing.ToArray(),
                Keep = keep.ToArray()
            };
        }

        public static PendingRecord RemoveItems(PendingRecord record, PendingItem[] remove)
        {
            if (record == null)
            {
                return new PendingRecord { Items = new PendingItem[0] };
            }

            var keep = new List<PendingItem>();
            var items = record.Items ?? new PendingItem[0];
            for (var i = 0; i < items.Length; i++)
            {
                if (!Listed(remove, items[i]))
                {
                    keep.Add(items[i]);
                }
            }

            record.Items = keep.ToArray();
            return record;
        }

        public static void Upsert(PendingRecord record, PendingItem item)
        {
            var list = new List<PendingItem>();
            var items = record.Items ?? new PendingItem[0];
            for (var i = 0; i < items.Length; i++)
            {
                if (!string.Equals(items[i].Key ?? "", item.Key ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(items[i]);
                }
            }

            list.Add(item);
            record.Items = list.ToArray();
        }

        private static PendingDecision Repack(string reason, PendingItem[] replace, bool resetCycle)
        {
            return new PendingDecision
            {
                Reuse = false,
                ResetCycle = resetCycle,
                Reason = reason,
                Replace = replace ?? new PendingItem[0],
                Keep = new PendingItem[0]
            };
        }

        private static PendingItem[] ItemsOf(PendingRecord pending)
        {
            if (pending == null || pending.Items == null)
            {
                return new PendingItem[0];
            }

            return pending.Items;
        }

        private static bool ZipStillThere(PendingItem item, Func<string, bool> zipExists)
        {
            if (item == null || string.IsNullOrEmpty(item.ZipPath) || zipExists == null)
            {
                return false;
            }

            return zipExists(item.ZipPath);
        }

        private static bool SameZipSettings(PendingItem[] items, DatabaseSnapshot now)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].ZipLevel != now.ZipLevel
                    || !string.Equals(items[i].PasswordSha256 ?? "", now.PasswordSha256 ?? "", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameNames(string[] left, string[] right)
        {
            var a = SortCopy(left);
            var b = SortCopy(right);
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] SortCopy(string[] names)
        {
            var copy = names == null ? new string[0] : (string[])names.Clone();
            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }

        private static bool Listed(PendingItem[] remove, PendingItem item)
        {
            if (remove == null || item == null)
            {
                return false;
            }

            for (var i = 0; i < remove.Length; i++)
            {
                if (string.Equals(remove[i].ZipPath ?? "", item.ZipPath ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PendingBackupTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YxyXb.Core/Backup/PendingBackup.cs tests/YxyXb.Core.Tests/PendingBackupTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: decide when a failed backup zip can be reused"
```

---

### Task 3: 任务行保存 pending_json

**Files:**
- Modify: `src/YxyXb.Core/Data/SchemaMigrate.cs`（`Apply` 末尾）
- Modify: `src/YxyXb.Contracts/Dto/FileTaskDto.cs`（`DoEmail` 后）
- Modify: `src/YxyXb.Contracts/Dto/MssqlDtos.cs`（`MssqlTaskDto`）
- Modify: `src/YxyXb.Contracts/Dto/MysqlDtos.cs`（`MysqlTaskDto`）
- Modify: `src/YxyXb.Core/Store/FileTaskStore.cs`
- Modify: `src/YxyXb.Core/Store/MssqlTaskStore.cs`
- Modify: `src/YxyXb.Core/Store/MysqlTaskStore.cs`
- Test: `tests/YxyXb.Core.Tests/PendingStoreTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: 三张 DTO 的 `string PendingJson`。`FileTaskStore.SavePending(SqliteDb, long, string)`、`MssqlTaskStore.SavePending`、`MysqlTaskStore.SavePending`。`MssqlTaskStore.ListByInstance(SqliteDb, long)`、`MysqlTaskStore.ListByInstance`。`Insert` / `Update` 的 SQL 不包含 `pending_json`。

- [ ] **Step 1: Write the failing test**

`tests/YxyXb.Core.Tests/PendingStoreTests.cs`：

```csharp
using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    public class PendingStoreTests
    {
        [Test]
        public void Update_KeepsPendingJson()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-pending-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    var fileId = FileTaskStore.Insert(db, NewFile());
                    FileTaskStore.SavePending(db, fileId, "{\"Items\":[]}");
                    var fileTask = FileTaskStore.GetById(db, fileId);
                    fileTask.Name = "改名";
                    FileTaskStore.Update(db, fileTask);
                    var fileAgain = FileTaskStore.GetById(db, fileId);
                    Assert.That(fileAgain.Name, Is.EqualTo("改名"));
                    Assert.That(fileAgain.PendingJson, Is.EqualTo("{\"Items\":[]}"));

                    var mssqlId = MssqlTaskStore.Insert(db, NewMssql());
                    MssqlTaskStore.SavePending(db, mssqlId, "{\"InstanceId\":1}");
                    var mssql = MssqlTaskStore.GetById(db, mssqlId);
                    mssql.Name = "库改名";
                    MssqlTaskStore.Update(db, mssql);
                    Assert.That(MssqlTaskStore.GetById(db, mssqlId).PendingJson, Is.EqualTo("{\"InstanceId\":1}"));

                    var mysqlId = MysqlTaskStore.Insert(db, NewMysql());
                    MysqlTaskStore.SavePending(db, mysqlId, "{\"InstanceId\":2}");
                    var mysql = MysqlTaskStore.GetById(db, mysqlId);
                    mysql.Name = "mysql改名";
                    MysqlTaskStore.Update(db, mysql);
                    Assert.That(MysqlTaskStore.GetById(db, mysqlId).PendingJson, Is.EqualTo("{\"InstanceId\":2}"));
                    Assert.That(MysqlTaskStore.ListByInstance(db, 2).Count, Is.EqualTo(1));
                }
            }
            finally
            {
                TryDeleteDb(file);
            }
        }

        private static FileTaskDto NewFile()
        {
            return new FileTaskDto
            {
                Name = "文件",
                Enabled = true,
                SourceKind = "local",
                SourcePath = @"D:\src",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
        }

        private static MssqlTaskDto NewMssql()
        {
            return new MssqlTaskDto
            {
                Name = "库",
                InstanceId = 1,
                Enabled = true,
                DbScope = "user",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
        }

        private static MysqlTaskDto NewMysql()
        {
            return new MysqlTaskDto
            {
                Name = "mysql",
                InstanceId = 2,
                Enabled = true,
                DbScope = "user",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
        }

        private static void TryDeleteDb(string file)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                if (File.Exists(file + "-wal")) File.Delete(file + "-wal");
                if (File.Exists(file + "-shm")) File.Delete(file + "-shm");
            }
            catch (IOException)
            {
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PendingStoreTests`

Expected: FAIL（`SavePending` 或 `PendingJson` 不存在）

- [ ] **Step 3: Write minimal implementation**

`SchemaMigrate.Apply` 在现有最后一条 `Add` 之后加：

```csharp
Add(db, "file_tasks", "pending_json", "TEXT");
Add(db, "mssql_tasks", "pending_json", "TEXT");
Add(db, "mysql_tasks", "pending_json", "TEXT");
```

三个 DTO 各加：

```csharp
public string PendingJson { get; set; }
```

`FileTaskStore.Read` 在 `PostProgram` 赋值后加：

```csharp
PendingJson = row["pending_json"] == DBNull.Value ? null : Convert.ToString(row["pending_json"])
```

`MssqlTaskStore.Read`、`MysqlTaskStore.Read` 同样读取 `pending_json`。不要把该列写进 `Insert` 或 `Update` 的 SQL。

三个 Store 各加：

```csharp
public static void SavePending(SqliteDb db, long id, string json)
{
    db.Execute(
        "UPDATE file_tasks SET pending_json=@p WHERE id=@id",
        new SQLiteParameter("@p", (object)json ?? DBNull.Value),
        new SQLiteParameter("@id", id));
}
```

MSSQL 用表名 `mssql_tasks`，MySQL 用 `mysql_tasks`。

两个库 Store 各加：

```csharp
public static IList<MssqlTaskDto> ListByInstance(SqliteDb db, long instanceId)
{
    var table = db.Query("SELECT * FROM mssql_tasks WHERE instance_id=@id", new SQLiteParameter("@id", instanceId));
    var list = new List<MssqlTaskDto>();
    foreach (DataRow row in table.Rows)
    {
        list.Add(Read(row));
    }

    return list;
}
```

MySQL 版把类型和表名换成 `MysqlTaskDto` / `mysql_tasks`。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PendingStoreTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YxyXb.Core/Data/SchemaMigrate.cs src/YxyXb.Contracts/Dto/FileTaskDto.cs src/YxyXb.Contracts/Dto/MssqlDtos.cs src/YxyXb.Contracts/Dto/MysqlDtos.cs src/YxyXb.Core/Store/FileTaskStore.cs src/YxyXb.Core/Store/MssqlTaskStore.cs src/YxyXb.Core/Store/MysqlTaskStore.cs tests/YxyXb.Core.Tests/PendingStoreTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: keep unfinished backup state on the task row"
```

---

### Task 4: 删除本地包与百度续传文件

**Files:**
- Modify: `src/YxyXb.Core/Cloud/CloudRuntime.cs`
- Modify: `src/YxyXb.Cloud/CloudRuntimeImpl.cs`
- Create: `src/YxyXb.Core/Backup/PendingFiles.cs`

**Interfaces:**
- Consumes: `PendingItem`，`PendingBackup.AllowPack`，`BaiduUploadCheckpoint.Delete(string dataDir, string localPath)`，`DistChannel.UncAndFtp`，`DistChannel.Cloud`
- Produces:
  - `ICloudRuntime.DiscardUpload(string localPath)`
  - `PendingFiles.TryDeleteZip(string zipPath)`：文件不存在返回 true；删除 zip 和 `zipPath + ".yxypart"`；`IOException` / `UnauthorizedAccessException` 返回 false
  - `PendingFiles.DeleteReplacements(PendingItem[] items, StringBuilder log)`：任一项 `TryDeleteZip` 失败则立刻 false，且不调用该项的 `DiscardUpload`。成功项在 zip 删除成功后调用 `CloudRuntime.Handler.DiscardUpload`（Handler 为 null 则跳过）。续传删除抛错只追加日志，仍返回 true（若 zip 都删掉了）
  - `PendingFiles.UploadItem(SqliteDb db, PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, string kind, string taskName, StringBuilder log, Action persist)`：只上传 `ChannelsToSend` 里的通道；某个通道成功后先改 `*Ok` 再 `persist()`；任一失败返回 false，但继续尝试后面的通道

- [ ] **Step 1: Write the failing test**

在 `PendingBackupTests` 增加（仍不连百度）：

```csharp
[Test]
public void TryDeleteZip_RemovesZipAndPart()
{
    var dir = Path.Combine(Path.GetTempPath(), "yxy-zip-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var zip = Path.Combine(dir, "a.zip");
    File.WriteAllText(zip, "zip");
    File.WriteAllText(zip + ".yxypart", "part");
    try
    {
        Assert.That(PendingFiles.TryDeleteZip(zip), Is.True);
        Assert.That(File.Exists(zip), Is.False);
        Assert.That(File.Exists(zip + ".yxypart"), Is.False);
        Assert.That(PendingFiles.TryDeleteZip(zip), Is.True);
    }
    finally
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter TryDeleteZip_RemovesZipAndPart`

Expected: FAIL（`PendingFiles` 不存在）

- [ ] **Step 3: Write minimal implementation**

`ICloudRuntime` 增加：

```csharp
void DiscardUpload(string localPath);
```

`CloudRuntimeImpl`：

```csharp
public void DiscardUpload(string localPath)
{
    if (string.IsNullOrEmpty(localPath))
    {
        return;
    }

    BaiduUploadCheckpoint.Delete(AppPaths.DataDirectory, localPath);
}
```

`src/YxyXb.Core/Backup/PendingFiles.cs`：

```csharp
using System;
using System.IO;
using System.Text;
using YxyXb.Core.Cloud;
using YxyXb.Core.Data;

namespace YxyXb.Core.Backup
{
    public static class PendingFiles
    {
        public static bool TryDeleteZip(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath))
            {
                return true;
            }

            try
            {
                var part = zipPath + ".yxypart";
                if (File.Exists(part))
                {
                    File.Delete(part);
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool DeleteReplacements(PendingItem[] items, StringBuilder log)
        {
            if (items == null)
            {
                return true;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var path = items[i] == null ? null : items[i].ZipPath;
                if (!PendingBackup.AllowPack(TryDeleteZip(path)))
                {
                    log.Append(" 删除未传完的包失败：").Append(path).Append("。");
                    return false;
                }

                DiscardQuiet(path, log);
                log.Append(" 已删除未传完的包：").Append(path).Append("。");
            }

            return true;
        }

        public static bool UploadItem(SqliteDb db, PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, string kind, string taskName, StringBuilder log, Action persist)
        {
            var ok = true;
            var channels = PendingBackup.ChannelsToSend(item, doNetwork, doFtp, doCloud);
            for (var i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                var sent = false;
                if (channel == "network")
                {
                    sent = DistChannel.UncAndFtp(db, true, false, item.ZipPath, log);
                    if (sent) item.NetworkOk = true;
                }
                else if (channel == "ftp")
                {
                    sent = DistChannel.UncAndFtp(db, false, true, item.ZipPath, log);
                    if (sent) item.FtpOk = true;
                }
                else if (channel == "cloud")
                {
                    sent = DistChannel.Cloud(db, true, provider, kind, taskName, item.ZipPath, log);
                    if (sent) item.CloudOk = true;
                }

                if (sent)
                {
                    if (persist != null) persist();
                }
                else
                {
                    ok = false;
                }
            }

            return ok;
        }

        private static void DiscardQuiet(string path, StringBuilder log)
        {
            var handler = CloudRuntime.Handler;
            if (handler == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                handler.DiscardUpload(path);
            }
            catch (Exception ex)
            {
                log.Append(" 百度续传文件未删除：").Append(ex.Message).Append("。");
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj --filter FullyQualifiedName~PendingBackupTests`

Expected: PASS（含 `TryDeleteZip_RemovesZipAndPart`）

- [ ] **Step 5: Commit**

```bash
git add src/YxyXb.Core/Cloud/CloudRuntime.cs src/YxyXb.Cloud/CloudRuntimeImpl.cs src/YxyXb.Core/Backup/PendingFiles.cs tests/YxyXb.Core.Tests/PendingBackupTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: delete an unfinished zip before packing a replacement"
```

---

### Task 5: 文件备份执行

**Files:**
- Modify: `src/YxyXb.Core/Backup/FileBackupRunner.cs`（`ExecuteOne` 的 try 块，约 68–116 行）

**Interfaces:**
- Consumes: `SourceFingerprint`，`PendingBackup.DecideFile` / `Parse` / `ToJson` / `RemoveItems`，`PendingFiles.DeleteReplacements` / `UploadItem`，`FileTaskStore.SavePending`，现有 `ZipPacker`、`DistChannel.Finish`、`BackupNaming`
- Produces: 文件任务失败重试不再生成第二个 zip。成功后 `SavePending(db, id, null)`。`MarkResult` 不改。

行为：

1. 现有 `PrepareSource` 之后扫描并算指纹。读大小或修改时间抛错时，异常冒泡，不调用 `DeleteReplacements`。
2. `DecideFile`。`Reuse` 时：`TryDeleteDir(tempRoot)` 并把 `tempRoot` 置空，日志 ` 沿用未传完的包 ` + 路径。zip 用 `Keep[0].ZipPath`。
3. 非 `Reuse`：先 `DeleteReplacements(decision.Replace, log)`。返回 false 则 `throw new InvalidOperationException("删除未传完的包失败，已中止。")`，不要 `SavePending`。成功后 `RemoveItems` 并立刻 `SavePending`。再按现在的命名打新包。日志在 `Replace.Length > 0` 时写 ` 重新打包：` + `decision.Reason`。FTP 临时目录留到现有 `finally`。
4. 任务启用了任一外发通道时，打包完成后把新 `PendingItem`（`Key = "file"`，三个 Ok 为 false）写入记录并 `SavePending`，然后再 `UploadItem`。三个通道都没启用：打完包后 `SavePending(db, id, null)`，不调用上传。
5. `UploadItem` 的 `persist` 把当前 `PendingBackup.ToJson(record)` 写入 `SavePending`，并赋给 `task.PendingJson`。
6. `distOk` 取 `UploadItem` 的返回值。没有要上传的通道时 `distOk = true`。然后仍调用 `DistChannel.Finish`。`Finish` 返回 true 后 `SavePending(db, id, null)`，再跑后续程序。返回 false 的现有 `throw` 保留，从而邮件失败时记录还在。

`ExecuteOne` 里替换从取得 `settings` 到 `ok = true` 的打包和分发。`catch` / `finally` / `MarkResult` 保持原样。指纹文件用 `SourceWalker` 的 `RelativePath`，不要改写分隔符：

```csharp
var fingerprintFiles = new List<FingerprintFile>();
for (var i = 0; i < files.Count; i++)
{
    var item = files[i];
    var info = new FileInfo(item.FullPath);
    fingerprintFiles.Add(new FingerprintFile
    {
        RelativePath = item.RelativePath,
        Length = info.Length,
        LastWriteUtcTicks = File.GetLastWriteTimeUtc(item.FullPath).Ticks
    });
}

var passwordHash = SourceFingerprint.PasswordHash(settings.ZipPassword);
var fingerprint = SourceFingerprint.Compute(fingerprintFiles);
var record = PendingBackup.Parse(task.PendingJson);
var decision = PendingBackup.DecideFile(record, new FileSnapshot
{
    SourceKind = task.SourceKind ?? "local",
    SourcePath = task.SourcePath ?? "",
    Fingerprint = fingerprint,
    ZipLevel = settings.ZipLevel,
    PasswordSha256 = passwordHash
}, File.Exists);
```

沿用时不要调用 `ZipPacker.Pack`。新包项：

```csharp
var packed = new PendingItem
{
    Key = "file",
    ZipPath = zipPath,
    Fingerprint = fingerprint,
    ZipLevel = settings.ZipLevel,
    PasswordSha256 = passwordHash
};
if (record == null) record = new PendingRecord();
record.SourceKind = task.SourceKind ?? "local";
record.SourcePath = task.SourcePath ?? "";
record.CycleUntilUtc = null;
record.Items = new[] { packed };
```

`track` 为 `task.DoNetwork || task.DoFtp || task.DoCloud`。只有 `track` 为 true 才在上传前保存这条记录。`ZipPacker.Pack` 抛错时调用 `PendingFiles.TryDeleteZip(zipPath)` 清掉半成品 `.yxypart`，不要 `SavePending` 新项，然后把异常继续抛出。

- [ ] **Step 1: 改 ExecuteOne**

按上面的顺序改 `FileBackupRunner.ExecuteOne`。不要改 `PrepareSource`、`TryDeleteDir`、`MarkResult`。

- [ ] **Step 2: 编译并跑判断测试**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj`

Expected: PASS。另外 `dotnet build src/YxyXb.Core/YxyXb.Core.csproj` 成功。

- [ ] **Step 3: Commit**

```bash
git add src/YxyXb.Core/Backup/FileBackupRunner.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: reuse a file backup zip when upload failed and sources match"
```

---

### Task 6: 数据库备份执行

**Files:**
- Modify: `src/YxyXb.Mssql/MssqlRunner.cs`（`ExecuteOne` 里从 `ResolveDatabases` 到 `Finish` 的循环）
- Modify: `src/YxyXb.MySql/MysqlRunner.cs`（同样区段）

**Interfaces:**
- Consumes: Task 2 的 `DecideDatabase`、`CycleUntil`、`RemoveItems`、`Upsert`；Task 4 的 `DeleteReplacements`、`UploadItem`；`MssqlTaskStore.SavePending`、`MysqlTaskStore.SavePending`
- Produces: 截止时间之前不调用 `MssqlConnect.BackupDatabase` / `MysqlConnect.DumpDatabase`。到点或实例/库列表/压缩设置变化时先删未传完的包再整批导出。只丢一个 zip 时只导出那一个库，且不改 `CycleUntilUtc`。

两个 Runner 各自写，不要抽到跨程序集的新类型。流程：

1. 仍调用现有 `ResolveDatabases`（允许列库名）。把库名复制后 `Array.Sort(..., StringComparer.Ordinal)` 再放进 `DatabaseSnapshot.DbNames`。
2. `DecideDatabase(PendingBackup.Parse(task.PendingJson), snapshot, now, File.Exists)`。
3. `Reuse == false` 时：`DeleteReplacements` 失败则抛 `删除未传完的包失败，已中止。` 并且不 `SavePending`。成功则 `RemoveItems`。`ResetCycle == true` 时把记录换成新头：`InstanceId`、排序后的 `DbNames`、`CycleUntil(task.CycleKind, task.CycleInterval, now)`、`Items` 空数组，并立刻 `SavePending`。`ResetCycle == false` 时只保存去掉丢失项之后的记录，不改 `CycleUntilUtc`。
4. 对每个库名：记录里还有同 `Key` 且 `File.Exists(ZipPath)` 的项，则日志 ` 沿用未传完的包 ` + 文件名，不导出、不打包，zip 列表用旧路径。否则才 `PrepareWorkDir`（第一次真正导出时再调用）、导出、`ZipPacker.Pack`、删掉 bak/sql 工作文件。新项 `Key` 为库名，`Fingerprint` 为空，三个 Ok 为 false。`Upsert` 后，若启用了任一外发通道则 `SavePending`。
5. 全部库处理完后，对 `record.Items` 逐个 `UploadItem`（kind：SQL Server 用 `"mssql"`，MySQL 用 `"mysql"`）。`persist` 写回 `SavePending`。
6. 三个通道都未启用：循环结束后 `SavePending(db, task.Id, null)`。
7. `DistChannel.Finish` 与成功后清空 `pending_json` 的规则与 Task 5 相同。`MarkResult` 不改。
8. 全部沿用时不要调用 `PrepareWorkDir`，也不要调用 `BackupDatabase` / `DumpDatabase`。`ZipPacker.Pack` 抛错时 `PendingFiles.TryDeleteZip(zipPath)`，不要把这个半成品写入记录，然后继续抛出。

新项示例：

```csharp
var packed = new PendingItem
{
    Key = dbName,
    ZipPath = zipPath,
    Fingerprint = "",
    ZipLevel = settings.ZipLevel,
    PasswordSha256 = SourceFingerprint.PasswordHash(settings.ZipPassword)
};
PendingBackup.Upsert(record, packed);
```

- [ ] **Step 1: 改 MssqlRunner.ExecuteOne**

按上面的 1–8 步替换打包循环。保留实例不存在、没有用户库、邮件失败补发、`finally` 里的 `MarkResult`。

- [ ] **Step 2: 改 MysqlRunner.ExecuteOne**

与 Step 1 相同，导出方法用 `MysqlConnect.DumpDatabase`，工作文件扩展名仍是 `.sql`，云 kind 为 `"mysql"`，保存用 `MysqlTaskStore.SavePending`。

- [ ] **Step 3: 编译**

Run: `dotnet build src/YxyXb.Mssql/YxyXb.Mssql.csproj`

Expected: 成功

Run: `dotnet build src/YxyXb.MySql/YxyXb.MySql.csproj`

Expected: 成功

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj`

Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/YxyXb.Mssql/MssqlRunner.cs src/YxyXb.MySql/MysqlRunner.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: skip database dump while the previous zip is still uploading"
```

---

### Task 7: 删除任务时清未传完的包

**Files:**
- Modify: `src/YxyXb.Core/Store/FileTaskStore.cs` `Delete`
- Modify: `src/YxyXb.Core/Store/MssqlTaskStore.cs` `Delete`
- Modify: `src/YxyXb.Core/Store/MysqlTaskStore.cs` `Delete`
- Modify: `src/YxyXb.Core/Store/MssqlInstanceStore.cs` `Delete`
- Modify: `src/YxyXb.Core/Store/MysqlInstanceStore.cs` `Delete`
- Modify: `src/YxyXb.Client/Pages/TasksPage.cs` 三处删除方法
- Modify: `src/YxyXb.Client/Pages/InstancesPage.cs` 两处删除方法

**Interfaces:**
- Consumes: `PendingBackup.Parse`，`PendingBackup.NeedsUpload`，`PendingFiles.DeleteReplacements`
- Produces: `Delete` 改为返回 `string`。`null` 表示已删除。非 null 是给界面的原因，数据库行还在。

`PendingFiles.DeleteUnpublished`：

```csharp
public static string DeleteUnpublished(string json, bool doNetwork, bool doFtp, bool doCloud)
{
    var record = PendingBackup.Parse(json);
    if (record == null || record.Items == null)
    {
        return null;
    }

    var doomed = new List<PendingItem>();
    for (var i = 0; i < record.Items.Length; i++)
    {
        if (PendingBackup.NeedsUpload(record.Items[i], doNetwork, doFtp, doCloud))
        {
            doomed.Add(record.Items[i]);
        }
    }

    var log = new StringBuilder();
    if (!DeleteReplacements(doomed.ToArray(), log))
    {
        return "未传完的本地包删不掉，任务未删除。" + log.ToString();
    }

    return null;
}
```

`FileTaskStore.Delete` 改为返回 `string`。任务不存在返回 null。否则调用 `PendingFiles.DeleteUnpublished(task.PendingJson, task.DoNetwork, task.DoFtp, task.DoCloud)`，非 null 则原样返回、不删行。通过后再执行原来的 `DELETE`。MSSQL / MySQL 任务 Store 同样改。不要在四个 Store 里各写一份清理循环。

实例 `Delete`：先 `ListByInstance`。任一任务 `DeleteUnpublished` 返回非 null，则立刻返回该字符串，不删实例、不删任何任务行。全部通过后再执行现有两条 `DELETE`。

界面：确认框文案改为「还没传完的本地包会删除。已经传完的备份包和日志会保留。」删除调用后：

```csharp
var error = FileTaskStore.Delete(_boot.Db, existing.Id);
if (error != null)
{
    MessageBox.Show(Window.GetWindow(this), error, "删除");
    return;
}
```

库任务和两个实例页同样处理。不要在任务编辑对话框的保存路径里删 zip。

- [ ] **Step 1: 实现 Delete 返回值并改五处调用**

按上面改 Store 和两个页面。

- [ ] **Step 2: 增加删除测试**

`PendingBackupTests` 增加：临时目录里建一个 zip，记录里 `CloudOk = false`，`NeedsUpload` 在 `doCloud: true` 时为 true。调用 `PendingFiles.DeleteUnpublished` 后文件不存在，返回 null。再对一个只读文件调用，返回非 null，文件仍在。测试 `finally` 里去掉只读再删目录。

- [ ] **Step 3: 跑测试并编译客户端**

Run: `dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj`

Expected: PASS

Run: `dotnet build src/YxyXb.Client/YxyXb.Client.csproj`

Expected: 成功

- [ ] **Step 4: Commit**

```bash
git add src/YxyXb.Core/Backup/PendingFiles.cs src/YxyXb.Core/Store/FileTaskStore.cs src/YxyXb.Core/Store/MssqlTaskStore.cs src/YxyXb.Core/Store/MysqlTaskStore.cs src/YxyXb.Core/Store/MssqlInstanceStore.cs src/YxyXb.Core/Store/MysqlInstanceStore.cs src/YxyXb.Client/Pages/TasksPage.cs src/YxyXb.Client/Pages/InstancesPage.cs tests/YxyXb.Core.Tests/PendingBackupTests.cs
git -c user.name=yxy -c user.email=yxy@local commit -m "feat: remove unfinished local zips when a backup task is deleted"
```

---

## Spec coverage

| 规格要求 | 任务 |
| --- | --- |
| 路径 + 大小 + 修改时间指纹，密码 SHA256 | Task 1、Task 2 |
| 文件沿用 / 源或压缩设置变化打新包 / 坏 JSON 不删无关文件 | Task 2、Task 5 |
| 库截止时间、实例和库列表、只补丢失的库 | Task 2、Task 6 |
| 已成功通道不再上传 | Task 2 `ChannelsToSend`、Task 4 `UploadItem`、Task 5–6 |
| 删不掉旧包则中止 | Task 2 `AllowPack`、Task 4、Task 5–6 |
| `pending_json` 列，编辑任务不清空 | Task 3 |
| 成功后清空记录，邮件失败只重试邮件 | Task 5–6 只在 `Finish` 返回 true 时清空 |
| 三个通道都关时不写记录 | Task 5–6 `track` |
| 删 zip 成功后丢百度续传，失败只记日志 | Task 4 |
| 删任务 / 删实例 | Task 7 |
| 历史无记录 zip 不自动删 | 没有任何任务按目录扫 zip |
| 不改媒体、不分片算法、保存对话框不删包 | 无对应改动 |
