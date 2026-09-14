using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Feishu.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.Feishu.Handlers;

/// <summary>
/// <inheritdoc cref="BindFeishuAppCommand"/>
/// </summary>
public class BindFeishuAppCommandHandler : IRequestHandler<BindFeishuAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindFeishuAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public BindFeishuAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(BindFeishuAppCommand request, CancellationToken cancellationToken)
    {
        var feishuApp = await _databaseContext.FeishuApps
            .FirstOrDefaultAsync(x => x.Id == request.FeishuAppId, cancellationToken);

        if (feishuApp == null)
        {
            throw new BusinessException("飞书应用连接不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(feishuApp.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理飞书应用绑定.") { StatusCode = 403 };
        }

        var channelTeamId = await GetChannelTeamIdAsync(request.ChannelType, request.ChannelId, cancellationToken);

        if (channelTeamId == null)
        {
            throw new BusinessException("绑定的渠道不存在.") { StatusCode = 404 };
        }

        if (channelTeamId.Value != feishuApp.TeamId)
        {
            throw new BusinessException("渠道与飞书应用连接不属于同一团队.") { StatusCode = 403 };
        }

        // 核心约束：同一飞书应用同时只能绑定一个渠道，否则一个事件会被多个渠道重复消费
        var bound = await _databaseContext.FeishuAppBindings
            .AnyAsync(x => x.FeishuAppId == feishuApp.Id, cancellationToken);

        if (bound)
        {
            throw new BusinessException("该飞书应用已绑定到其它渠道，请先解除绑定.") { StatusCode = 409 };
        }

        _databaseContext.FeishuAppBindings.Add(new FeishuAppBindingEntity
        {
            FeishuAppId = feishuApp.Id,
            ChannelType = (int)request.ChannelType,
            ChannelId = request.ChannelId,
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task<int?> GetChannelTeamIdAsync(FeishuChannelType channelType, string channelId, CancellationToken cancellationToken)
    {
        switch (channelType)
        {
            case FeishuChannelType.App:
            {
                if (!Guid.TryParse(channelId, out var appId))
                {
                    throw new BusinessException("渠道 id 不正确，应用渠道需为应用 id.") { StatusCode = 400 };
                }

                var teamId = await _databaseContext.Apps
                    .Where(x => x.Id == appId)
                    .Select(x => (int?)x.TeamId)
                    .FirstOrDefaultAsync(cancellationToken);
                return teamId;
            }

            default:
                throw new BusinessException("不支持的渠道类型.") { StatusCode = 400 };
        }
    }
}
