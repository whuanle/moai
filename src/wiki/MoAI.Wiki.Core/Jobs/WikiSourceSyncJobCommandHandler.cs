using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Hangfire.Models;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Jobs;

/// <summary>
/// <inheritdoc cref="WikiSourceSyncJobCommand"/>
/// </summary>
public class WikiSourceSyncJobCommandHandler : IRequestHandler<WikiSourceSyncJobCommand, RecuringJobResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly WikiSourceSyncService _syncService;
    private readonly ILogger<WikiSourceSyncJobCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceSyncJobCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="syncService">外部源同步服务.</param>
    /// <param name="logger">日志.</param>
    public WikiSourceSyncJobCommandHandler(
        DatabaseContext databaseContext,
        WikiSourceSyncService syncService,
        ILogger<WikiSourceSyncJobCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _syncService = syncService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RecuringJobResponse> Handle(WikiSourceSyncJobCommand request, CancellationToken cancellationToken)
    {
        var sourceId = request.Params?.SourceId ?? Guid.Empty;
        if (sourceId == Guid.Empty)
        {
            return new RecuringJobResponse { IsCancel = true };
        }

        var source = await _databaseContext.WikiSources
            .FirstOrDefaultAsync(x => x.Id == sourceId, cancellationToken);

        // 外部源已删除或已停用：交由调度器移除该定时任务
        if (source == null || !source.IsEnable)
        {
            return new RecuringJobResponse { IsCancel = true };
        }

        try
        {
            var result = await _syncService.SyncAsync(source, false, cancellationToken);
            _logger.LogInformation(
                "外部源定时同步完成. SourceId={SourceId}, Total={Total}, Created={Created}, Updated={Updated}, Failed={Failed}",
                sourceId,
                result.Total,
                result.Created,
                result.Updated,
                result.Failed);
        }
        catch (System.Exception ex)
        {
            // 单次失败不影响后续调度，错误已记入外部源的同步状态
            _logger.LogWarning(ex, "外部源定时同步失败. SourceId={SourceId}", sourceId);
        }

        return new RecuringJobResponse();
    }
}
