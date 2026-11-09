using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Jobs;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="RetryKnowledgeGraphImportJobCommand"/>
/// </summary>
public class RetryKnowledgeGraphImportJobCommandHandler : IRequestHandler<RetryKnowledgeGraphImportJobCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryKnowledgeGraphImportJobCommandHandler"/> class.
    /// </summary>
    public RetryKnowledgeGraphImportJobCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IBackgroundJobClient backgroundJobClient)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RetryKnowledgeGraphImportJobCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var job = await _databaseContext.KgImportJobs
            .FirstOrDefaultAsync(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.Id == request.JobId, cancellationToken)
            ?? throw new BusinessException("导入任务不存在.") { StatusCode = 404 };
        if (job.Status != KnowledgeGraphImportJobStatus.Failed && job.Status != KnowledgeGraphImportJobStatus.Cancelled)
        {
            throw new BusinessException("仅失败或已取消的任务可以重试.") { StatusCode = 409 };
        }

        job.Status = KnowledgeGraphImportJobStatus.Pending;
        job.DoneRows = 0;
        job.TotalRows = 0;
        job.ErrorJson = null;
        job.ResultJson = null;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<KnowledgeGraphAiImportJob>(x => x.RunAsync(job.Id, CancellationToken.None));
        return EmptyCommandResponse.Default;
    }
}

/// <summary>
/// <inheritdoc cref="CancelKnowledgeGraphImportJobCommand"/>
/// </summary>
public class CancelKnowledgeGraphImportJobCommandHandler : IRequestHandler<CancelKnowledgeGraphImportJobCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelKnowledgeGraphImportJobCommandHandler"/> class.
    /// </summary>
    public CancelKnowledgeGraphImportJobCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CancelKnowledgeGraphImportJobCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var job = await _databaseContext.KgImportJobs
            .FirstOrDefaultAsync(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.Id == request.JobId, cancellationToken)
            ?? throw new BusinessException("导入任务不存在.") { StatusCode = 404 };
        if (job.Status == KnowledgeGraphImportJobStatus.Done || job.Status == KnowledgeGraphImportJobStatus.Failed)
        {
            throw new BusinessException("任务已结束，无需取消.") { StatusCode = 409 };
        }

        if (job.Status == KnowledgeGraphImportJobStatus.Cancelled)
        {
            return EmptyCommandResponse.Default;
        }

        // pending 直接取消；执行中在阶段间隙生效（任务自行检测 status 翻转后停止）
        job.Status = KnowledgeGraphImportJobStatus.Cancelled;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
