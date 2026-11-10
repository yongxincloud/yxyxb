# 01: 上传恢复与远端校验

**What to build:** 云上传调用一次即可完成远端存在性检查、上传恢复和成功后的远端大小校验，调用者不再自行重复校验。

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] 远端已有同大小对象时不重复上传。
- [x] 上传抛出异常但远端对象已完整时按成功处理。
- [x] 上传成功后校验远端大小，不一致时报告失败。
- [x] 日志 sink 不依赖具体云 adapter 类型。

## Comments

2026-11-10：`ICloudRuntime` 的上传入口已吸收上传恢复和成功后的远端大小校验，调用方不再重复 Verify；日志通过 `ICloudStorage.OnLog` 统一接线。新增 fake storage 测试覆盖上述行为。
