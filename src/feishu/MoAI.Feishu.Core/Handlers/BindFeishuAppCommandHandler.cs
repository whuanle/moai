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

        if (request.ChannelType == FeishuChannelType.App)
        {
            // 独占型渠道：同一飞书应用只能绑定一个团队应用，否则一条消息会被两个应用同时消费
            var appBound = await _databaseContext.FeishuAppBindings
                .AnyAsync(x => x.FeishuAppId == feishuApp.Id && x.ChannelType == (int)FeishuChannelType.App, cancellationToken);

            if (appBound)
            {
                throw new BusinessException("该飞书应用已绑定到其它团队应用，请先解除绑定.") { StatusCode = 409 };
            }
        }
        else
        {
            // 订阅型渠道（知识库外部源等）可一对多：同一飞书应用可同时服务多个外部源，仅禁止重复绑定同一渠道记录
            var duplicate = await _databaseContext.FeishuAppBindings
                .AnyAsync(x => x.FeishuAppId == feishuApp.Id
                    && x.ChannelType == (int)request.ChannelType
                    && x.ChannelId == request.ChannelId, cancellationToken);

            if (duplicate)
            {
                throw new BusinessException("该渠道已绑定此飞书应用.") { StatusCode = 409 };
            }
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

            case FeishuChannelType.WikiSource:
            {
                if (!Guid.TryParse(channelId, out var sourceId))
                {
                    throw new BusinessException("渠道 id 不正确，知识库外部源渠道需为外部源 id.") { StatusCode = 400 };
                }

                var teamId = await _databaseContext.WikiSources
                    .Where(x => x.Id == sourceId)
                    .Select(x => (int?)x.TeamId)
                    .FirstOrDefaultAsync(cancellationToken);
                return teamId;
            }

            default:
                throw new BusinessException("不支持的渠道类型.") { StatusCode = 400 };
        }
    }
}
