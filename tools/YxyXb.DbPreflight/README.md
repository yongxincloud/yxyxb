# YxyXb 网站数据库预检

只读打开 SQLite 源数据库，创建一致备份，并输出不含客户明细的 JSON 报告。源库结构缺失、SQLite 完整性异常、逻辑孤儿或关键业务不变量异常时返回退出码 `1`。

```powershell
dotnet run --project tools/YxyXb.DbPreflight/YxyXb.DbPreflight.csproj -- `
  --db <sqlite-path> `
  --backup-dir <backup-directory>
```

生产执行前确认目标目录有足够空间，并保留生成的备份和报告。工具不会修复数据库，也不会输出邮箱、手机号、令牌、密码或订单明细。
