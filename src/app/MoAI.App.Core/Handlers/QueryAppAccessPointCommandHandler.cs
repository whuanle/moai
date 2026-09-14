using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Database.Enums;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppAccessPointCommand"/>
/// </summary>
public class QueryAppAccessPointCommandHandler : IRequestHandler<QueryAppAccessPointCommand, AppAccessPointConfigResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppAccessPointCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppAccessPointCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<AppAccessPointConfigResponse> Handle(QueryAppAccessPointCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId, x.IsExternal })
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
            throw new BusinessException("只有团队管理员可以查看访问点配置.") { StatusCode = 403 };
        }

        var config = await _databaseContext.AppAccessPoints
            .FirstOrDefaultAsync(x => x.AppId == request.AppId, cancellationToken);

        // 未保存过时返回默认值，不 404
        return new AppAccessPointConfigResponse
        {
            AppId = request.AppId,
            Title = config?.Title,
            Subtitle = config?.Subtitle,
            Placeholder = config?.Placeholder,
            PrimaryColor = config?.PrimaryColor,
            Position = config?.Position ?? "bottomRight",
            LauncherText = config?.LauncherText,
            Avatar = config?.Avatar,
            PanelWidth = config?.PanelWidth ?? 380,
            PanelHeight = config?.PanelHeight ?? 560,
            DefaultOpen = config?.DefaultOpen ?? false,
            Enabled = config?.Enabled ?? true,
        };
    }
}
