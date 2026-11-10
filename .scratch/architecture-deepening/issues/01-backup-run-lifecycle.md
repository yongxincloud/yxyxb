# 统一备份运行 lifecycle

Category: refactor  
Status: ready-for-agent  
Type: AFK

## Parent

[架构深化与存量数据安全](../spec.md)

## What to build

建立一个深 module，统一承载一次文件备份、MSSQL 备份或 MySQL 备份的完整运行过程。唯一外部 seam 是任务存储成功抢占运行锁后的单任务执行入口。

文件、MSSQL 和 MySQL 来源 adapter 只负责准备来源快照、生成一个或多个备份包并返回运行统计。统一 module 负责复用或替换未传完的备份、分卷、逐文件通道进度、任务状态持久化、上传与远端校验、邮件通知、立即删除、结果码和语义日志。

保持单 ZIP、分卷备份包、`pending_json` 待传记录和历史未传完的备份全部兼容。先通过 fake 来源和 fake 外部通道完成 module 测试，再依次切换文件备份、MSSQL 和 MySQL。

## Acceptance criteria

- [ ] 单任务执行入口成为唯一 lifecycle 测试 seam；`ExecuteDue`、运行锁和计划计算不进入该 module。
- [ ] 文件、MSSQL、MySQL 来源 adapter 只返回准备结果、备份包和运行统计。
- [ ] 三类任务继续读写各自现有的 `pending_json`，已有未传完的备份无需迁移即可继续。
- [ ] 单 ZIP 和分卷备份包的复用、重打包替换、逐文件上传和清单最后上传保持兼容。
- [ ] 网络、FTP、云、邮件和立即删除的现有可见结果保持不变。
- [ ] 邮件失败仍为通知失败并记录 `warn`，不把数据状态改为失败。
- [ ] 存在跳过文件时仍处理可读文件，但数据结果为失败并保留重试证据。
- [ ] 部分删除失败时不静默成功，并保留可重试证据。
- [ ] 新 lifecycle 测试工程已加入 solution，solution 可以完成构建。
- [ ] 新测试使用临时 SQLite、fake 来源、fake 通道和 fake 邮件，覆盖旧单 ZIP、旧未传完的备份和历史分卷记录。
- [ ] 完成 module 和 fake 来源测试后，按文件备份、MSSQL、MySQL 顺序逐个切换；每一步现有入口保持可用。
- [ ] 第一阶段不连接或修改生产网站数据库。

## Blocked by

None - can start immediately. The implementation must first repair the current solution references to missing test projects.

## Comments

> *This issue was generated from the agreed architecture spec and grilling decisions.*

## Agent Brief

**Category:** refactor  
**Summary:** 用一个深 lifecycle module 统一三类备份的运行、状态和收口语义，同时保持历史数据与现有行为兼容。

**Current behavior:**

文件备份、MSSQL 和 MySQL 各自编排复用、分卷、上传、持久化、邮件、删除和日志。MSSQL 与 MySQL runner 大量重复；文件 runner 又复制完成和统计逻辑。数据库 runner 当前没有任何直接测试。

**Desired behavior:**

任务存储先抢占运行锁，再把单个任务交给统一 lifecycle。来源 adapter 生成备份包和统计；lifecycle 用同一个实现推进所有已启用去向并收口。旧任务记录和备份包不改格式。

**Key interfaces:**

- 单任务执行入口：调用者提供一个已抢占运行锁的任务和运行模式，得到可观察的最终结果。
- 来源 adapter：准备来源、生成备份包、返回备份包和运行统计。
- 任务存储 adapter：读取和保存既有任务、运行锁结果与 `pending_json`。
- 外部通道依赖：网络、FTP、云、邮件和本地包删除由 lifecycle 接受并调用。

**Constraints:**

- 第一轮只做行为保持，不改变业务规则。
- 不迁移、重写或删除线上网站数据。
- 不改变 ADR-0002 的分卷格式。
- 不把日志逐字匹配作为主要测试契约。
- 不在 module 内创建真实网络、进程或云客户端。

**Out of scope:**

- 客户端网站会话、厂商运营、网站数据库启动、云去向和离线介质重构。
- 新增云服务商、云空间下载、增量备份或去重。
- 改变订单、授权、设备或介质业务规则。
- 恢复当前工作树中与 lifecycle 无关的已删除测试和部署文件。
