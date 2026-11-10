# 01: Schema 基线与现有迁移

**Parent:** [架构深化与存量数据安全](../../architecture-deepening/spec.md)

**What to build:** 建立网站数据库启动 module，记录 schema 版本，并把当前启动时的建表、加列和索引创建迁入有序、幂等实现。首次面对现有生产数据库时只登记基线并完成必要增量，不重写客户、订单、授权或财务历史。

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] 启动入口只调用一个数据库启动 module，不在 Program 中直接拼装表结构变更。
- [ ] 现有生产数据库可识别并登记当前 schema 基线，重复启动不会重复执行已完成的迁移。
- [ ] 新空数据库可完成初始化，并与当前 EF 模型所需表、列和索引一致。
- [ ] 迁移按稳定顺序执行，失败后保留已完成步骤和可诊断报告，不在下一次启动静默跳步。
- [ ] 现有 `Products`、`Orders`、`Customers`、`SubscriptionAuths`、设备和开票字段语义不变。
- [ ] 使用脱敏生产结构和旧版本测试库验证首次升级、重复升级和中断重试。
- [ ] 不删除旧列、旧表或历史产品，不访问生产服务器。

