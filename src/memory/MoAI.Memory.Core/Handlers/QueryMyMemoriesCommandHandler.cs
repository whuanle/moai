using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;
using MoAI.Memory.Services;
using MoAI.Team.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMyMemoriesCommand"/>
/// </summary>
public class QueryMyMemoriesCommandHandler : IRequestHandler<QueryMyMemoriesCommand, QueryMyMemoriesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryService _memoryService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMyMemoriesCommandHandler"/> class.
    /// </summary>
    public QueryMyMemoriesCommandHandler(DatabaseContext databaseContext, IMemoryService memoryService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _memoryService = memoryService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryMyMemoriesCommandResponse> Handle(QueryMyMemoriesCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var resolution = await _memoryService.ResolveAsync(request.AppId, request.ContextUserId, cancellationToken);
        var (items, total) = await _memoryService.ListMineAsync(request.AppId, request.ContextUserId, request.Page, request.PageSize, cancellationToken);

        return new QueryMyMemoriesCommandResponse
        {
            Items = items,
            Total = total,
            MemoryEnabled = resolution is { IsEnabled: true },
        };
    }
}
