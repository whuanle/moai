using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="PublishAppCommand"/>
/// </summary>
public class PublishAppCommandHandler : IRequestHandler<PublishAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public PublishAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(PublishAppCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("只有团队管理员可以发布应用.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持发布使用.") { StatusCode = 400 };
        }

        // 发布即快照当前配置：正式会话按快照执行，之后的保存只落草稿，直到下次发布
        var config = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        if (config == null)
        {
            // 配置行尚未创建（应用建后未保存过配置）时以默认值建行，保证已发布应用必有快照
            config = new AppAgentConfigEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = app.Id,
                Prompt = string.Empty,
                WikiIds = "[]",
                Plugins = "[]",
                Skills = "[]",
                ExecutionSettings = "{}",
                OpeningStatement = string.Empty,
            };
            _databaseContext.AppAgentConfigs.Add(config);
        }

        config.PublishedConfig = AppAgentConfigSnapshot.Serialize(config);
        config.Status = 1;

        app.PublishStatus = 1;
        app.PublishTime = DateTimeOffset.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
