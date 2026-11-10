# 04: 清理重复收口路径并完成三来源兼容验收

**Parent:** [统一备份运行 lifecycle](../../architecture-deepening/issues/01-backup-run-lifecycle.md)

**What to build:** 在文件备份、MSSQL 和 MySQL 都已接入统一 lifecycle 后，删除不再调用的旧 runner、状态收口和完成 helper，并用一套跨来源兼容矩阵证明三种备份仍按原业务规则工作。

**Blocked by:** 03 - MySQL 备份接入统一备份运行 lifecycle

**Status:** ready-for-agent

- [ ] 旧文件、MSSQL、MySQL runner 中重复的 lifecycle 编排已删除，单任务 seam 是唯一生产入口。
- [ ] 不再有生产调用方使用旧的第二套完成、待传状态推进或邮件删除 helper。
- [ ] 旧单 ZIP、旧未传完的备份、历史分卷记录和部分通道成功状态仍可继续执行。
- [ ] 三来源兼容矩阵覆盖成功、跳过文件或导出失败、部分上传失败后重试、邮件 `warn`、远端校验失败和本地删除失败。
- [ ] 日志保留结果码和用户可理解的关键消息，但测试不逐字锁定完整日志。
- [ ] lifecycle 测试工程加入 solution 后，solution 构建成功且测试全部通过。
- [ ] 本 ticket 及前序 ticket 均不连接或修改生产网站数据库。
- [ ] 最终验收记录明确文件、MSSQL 和 MySQL 的任务入口、运行锁和结果语义保持兼容。
