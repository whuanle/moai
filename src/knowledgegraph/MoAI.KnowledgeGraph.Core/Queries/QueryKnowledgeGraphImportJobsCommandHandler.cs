using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphImportJobsCommand"/>
/// </summary>
public class QueryKnowledgeGraphImportJobsCommandHandler : IRequestHandler<QueryKnowledgeGraphImportJobsCommand, QueryKnowledgeGraphImportJobsCommandResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphImportJobsCommandHandler"/> class.
    /// </summary>
    public QueryKnowledgeGraphImportJobsCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphImportJobsCommandResponse> Handle(QueryKnowledgeGraphImportJobsCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var query = _databaseContext.KgImportJobs.Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId);
        var total = await query.LongCountAsync(cancellationToken);
        var jobs = await query
            .OrderByDescending(x => x.Id)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new QueryKnowledgeGraphImportJobsCommandResponse
        {
            Items = jobs.Select(JobImportJobItemMapper.Map).ToList(),
            Total = total,
        };
    }
}

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphImportJobCommand"/>
/// </summary>
public class QueryKnowledgeGraphImportJobCommandHandler : IRequestHandler<QueryKnowledgeGraphImportJobCommand, KnowledgeGraphImportJobItem>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphImportJobCommandHandler"/> class.
    /// </summary>
    public QueryKnowledgeGraphImportJobCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphImportJobItem> Handle(QueryKnowledgeGraphImportJobCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var job = await _databaseContext.KgImportJobs
            .FirstOrDefaultAsync(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.Id == request.JobId, cancellationToken)
            ?? throw new Infra.Exceptions.BusinessException("导入任务不存在.") { StatusCode = 404 };
        return JobImportJobItemMapper.Map(job);
    }
}

/// <summary>
/// 实体 → 列表项映射（errorJson/resultJson 解析）.
/// </summary>
internal static class JobImportJobItemMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// 映射.
    /// </summary>
    public static KnowledgeGraphImportJobItem Map(KgImportJobEntity job)
    {
        string? error = null;
        if (!string.IsNullOrWhiteSpace(job.ErrorJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(job.ErrorJson);
                error = doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : job.ErrorJson;
            }
            catch (JsonException)
            {
                error = job.ErrorJson;
            }
        }

        ImportKnowledgeGraphFromFileResponse? result = null;
        if (!string.IsNullOrWhiteSpace(job.ResultJson))
        {
            try
            {
                result = JsonSerializer.Deserialize<ImportKnowledgeGraphFromFileResponse>(job.ResultJson, JsonOptions);
            }
            catch (JsonException)
            {
                result = null;
            }
        }

        return new KnowledgeGraphImportJobItem
        {
            JobId = job.Id,
            Type = job.Type,
            FileName = job.FileName,
            Status = job.Status,
            TotalRows = job.TotalRows,
            DoneRows = job.DoneRows,
            Error = error,
            Result = result,
            CreateTime = job.CreateTime,
        };
    }
}
