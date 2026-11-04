using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// 应用对外功能响应组装：开关 + 该应用的团队资源 key（ACP/A2A 共用一把；开启过功能才有 key）.
/// </summary>
internal static class AppFeatureResponseMapper
{
    /// <summary>
    /// 组装响应：查资源 key 行（可能不存在）.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="app">应用（含开关字段）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回响应模型.</returns>
    public static async Task<AppFeatureResponse> BuildAsync(DatabaseContext databaseContext, Database.Entities.AppEntity app, CancellationToken cancellationToken)
    {
        var response = new AppFeatureResponse
        {
            IsAcpEnabled = app.IsAcpEnabled,
            IsA2aEnabled = app.IsA2aEnabled,
        };

        if (app.IsAcpEnabled || app.IsA2aEnabled)
        {
            var key = await databaseContext.TeamResourceKeys
                .Where(x => x.TeamId == app.TeamId && x.ResourceType == TeamResourceType.App && x.ResourceId == app.Id.ToString())
                .Select(x => new { x.Id, x.Key, x.KeyPrefix, x.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (key != null)
            {
                response.TeamResourceKeyId = key.Id;
                response.Key = key.Key;
                response.KeyPrefix = key.KeyPrefix;
                response.Status = key.Status;
            }
        }

        return response;
    }
}

/// <summary>
/// <inheritdoc cref="UpdateAppFeatureCommand"/>
/// </summary>
public class UpdateAppFeatureCommandHandler : IRequestHandler<UpdateAppFeatureCommand, AppFeatureResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppFeatureCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateAppFeatureCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<AppFeatureResponse> Handle(UpdateAppFeatureCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);
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
            throw new BusinessException("只有团队管理员可以管理应用.") { StatusCode = 403 };
        }

        app.IsAcpEnabled = request.IsAcpEnabled;
        app.IsA2aEnabled = request.IsA2aEnabled;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 开启任一功能即自动生成/复用该应用的团队资源 key（查库无则写、有则复用；一个应用一把，ACP/A2A 共用）
        if (app.IsAcpEnabled || app.IsA2aEnabled)
        {
            await TeamResourceKeyEnsurer.EnsureAsync(_databaseContext, app.TeamId, TeamResourceType.App, app.Id.ToString(), cancellationToken);
        }

        return await AppFeatureResponseMapper.BuildAsync(_databaseContext, app, cancellationToken);
    }
}

/// <summary>
/// <inheritdoc cref="QueryAppFeatureCommand"/>
/// </summary>
public class QueryAppFeatureCommandHandler : IRequestHandler<QueryAppFeatureCommand, AppFeatureResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppFeatureCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppFeatureCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<AppFeatureResponse> Handle(QueryAppFeatureCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);
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
            throw new BusinessException("只有团队管理员可以管理应用.") { StatusCode = 403 };
        }

        return await AppFeatureResponseMapper.BuildAsync(_databaseContext, app, cancellationToken);
    }
}
