using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;
using MoAI.Memory.Services;
using MoAI.Team.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppMemoryStatsCommand"/>：仅团队管理员可查看应用记忆统计.
/// </summary>
public class QueryAppMemoryStatsCommandHandler : IRequestHandler<QueryAppMemoryStatsCommand, QueryAppMemoryStatsResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryService _memoryService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppMemoryStatsCommandHandler"/> class.
    /// </summary>
    public QueryAppMemoryStatsCommandHandler(DatabaseContext databaseContext, IMemoryService memoryService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _memoryService = memoryService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppMemoryStatsResponse> Handle(QueryAppMemoryStatsCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null || myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以查看应用记忆统计.") { StatusCode = 403 };
        }

        var (totalMemories, userCount) = await _memoryService.GetAppStatsAsync(request.AppId, cancellationToken);
        return new QueryAppMemoryStatsResponse { TotalMemories = totalMemories, UserCount = userCount };
    }
}
