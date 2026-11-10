# YxyXb 开发指南

## 产品边界

用心云·循备是运行在 Windows 客户电脑上的备份产品：客户端负责配置和人工操作，Windows 服务中的任务引擎负责准点执行。客户的文件、数据库备份包和云凭据留在客户自己的电脑或客户自己的云账号中。

网站和 `YxyXb.Api` 保存账号、订单、授权、设备、开票、佣金和消息等业务数据，不接收客户的业务备份包。

## 仓库结构

- `src/YxyXb.Client`：WPF 客户端，负责页面、人工执行、设备登录和更新入口。
- `src/YxyXb.Engine`：Windows Service 宿主和后台调度循环；计划任务不能依赖客户端窗口。
- `src/YxyXb.Core`：本机 SQLite、任务存储、备份流水线、授权、设备、调度、日志和共享工具。
- `src/YxyXb.Media`：离线介质登记、挂载、拷贝、清理和卷/整盘离线。
- `src/YxyXb.Mssql`、`src/YxyXb.MySql`：数据库备份和恢复。
- `src/YxyXb.Contracts`、`src/YxyXb.Cloud`、`src/YxyXb.{Qiniu,Baidu,Aliyun,TencentCloud,Ctyun}`：DTO、云存储抽象和各云服务商适配器。
- `src/YxyXb.Api`、`src/YxyXb.Web`：ASP.NET Core 8 API 与 Vue 3 / Vite 网站。
- `src/YxyXb.Update`：客户端更新器。

## 关键约束

1. 客户端和任务引擎共用本机 SQLite。开始执行任务前必须通过任务存储或 `TaskRunLock` 抢占运行锁，避免人工执行和计划执行生成两份备份包。
2. 文件备份、数据库备份、云、UNC、FTP 和邮件受 `SiteGate` 授权控制；离线介质任务不受授权和网络限制。
3. 云上传统一通过 `ICloudStorage` / `CloudRuntime`。对象名使用 `CloudObjectKeys` 生成，云端清理只能处理产品前缀下的对象。
4. 对象存储按 ADR-0001 由客户端直接 HTTPS 签名访问，不引入官方 SDK。
5. 持久化和比较时间使用 UTC；只有面向用户的展示和到期提醒转换为 `Asia/Shanghai`。
6. 密码、云密钥和数据库凭据不得写入日志或远程数据库。敏感配置沿用本机加密存储。
7. 未传完的备份只有在源指纹、压缩设置和本地包仍匹配时才复用；否则重新打包，不能静默覆盖证据。

## 常用命令

```powershell
dotnet build YxyXb.sln
dotnet test tests/YxyXb.Api.Tests/YxyXb.Api.Tests.csproj
dotnet test tests/YxyXb.Core.Tests/YxyXb.Core.Tests.csproj

Set-Location src/YxyXb.Web
npm ci
npm test
npm run build
```

客户端、引擎、更新器和多数基础设施项目面向 .NET Framework 4.8；API 面向 .NET 8。Windows 客户端和引擎应在 Windows 上构建验证。

## Agent skills

### Issue tracker

Issues and specs live as local Markdown under `.scratch/<feature>/`, even though the Git remote is GitHub. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the default five role names unchanged in each issue's `Status:` field. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repository: read root `GLOSSARY.md` and relevant ADRs under `docs/adr/`. See `docs/agents/domain.md`.
