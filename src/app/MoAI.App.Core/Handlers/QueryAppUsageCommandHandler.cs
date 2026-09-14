using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppUsageCommand"/>
/// </summary>
public class QueryAppUsageCommandHandler : IRequestHandler<QueryAppUsageCommand, QueryAppUsageCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppUsageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppUsageCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppUsageCommandResponse> Handle(QueryAppUsageCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能查看监控.") { StatusCode = 403 };
        }

        var audits = _databaseContext.AiModelTokenAudits.Where(x =>
            x.TeamId == app.TeamId
            && x.UseType == (int)AiModelUseType.App
            && x.UseResourceId == app.Id);

        var byModel = await audits
            .GroupBy(x => x.ModelId)
            .Select(g => new
            {
                ModelId = g.Key,
                CallCount = g.Sum(x => (long)x.Count),
                PromptTokens = g.Sum(x => (long)x.PromptTokens),
                CompletionTokens = g.Sum(x => (long)x.CompletionTokens),
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
            })
            .ToListAsync(cancellationToken);

        var modelIds = byModel.Select(x => x.ModelId).ToList();
        var names = await _databaseContext.AiModels
            .Where(m => modelIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var byModelItems = byModel
            .OrderByDescending(x => x.TotalTokens)
            .Select(x => new AppUsageModelItem
            {
                ModelId = x.ModelId,
                ModelName = names.TryGetValue(x.ModelId, out var n) ? n : x.ModelId.ToString(),
                CallCount = x.CallCount,
                PromptTokens = x.PromptTokens,
                CompletionTokens = x.CompletionTokens,
                TotalTokens = x.TotalTokens,
            })
            .ToList();

        var summary = new AppUsageSummary
        {
            CallCount = byModel.Sum(x => x.CallCount),
            PromptTokens = byModel.Sum(x => x.PromptTokens),
            CompletionTokens = byModel.Sum(x => x.CompletionTokens),
            TotalTokens = byModel.Sum(x => x.TotalTokens),
        };

        return new QueryAppUsageCommandResponse
        {
            Summary = summary,
            ByModel = byModelItems,
        };
    }
}
