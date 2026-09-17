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
/// <inheritdoc cref="QueryAppCommand"/>
/// </summary>
public class QueryAppCommandHandler : IRequestHandler<QueryAppCommand, QueryAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppCommandResponse> Handle(QueryAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (app.IsExternal)
        {
            // 外部应用不对普通内部用户暴露；团队 Admin+ 需要进入管理页配置，故放行
            if (myRole != TeamRole.Admin && myRole != TeamRole.Owner)
            {
                throw new BusinessException("应用不存在.") { StatusCode = 404 };
            }
        }
        else
        {
            // 非团队成员仅在「已发布且公开到平台」时可只读查看
            var canViewAsPublic = app.IsPublic && app.PublishStatus == 1 && !app.IsDisable;
            if (myRole == null && !canViewAsPublic)
            {
                throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
            }
        }

        // 开场白随应用详情下发（聊天页成员可读，无需 Admin 权限）；Agent/流程应用均有配置行
        var openingStatement = string.Empty;
        var openingStatementEnabled = false;
        if (app.AppType is (int)AppType.Agent or (int)AppType.Workflow)
        {
            var agentConfig = await _databaseContext.AppAgentConfigs
                .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);
            if (agentConfig != null)
            {
                openingStatement = agentConfig.OpeningStatement;
                openingStatementEnabled = agentConfig.OpeningStatementEnabled;
            }
        }

        return new QueryAppCommandResponse
        {
            AppId = app.Id,
            TeamId = app.TeamId,
            Name = app.Name,
            Description = app.Description,
            AppType = (AppType)app.AppType,
            AvatarPath = app.Avatar,
            IsExternal = app.IsExternal,
            IsAuth = app.IsAuth,
            IsPublic = app.IsPublic,
            PublishStatus = app.PublishStatus,
            PublishTime = app.PublishTime,
            OpeningStatement = openingStatement,
            OpeningStatementEnabled = openingStatementEnabled,
            MyRole = myRole == null ? -1 : (int)myRole.Value,
            CreateTime = app.CreateTime,
            UpdateTime = app.UpdateTime
        };
    }
}
