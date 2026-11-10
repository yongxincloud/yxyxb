using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YxyXb.Database;

namespace YxyXb.Api.Data;

public sealed class DatabaseStartupOptions
{
    public string? BackupDirectory { get; set; }
    public string? ReportDirectory { get; set; }
}

public sealed class DatabaseStartupResult
{
    public int Version { get; set; }
    public int? VersionBefore { get; set; }
    public string? BackupPath { get; set; }
    public string? PreflightReportPath { get; set; }
    public string? VerificationReportPath { get; set; }
    public Dictionary<string, long> TableCounts { get; set; } = new();
}

public sealed class DatabaseStartupException : InvalidOperationException
{
    public DatabaseStartupException(
        string stage,
        string? backupPath,
        string? reportPath,
        Exception? innerException = null)
        : base("网站数据库" + stage + "失败，服务已停止启动。", innerException)
    {
        Stage = stage;
        BackupPath = backupPath;
        ReportPath = reportPath;
    }

    public string Stage { get; }
    public string? BackupPath { get; }
    public string? ReportPath { get; }
}

public sealed class DatabaseStartupRunner
{
    private readonly WebDb _db;
    private readonly ILogger<DatabaseStartupRunner> _logger;
    private readonly TimeProvider _time;

    public DatabaseStartupRunner(
        WebDb db,
        ILogger<DatabaseStartupRunner>? logger = null,
        TimeProvider? time = null)
    {
        _db = db;
        _logger = logger ?? NullLogger<DatabaseStartupRunner>.Instance;
        _time = time ?? TimeProvider.System;
    }

    public DatabaseStartupResult Run(DatabaseStartupOptions? options = null)
    {
        options ??= new DatabaseStartupOptions();
        var dataSource = Path.GetFullPath(DataSource(_db));
        var backupDirectory = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? Path.Combine(Path.GetDirectoryName(dataSource) ?? ".", "startup-backups")
            : Path.GetFullPath(options.BackupDirectory);
        var reportDirectory = string.IsNullOrWhiteSpace(options.ReportDirectory)
            ? backupDirectory
            : Path.GetFullPath(options.ReportDirectory);
        Directory.CreateDirectory(backupDirectory);
        Directory.CreateDirectory(reportDirectory);

        var isExistingDatabase = File.Exists(dataSource) && new FileInfo(dataSource).Length > 0;
        var startup = new DatabaseStartupService();
        var versionBefore = isExistingDatabase ? startup.TryReadVersion(_db) : null;
        var needsMigration = !versionBefore.HasValue || versionBefore.Value < 1;
        PreflightResult? before = null;
        if (isExistingDatabase)
        {
            before = PreflightRunner.Run(new PreflightOptions
            {
                DatabasePath = dataSource,
                BackupDirectory = backupDirectory,
                ReportPath = ReportPath(reportDirectory, "startup-preflight"),
                CreateBackup = needsMigration,
                RequireMigratedSchema = !needsMigration
            });
            if (!before.Passed)
            {
                _logger.LogCritical(
                    "网站数据库预检失败，服务已停止启动。失败报告 {ReportPath}。",
                    before.ReportPath);
                throw new DatabaseStartupException(
                    "预检",
                    before.BackupPath,
                    before.ReportPath);
            }
        }

        var completedSteps = new List<string>();

        try
        {
            var version = startup.Initialize(_db, completedSteps.Add);
            PreflightResult? verification = null;
            if (isExistingDatabase && needsMigration)
            {
                verification = PreflightRunner.Run(new PreflightOptions
                {
                    DatabasePath = dataSource,
                    BackupDirectory = backupDirectory,
                    ReportPath = ReportPath(reportDirectory, "startup-verification"),
                    CreateBackup = false,
                    RequireMigratedSchema = true,
                    ExpectedTableCounts = before!.TableCounts,
                    ExpectedSemanticFingerprint = before.SemanticFingerprint
                });
                if (!verification.Passed)
                {
                    _logger.LogCritical(
                        "网站数据库迁移后验证失败，服务已停止启动。验证报告 {ReportPath}。",
                        verification.ReportPath);
                    throw new DatabaseStartupException(
                        "迁移后验证",
                        before.BackupPath,
                        verification.ReportPath);
                }
            }

            var result = new DatabaseStartupResult
            {
                Version = version,
                VersionBefore = versionBefore,
                BackupPath = before?.BackupPath,
                PreflightReportPath = before?.ReportPath,
                VerificationReportPath = verification?.ReportPath,
                TableCounts = verification?.TableCounts ?? before?.TableCounts ?? new()
            };
            _logger.LogInformation(
                "网站数据库启动检查完成。版本 {VersionBefore}->{Version}，预检 {Passed} 项，警告 {Warnings} 项，备份 {BackupPath}。",
                result.VersionBefore,
                result.Version,
                (before?.PassedCount ?? 0) + (verification?.PassedCount ?? 0),
                (before?.WarningCount ?? 0) + (verification?.WarningCount ?? 0),
                result.BackupPath);
            return result;
        }
        catch (DatabaseStartupException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var versionAfter = startup.TryReadVersion(_db);
            var failureReport = WriteFailureReport(
                reportDirectory,
                "迁移",
                versionBefore,
                versionAfter,
                before?.BackupPath,
                before?.ReportPath,
                null,
                completedSteps,
                exception);
            _logger.LogCritical(
                "网站数据库迁移失败。版本 {VersionBefore}->{VersionAfter}，已完成步骤 {Steps}，失败报告 {ReportPath}。",
                versionBefore,
                versionAfter,
                completedSteps.Count,
                failureReport);
            throw new DatabaseStartupException(
                "迁移",
                before?.BackupPath,
                failureReport,
                exception);
        }
    }

    private string WriteFailureReport(
        string reportDirectory,
        string stage,
        int? versionBefore,
        int? versionAfter,
        string? backupPath,
        string? preflightReportPath,
        string? verificationReportPath,
        IReadOnlyList<string> completedSteps,
        Exception exception)
    {
        var reportPath = ReportPath(reportDirectory, "startup-failure");
        var report = new
        {
            GeneratedUtc = _time.GetUtcNow(),
            Stage = stage,
            VersionBefore = versionBefore,
            VersionAfter = versionAfter,
            BackupPath = backupPath,
            PreflightReportPath = preflightReportPath,
            VerificationReportPath = verificationReportPath,
            CompletedSteps = completedSteps,
            FailureType = exception.GetType().Name
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return reportPath;
    }

    private string ReportPath(string directory, string prefix)
    {
        return Path.Combine(
            directory,
            prefix + "-" + _time.GetUtcNow().ToString("yyyyMMdd-HHmmssfff") + ".json");
    }

    private static string DataSource(WebDb db)
    {
        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
            throw new InvalidOperationException("网站数据库连接缺少 Data Source。");
        return dataSource;
    }
}
