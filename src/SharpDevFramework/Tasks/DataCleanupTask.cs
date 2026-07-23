using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpDevLib;

namespace SharpDevFramework;

/// <summary>
/// 数据清理任务配置
/// </summary>
internal class DataCleanupTableConfig
{
    /// <summary>
    /// 表名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 保留天数
    /// </summary>
    public int RetentionDays { get; set; }
}

/// <summary>
/// 数据清理任务
/// </summary>
[TaskRegister(nameof(FrameworkTaskTypes.DataCleanup))]
public class DataCleanupTask(IServiceProvider serviceProvider) : BaseTask(serviceProvider)
{
    /// <summary>
    /// 执行数据清理
    /// </summary>
    protected override async Task ProcessAsync(int id, CancellationToken cancellationToken)
    {
        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var dataCleanupEnabled = configuration.GetValue<bool>("FrameworkSettings:DataCleanup:IsEnabled");
        if (!dataCleanupEnabled) return;

        var tables = configuration.GetSection("FrameworkSettings:DataCleanup:Tables").Get<DataCleanupTableConfig[]>() ?? [];
        foreach (var table in tables)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await CleanupTableAsync(table, cancellationToken);
        }
    }

    /// <summary>
    /// 清理指定表的数据
    /// </summary>
    async Task CleanupTableAsync(DataCleanupTableConfig config, CancellationToken cancellationToken)
    {
        var cutoffTimestamp = DateTime.Now.AddDays(-config.RetentionDays).ToUtcTimestamp();
        var tableName = config.Name.Trim();

        try
        {
            var sqlHelper = new SqlHelper(SqliteFactory.Instance, $"Data Source={SharpFrameworkStatics.DatabasePath}");
            var deletedCount = await sqlHelper.ExecuteNonQueryAsync($"DELETE FROM {tableName} WHERE CreatedAt < {cutoffTimestamp}", cancellationToken: cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("数据清理完成: 表={TableName}, 删除记录数={DeletedCount}", tableName, deletedCount);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error)) _logger.LogError(ex, "数据清理失败: 表={TableName}", tableName);
        }
    }
}
