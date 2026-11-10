using YxyXb.Database;

if (!TryReadOptions(args, out var options))
{
    Console.Error.WriteLine("用法: YxyXb.DbPreflight --db <sqlite-path> [--backup-dir <dir>] [--report <json-path>] [--require-migrated-schema]");
    return 2;
}

var result = PreflightRunner.Run(options);
Console.WriteLine("状态: " + (result.Passed ? "通过" : "失败"));
Console.WriteLine("备份: " + result.BackupPath);
Console.WriteLine("报告: " + result.ReportPath);
Console.WriteLine($"检查: {result.PassedCount} 通过, {result.WarningCount} 警告, {result.FailureCount} 失败");
foreach (var check in result.Checks.Where(x => !string.Equals(x.Status, "pass", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine($"[{check.Status}] {check.Code}: {check.Message}");
}

return result.Passed ? 0 : 1;

static bool TryReadOptions(string[] args, out PreflightOptions options)
{
    options = new PreflightOptions();
    for (var i = 0; i < args.Length; i++)
    {
        var key = args[i];
        if (string.Equals(key, "--require-migrated-schema", StringComparison.OrdinalIgnoreCase))
        {
            options.RequireMigratedSchema = true;
            continue;
        }

        if (i + 1 >= args.Length)
        {
            return false;
        }

        var value = args[++i];
        if (string.Equals(key, "--db", StringComparison.OrdinalIgnoreCase))
        {
            options.DatabasePath = value;
        }
        else if (string.Equals(key, "--backup-dir", StringComparison.OrdinalIgnoreCase))
        {
            options.BackupDirectory = value;
        }
        else if (string.Equals(key, "--report", StringComparison.OrdinalIgnoreCase))
        {
            options.ReportPath = value;
        }
        else
        {
            return false;
        }
    }

    return !string.IsNullOrWhiteSpace(options.DatabasePath);
}
