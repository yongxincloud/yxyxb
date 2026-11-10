# YxyXb 网站数据库预检

只读打开 SQLite 源数据库，创建一致备份，并输出不含客户明细的 JSON 报告。源库结构缺失、SQLite 完整性异常、逻辑孤儿或关键业务不变量异常时返回退出码 `1`。

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

发布 Linux 工具：

```bash
dotnet publish tools/YxyXb.DbPreflight/YxyXb.DbPreflight.csproj \
  -c Release -r linux-x64 --self-contained false \
  -o /opt/yxyxb/tools
```

在 API 正常运行时使用 SQLite 在线备份；备份目录必须与数据库目录分开并保留足够空间。工具不会修复数据库，也不会输出邮箱、手机号、令牌、密码或订单明细。退出码 `0` 才能继续迁移，退出码 `1` 时先处理报告中的失败项。
