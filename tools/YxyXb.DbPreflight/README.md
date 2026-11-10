# YxyXb 网站数据库预检

只读打开 SQLite 源数据库，创建一致备份，并输出不含客户明细的 JSON 报告。源库结构缺失、SQLite 完整性异常、唯一索引异常、逻辑孤儿或关键业务不变量异常时返回退出码 `1`。报告包含业务表行数、主键关系和不可逆业务语义指纹，不包含邮箱、手机号、令牌、密码、云密钥或订单明细。

同一个预检 module 也由 `YxyXb.Api` 启动入口调用。旧数据库需要迁移时，启动会先预检并创建一致备份；迁移完成后再次验证结构、行数和业务语义，任一步失败都会停止 API。

## Windows 开发环境

```powershell
dotnet run --project tools/YxyXb.DbPreflight/YxyXb.DbPreflight.csproj -- `
  --db <sqlite-path> `
  --backup-dir <backup-directory>
```

## Linux 生产环境

服务使用 `/var/lib/yxyxb/yxyxb-api.db` 时，建议由 API 服务账号执行预检：

```bash
sudo install -d -o www-data -g www-data /var/lib/yxyxb/preflight
sudo -u www-data dotnet /opt/yxyxb/tools/YxyXb.DbPreflight.dll \
  --db /var/lib/yxyxb/yxyxb-api.db \
  --backup-dir /var/lib/yxyxb/preflight
```

确认数据库已经迁移后，可增加 `--require-migrated-schema`，要求 `SchemaVersions` 必须存在。

发布 Linux 工具：

```bash
dotnet publish tools/YxyXb.DbPreflight/YxyXb.DbPreflight.csproj \
  -c Release -r linux-x64 --self-contained false \
  -o /opt/yxyxb/tools
```

在 API 正常运行时使用 SQLite 在线备份；备份目录必须与数据库目录分开并保留至少数据库大小 2 倍的空间。工具会先检查可用空间，空间不足时不创建半成品备份。工具不会修复数据库，也不会输出客户明细。退出码 `0` 才能继续迁移，退出码 `1` 时先处理报告中的失败项。

完整上线和回滚步骤见 `docs/operations/database-upgrade.md`。
