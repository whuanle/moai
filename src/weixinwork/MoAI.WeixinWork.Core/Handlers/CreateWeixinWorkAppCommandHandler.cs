using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.WeixinWork.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.WeixinWork.Handlers;

/// <summary>
/// <inheritdoc cref="CreateWeixinWorkAppCommand"/>
/// </summary>
public class CreateWeixinWorkAppCommandHandler : IRequestHandler<CreateWeixinWorkAppCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWeixinWorkAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateWeixinWorkAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateWeixinWorkAppCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理企业微信应用连接.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.WeixinWorkApps
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("连接名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        // 一个企业（CorpId）的同一自建应用（AgentId）全局只允许创建一条连接，避免同一应用多处接入互抢回调
        var appExist = await _databaseContext.WeixinWorkApps
            .AnyAsync(x => x.CorpId == request.CorpId && x.AgentId == request.AgentId, cancellationToken);

        if (appExist)
        {
            throw new BusinessException("该企业微信应用已创建过连接.") { StatusCode = 409 };
        }

        var entity = new WeixinWorkAppEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CorpId = request.CorpId,
            CorpSecret = request.CorpSecret,
            AgentId = request.AgentId,
            Token = request.Token,
            EncodingAesKey = request.EncodingAesKey,
            ApiEndpoint = request.ApiEndpoint?.Trim().TrimEnd('/') ?? string.Empty,
            IsDisable = false,
        };

        _databaseContext.WeixinWorkApps.Add(entity);

        // 创建即绑定：应用渠道（独占型）随连接一并落库；新建连接必然没有存量绑定，互斥约束天然满足
        if (request.ChannelType.HasValue)
        {
            if (request.ChannelType.Value != WeixinWorkChannelType.App)
            {
                throw new BusinessException("创建连接仅支持绑定应用渠道.") { StatusCode = 400 };
            }

            if (!Guid.TryParse(request.ChannelId, out var appId))
            {
                throw new BusinessException("渠道 id 不正确，应用渠道需为应用 id.") { StatusCode = 400 };
            }

            var channelTeamId = await _databaseContext.Apps
                .Where(x => x.Id == appId)
                .Select(x => (int?)x.TeamId)
                .FirstOrDefaultAsync(cancellationToken);

            if (channelTeamId == null)
            {
                throw new BusinessException("绑定的渠道不存在.") { StatusCode = 404 };
            }

            if (channelTeamId.Value != entity.TeamId)
            {
                throw new BusinessException("渠道与企业微信应用连接不属于同一团队.") { StatusCode = 403 };
            }

            _databaseContext.WeixinWorkAppBindings.Add(new WeixinWorkAppBindingEntity
            {
                WeixinWorkAppId = entity.Id,
                ChannelType = (int)WeixinWorkChannelType.App,
                ChannelId = request.ChannelId!,
            });
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid
        {
            Value = entity.Id
        };
    }
}
