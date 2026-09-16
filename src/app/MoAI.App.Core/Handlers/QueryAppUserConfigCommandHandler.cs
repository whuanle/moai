using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppUserConfigCommand"/>
/// </summary>
public class QueryAppUserConfigCommandHandler : IRequestHandler<QueryAppUserConfigCommand, QueryAppUserConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppUserConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppUserConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppUserConfigCommandResponse> Handle(QueryAppUserConfigCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var config = await _databaseContext.AppUserConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);

        var lockedSkills = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == request.AppId)
            .Select(x => x.Skills)
            .FirstOrDefaultAsync(cancellationToken);

        return new QueryAppUserConfigCommandResponse
        {
            PromptId = config?.PromptId ?? 0,
            Skills = ParseGuidList(config?.Skills),
            LockedSkills = ParseGuidList(lockedSkills),
        };
    }

    private static IReadOnlyList<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Guid>();
        }

        try
        {
            var raw = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            var result = new List<Guid>();
            foreach (var item in raw)
            {
                if (Guid.TryParse(item, out var id) && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }

            return result;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}
