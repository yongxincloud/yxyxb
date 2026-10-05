# 百度大文件续传与日志保留

**日期：** 2026-10-05  
**状态：** 已落地（feat/baidu-large-upload）  
**方案：** B（续传文件 + 齐片门闩 + 限速友好上传 + 日志保留天数）

## 目标

百度上传的包会超过 1GB。限速后单请求易超时，后半段失败不能整包重来。原则：**片可以小，整文件必须完整上传成功**。顺带在综合设置增加 SQLite 业务日志自动清理天数。

## 已确认

- 失败后 **断点续传**（复用 `uploadid` 与已成功 `partseq`）。
- 续传状态落本机独立文件；`create` 成功后删除。
- 完整性：**分片 MD5** + **齐片门闩**（已成功片数 = 应传片数且无缺口）后才允许 `create`。
- 片长 **1MB**；上传并行可配，**默认 1**，允许 **1–2**。
- 超时按「片大小 / 上限」动态加长；限速尽量按写出字节节流。
- 业务进度写现有任务日志；续传不用日志表恢复。
- 综合设置增加 **日志保留天数**；只清 `logs` 表，不动续传 JSON、备份 zip。

## 不做

- 上传进度条 UI、手动清理续传文件按钮。
- 改百度开放平台应用、改七牛分片算法。
- 用日志表行做续传状态。

## 上传与续传

1. 计算本地绝对路径、`size`、每片 MD5（片长固定 1MB，组成百度 `block_list`）。
2. 若存在匹配续传文件（同路径、同 size、同全部 MD5），复用其 `uploadid` 与已成功 `partseq`；否则 `precreate` 并新建续传文件。
3. 只上传未成功片。并行度读设置 `UploadParallel`（默认 1，夹紧到 1–2）。
4. 每片成功立即把该 `partseq` 写入续传文件。
5. 齐片门闩通过后才 `create`。
6. `create` 成功删除续传文件；失败保留，任务记失败，下次同包可续。
7. 秒传（`return_type=2`）：不传片，直接 `create`；无续传文件或立刻清掉。

### 续传文件

- 目录：`data/baidu-upload/`（在程序 `AppPaths.DataDirectory` 下）。
- 文件名：可用本地路径哈希，避免路径非法字符，例如 `{sha256(localPath)}.json`。
- 字段至少含：`localPath`、`size`、`blockSize`、`blockMd5[]`、`remotePath`、`uploadId`、`donePartSeq[]`、`updatedUtc`。
- 匹配失败（zip 被替换）：废弃旧文件，重新 `precreate`。

### 超时与限速

- HTTP 超时：`max(120s, ceil(blockBytes / maxBytesPerSec) * 2 + 30s)`；`UploadMaxKbps == 0`（不限速）时固定 **600s**。
- `BytePacer`：在写出请求体时按字节节流，避免整片突发占满带宽导致桌面卡顿。
- 单片失败重试 3 次，间隔递增；仍失败则任务失败并保留续传。
- `ICloudStorage` 增加并行度入口（如 `SetParallel(int)`），或由 `CloudRuntimeImpl` 在创建百度实现后设置；七牛可忽略该值。

### 401

续期一次再试当前请求；仍失败提示重新授权，保留续传文件。

## 设置

### 云备份页

- 保留上传下限/上限（KB/s）。
- 新增 **上传并行数**：整数，默认 1，范围 1–2。
- 存 `AppSettingsDto.UploadParallel`（及 settings 表列 / 现有读写路径，与 `UploadMaxKbps` 同级）。

### 综合设置

- 新增 **日志保留天数**：正整数，**最少 1，默认 30**。
- 存 `AppSettingsDto.LogRetainDays`（及 settings 列）。
- 清理入口：挂现有自动删除（`PurgeRunner`）同日调度，或同一次清理流程内增加一步：`DELETE FROM logs WHERE created_utc < 截止时间`。
- **不删除** `data/baidu-upload/*.json`、备份目录 zip、其它业务表。

## 业务日志

任务日志摘要（不写 token / Secret）：

- 开始上传 / 复用续传  
- 单片失败（可带 partseq）  
- 齐片未满禁止 create  
- create 成功或失败  

## 代码落点（预期）

| 区域 | 变化 |
| --- | --- |
| `BaiduSlices` | `BlockSize = 1 * 1024 * 1024`；现有按块 MD5 列表逻辑保持 |
| `BaiduStorage` / `BaiduHttp` | 续传、门闩、动态超时、并行、写出节流 |
| 新小模块 | 续传 JSON 读写（如 `BaiduUploadCheckpoint`） |
| `AppSettingsDto` + `SettingsStore` + schema | `UploadParallel`、`LogRetainDays` |
| `CloudPage` / `SettingsPage` | 并行数、日志保留天数控件 |
| `CloudRuntimeImpl` | 把并行度传给百度存储 |
| `PurgeRunner` 或旁路 | 按 `LogRetainDays` 删过期 `logs` |
| 测试 | 见下 |

## 测试

1. 续传文件：匹配复用 / 不匹配废弃。  
2. 缺一片时门闩拦住 `create`。  
3. 动态超时：有上限时不低于按片推算；上限 0 时有合理下限。  
4. 日志保留：过期行删、未过期保留；不碰续传文件。  
5. 现有百度 Upload / 秒传单测仍通过（片长改为 1MB 后断言同步更新）。

## 验收

- >1GB 本地包在设置了上传上限时，可经多次失败/重启后最终 `create` 成功，网盘文件可下载且大小与本地一致。  
- 并行默认 1 时远程桌面不明显因上传卡死；上限生效时带宽不超过设定。  
- 综合设置日志保留天数保存后，过期日志行被清理，近期日志仍在。
