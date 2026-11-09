using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Jobs;

/// <summary>
/// 知识库检索经验聚类周期清理任务：删除超过保留期（默认 30 天）未被任何查询命中（last_recall_time 未更新）的聚类.
/// </summary>
public class WikiRecallClusterCleanupJob
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<WikiRecallClusterCleanupJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiRecallClusterCleanupJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="logger">日志.</param>
    public WikiRecallClusterCleanupJob(DatabaseContext databaseContext, ILogger<WikiRecallClusterCleanupJob> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <summary>
    /// 执行清理（Hangfire 周期触发）.
    /// </summary>
    /// <returns>异步任务.</returns>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task InvokeAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddDays(-WikiRecallClusterDefaults.RetentionDays);
        var removed = await _databaseContext.WikiRecallClusters
            .Where(x => x.LastRecallTime < deadline)
            .ExecuteDeleteAsync(CancellationToken.None);
        if (removed > 0)
        {
            _logger.LogInformation("检索经验聚类周期清理完成，删除 {Count} 条超期聚类（{RetentionDays} 天未命中）.", removed, WikiRecallClusterDefaults.RetentionDays);
        }
    }
}
