# 百度网盘分片上传与定时删除

**日期：** 2026-10-04  
**状态：** 待审阅  
**范围：** 桌面客户端「云备份」、任务云上传、引擎定时删除。网站不参与。

## 目标

客户在本机把备份 zip 传到自己的百度网盘（分片、可限速），并按天删除本产品目录下的过期包。客户不申请百度开放平台应用。

## 已确认的决定

- 产品统一申请一个百度开放平台应用；AppKey / AppSecret 打在客户端配置里。
- 客户只做 OAuth：浏览器授权后跳回 `127.0.0.1`，token 加密存在本机 SQLite。
- 任务「云上传」仍只选一个服务商（七牛或百度）。
- 自动删除与七牛并列：独立勾选、独立保留天数（默认 7）、只删本产品前缀。
- 实现放新项目 `Yxy.Baidu`，实现现有 `ICloudStorage`。
- 网盘应用文件夹名：`用心云备份`。完整路径见下文。
- 云备份页沿用左侧服务商列表；选中百度后右侧换成授权区，不填 AK/SK。
- 上传参数与七牛相同：4MB 分片、同时 2 路、共用云备份页的上下限（KB/s）。
- 同一次任务内分片失败重试 3 次；不跨进程续 `uploadid`。
- 腾讯云 / 天翼云继续占位。不改网站。

## 架构

引擎继续只依赖 `ICloudStorage`（测试、限速、上传、按前缀/时间删除）。`DistChannel.Cloud` 已按 `CloudProvider` 调用，不写死七牛类名。

新增 `src/Yxy.Baidu/`（net48，参照 `Yxy.Qiniu`）：

| 单元 | 做什么 | 依赖 |
| --- | --- | --- |
| `BaiduAppConfig` | 读安装目录下的 `BaiduApp.json`（AppKey、AppSecret、RedirectUri、AppFolderName） | 无 |
| `BaiduOAuth` | 拼授权 URL、本机 HttpListener 收回调、code 换 token、refresh | `BaiduHttp`、配置 |
| `BaiduHttp` | TLS 1.2、HTTPS、JSON、错误码 | 无 |
| `BaiduStorage` | `ICloudStorage`：测试、限速、superfile2 上传、列目录删过期 | `BaiduHttp`、账号 DTO |

`Yxy.Cloud` 引用 `Yxy.Baidu`。`CloudStorageFactory` 增加 `baidu`。`Yxy.sln` 加入该项目。客户端和引擎已引用 `Yxy.Cloud`，不必再直接引用 `Yxy.Baidu`。

应用密钥不进 SQLite、不进日志。仓库只提交 `BaiduApp.example.json`，发布时复制为 `BaiduApp.json` 并填真实值。`BaiduApp.json` 加入 gitignore。

## 网盘路径

对象名仍用 `CloudObjectKeys.ForZip` / `ProductPrefix`。百度完整路径：

`/apps/{AppFolderName}/{对象名}`

默认 `{AppFolderName}` 为 `用心云备份`。必须与开放平台给该应用分配的「应用文件夹名」一致；不一致时只改配置，不改代码默认以外的逻辑。

示例：`/apps/用心云备份/用心云备份/file/日报/客户标识_日报_20261004-120000.zip`

客户在网盘里看到的是应用专属目录（常见为「我的应用数据 / 用心云备份」），不是网盘根目录。删除只扫 `/apps/{AppFolderName}/{客户标识}/` 下的文件。

## 账号与就绪条件

`cloud_accounts` 仍按 `provider` 一行。`baidu` 与 `qiniu` 各一行。

`CloudAccountDto` 增加（仅百度行使用）：`AccessToken`、`RefreshToken`、`TokenExpiryUtc`、`DisplayName`。七牛字段不变。

**必须改 `CloudAccountStore.Save`：** 当前只把 Bucket / AccessKey / SecretKey / Zone 写入加密 JSON，百度 token 会被丢掉。按 provider 持久化该行需要的字段；读回时缺字段视为未授权。

就绪判断拆开，禁止继续用「有 Bucket+AK+SK」套在百度上：

- 七牛：`Enabled` 且 Bucket、AccessKey、SecretKey 非空。
- 百度：`Enabled` 且 `RefreshToken` 非空。
- `EnabledProviders()` 返回所有就绪的 provider。任务下拉用这个列表，不再写死 `IsQiniuReady`。

文件 / MSSQL / MySQL 任务对话框当前勾云后把 `CloudProvider` 写成 `"qiniu"`。改为：就绪列表只有一项则用该项；多项则下拉选择；一项都没有则不能勾云。

## 授权（仅客户端）

1. 云备份页点「授权百度」。
2. 用配置里的 **固定** `RedirectUri`（默认 `http://127.0.0.1:17890/baidu/`）启动 `HttpListener`。端口必须与百度开放平台登记完全一致。端口被占用则提示失败，不改用随机端口。
3. 打开系统浏览器：`https://openapi.baidu.com/oauth/2.0/authorize`，`response_type=code`，`scope=basic,netdisk`，`qrcode=1`（登录页带扫码入口），带随机 `state`，回调校验 `state`。
4. 用 code 调 `https://openapi.baidu.com/oauth/2.0/token` 换 `access_token` 与 `refresh_token`，拉用户信息填 `DisplayName`，加密写入 `provider=baidu` 行。
5. 关掉监听。超时、关页、`state` 不符：失败，不覆盖已有 token。
6. 已授权显示脱敏用户名，按钮为「重新授权」。取消「启用」后任务里不再出现百度；token 留在库里直到下次授权覆盖。

