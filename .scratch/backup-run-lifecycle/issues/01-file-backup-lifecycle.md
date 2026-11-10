# 01: 文件备份接入统一备份运行 lifecycle

**Parent:** [统一备份运行 lifecycle](../../architecture-deepening/issues/01-backup-run-lifecycle.md)

**What to build:** 建立统一备份运行 lifecycle 和聚焦测试工程，并让文件备份从任务开始到最终结果完整走新 module。完成后文件备份用户继续看到相同的包复用、分卷、上传、邮件、删除、日志和失败行为，MSSQL 与 MySQL 仍走旧路径。

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] 新 lifecycle 测试工程已加入 solution，`dotnet build YxyXb.sln` 成功且构建告警没有增加。
- [ ] 单任务执行入口是唯一 lifecycle 测试 seam；任务存储抢占运行锁、`ExecuteDue` 和计划计算仍在 module 外。
- [ ] 文件来源 adapter 只准备来源快照、生成一个或多个备份包并返回运行统计。
- [ ] 文件备份继续支持单 ZIP、分卷备份包、复用未传完的备份、源变化后重新打包和旧 `pending_json`。
- [ ] 逐文件通道进度、最后上传分卷清单、远端校验、邮件、立即删除、结果码和语义日志由统一 lifecycle 完成。
- [ ] 网络、FTP、云和邮件失败继续尝试现有规则中的后续工作；邮件失败记录 `warn`，跳过文件导致数据失败，删除失败保留重试证据。
- [ ] 临时 SQLite + fake 文件源 + fake 通道 + fake 邮件测试覆盖成功、跳过文件、邮件失败、旧单 ZIP、旧未传完的备份和历史分卷记录。
- [ ] MSSQL、MySQL、生产网站数据库和线上数据不进入本 ticket 的修改范围。
