# 02: MSSQL 备份接入统一备份运行 lifecycle

**Parent:** [统一备份运行 lifecycle](../../architecture-deepening/issues/01-backup-run-lifecycle.md)

**What to build:** 让 MSSQL 备份的多个数据库导出、部分成功、逐项上传和最终收口都进入已由文件备份验证的统一 lifecycle。MSSQL 用户继续通过现有任务入口和运行锁执行，任务状态、重试和可见结果保持兼容。

**Blocked by:** 01 - 文件备份接入统一备份运行 lifecycle

**Status:** ready-for-agent

- [ ] MSSQL 来源 adapter 只负责解析数据库范围、导出数据库、生成一个或多个备份包并返回运行统计。
- [ ] 每个数据库的备份包仍独立记录网络、FTP 和云通道进度，部分失败只补传未完成项。
- [ ] 单 ZIP、分卷备份包和历史 `pending_json` 保持可读，不迁移 MSSQL 任务状态格式。
- [ ] 邮件、立即删除、结果码和日志由统一 lifecycle 完成，不再由 MSSQL runner 复制收口逻辑。
- [ ] MSSQL 现有任务入口、抢占运行锁、下一轮计划和重试间隔语义保持不变。
- [ ] fake MSSQL 来源测试覆盖全部成功、单库导出失败、部分上传失败后重试、邮件失败和删除失败。
- [ ] 文件备份和 MySQL 备份的执行行为在本 ticket 后仍保持兼容。