引擎不弹浏览器。上传与删除前：若 access 将在 5 分钟内过期或已过期，用 refresh 续期并回写库。refresh 失败则本次失败，提示重新授权。遇到 401 时续期一次再重试，仍失败则失败。

## 上传

`BaiduStorage.Upload`：

1. 本地 zip 不存在或对象名为空则失败。
2. 需要时续期。
3. 按 4MB（最后一片可更短）切分，计算每片 MD5，组成 `block_list`。
4. `precreate`（path、size、isdir=0、autoinit=1、block_list）。若接口判定秒传成功，跳过传片。
5. 否则对 `https://d.pcs.baidu.com/rest/2.0/pcs/superfile2` 上传分片，`partseq` 从 0 计。`Parallel.For` 最大并行 2。限速逻辑放在 `Yxy.Baidu` 内（不引用 `Yxy.Qiniu`），读取设置里的 `UploadMaxKbps`（上限 0 不限速；下限规则与七牛相同：上限为 0 时下限不生效）。单片失败重试 3 次，间隔递增。
6. `create` 成功才算上传成功。中途失败不把半成品当正式文件；不把 `uploadid` 存到跨进程状态。

上传失败则任务失败，且不触发立即删本地。立即删从不删云端对象。日志只写路径与结果，不写 token / Secret。

## 定时删除

`settings` 增加列（`SchemaMigrate.Add`，新建库的 `SchemaSql` 同步加上）：

- `baidu_retain_days INTEGER NOT NULL DEFAULT 7`
- `purge_baidu INTEGER NOT NULL DEFAULT 0`

`AppSettingsDto` / `SettingsStore` / 设置页「自动删除」：在七牛项下增加「删百度网盘」与「百度保留天数」。

`PurgeRunner.ExecuteDue`：

- 入口「是否有任何删除勾选」包含 `PurgeBaidu`。
- 仍每天到点一次（`PurgeTime`，默认 00:30）。
- 勾了百度且站点通道允许时：`older = UtcNow - BaiduRetainDays`，`prefix = CloudObjectKeys.ProductPrefix(SoftwareId)`，调用 `DeleteExpired(db, baidu, prefix, older, onDeleted)`。
- 每个对象删除接口成功：日志「删除调用成功：对象名」。当天汇总：「百度删除调用成功，共 N 个。」失败进当天汇总错误，不写成功行。
- 七牛分支行为不变。

`DeleteExpired`：在 `/apps/{AppFolderName}/{prefix}` 下列文件（分页），用服务端修改时间；没有则从文件名末尾 `yyyyMMdd-HHmmss` 解析（与七牛相同）。早于 `olderThanUtc` 的逐个删除。不删除目录本身、不扫描应用目录以外的网盘文件。

## 界面

云备份页左侧列表增加「百度网盘（本期可用）」，插在七牛与腾讯之间。选中百度时表单可操作：启用、授权状态、授权/重新授权、与七牛共用的上传上下限、测试连接、保存。腾讯/天翼仍禁用。选中七牛时行为与现在一致。

测试连接：账号就绪后拉用户信息或列出应用目录，成功提示「连接成功」。

云备份页标题说明改为：凭据只在本页配置；百度用授权，七牛用空间密钥。

## 错误处理

| 情况 | 行为 |
| --- | --- |
| 缺 `BaiduApp.json` 或 Key 为空 | 授权/测试提示「未配置百度应用」 |
| 监听端口占用 | 提示端口被占用，不改端口 |
| 未授权或未启用 | 不能选百度；已选则上传失败 |
| refresh 失败 | 上传/删除失败，提示重新授权 |
| 分片或 create 失败 | 任务失败，不立即删本地 |
| 删除单个对象失败 | 该次 purge 记错误，继续尝试其余对象（与七牛逐个调用同一策略：当前七牛是遇异常整段失败；百度与七牛保持一致：单次 `DeleteOne` 抛错则中止该提供商本轮删除并记失败） |

## 测试

自动测试不连真百度：

1. `CloudStorageFactory.Create("baidu")` 得到已实现适配器。
2. 路径拼接：`/apps/用心云备份/` + `CloudObjectKeys.ForZip(...)`。
3. `CloudAccountStore` 保存并读回 refresh_token；七牛行保存仍不含百度字段丢失 AK。
4. 4MB 边界的分片数与 MD5 列表长度（可用本地假文件，HTTP 用可注入的测试替身或把切分函数抽成内部可测方法）。
5. 删除过滤：前缀外、未过期的不删。
6. 无 refresh_token 或未启用时百度未就绪；`EnabledProviders` 可同时含 qiniu 与 baidu。
7. `PurgeRunner.IsDue` 现有用例不改；有 `PurgeBaidu` 无其它勾选时到期仍会进入执行（可用设置桩，不打网）。

真机：授权、上传一个小 zip、勾删除且天数为 0 或把对象时间打旧后能删，作为手工验收。

## 非目标

- 网站中转上传或代持 token。
- 同一任务同时传七牛和百度。
- 跨进程断点续传。
- 客户自建百度开放平台应用。
- 写入客户网盘根目录或应用目录以外的路径。
- 实现腾讯云 / 天翼云。
