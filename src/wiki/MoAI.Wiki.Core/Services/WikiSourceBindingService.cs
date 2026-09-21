using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Feishu.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部源与飞书应用连接的绑定服务：封装「选择已有连接 / 新建连接」两种绑定方式，
/// 以及外部源渠道（订阅型，可与团队应用共享同一飞书应用）的绑定与解除.
/// </summary>
[InjectOnScoped]
public class WikiSourceBindingService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceBindingService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="mediator">MediatR 实例，用于复用飞书模块的创建/绑定命令.</param>
    public WikiSourceBindingService(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <summary>
    /// 解析外部源要使用的飞书应用连接 id：优先使用已有连接，否则按 AppID/AppSecret 新建连接.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">操作用户 id.</param>
    /// <param name="userType">操作用户类型.</param>
    /// <param name="feishuAppId">方式一：已有连接 id，为空表示改用方式二.</param>
    /// <param name="newAppName">方式二：新建连接名称.</param>
    /// <param name="newAppId">方式二：飞书开放平台 AppID.</param>
    /// <param name="newAppSecret">方式二：飞书开放平台 AppSecret.</param>
    /// <param name="newAppDomain">方式二：接入域名.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>飞书应用连接 id.</returns>
    public async Task<Guid> ResolveAsync(
        int teamId,
        long userId,
        UserType userType,
        Guid? feishuAppId,
        string? newAppName,
        string? newAppId,
        string? newAppSecret,
        string? newAppDomain,
        CancellationToken cancellationToken)
    {
        if (feishuAppId.HasValue)
        {
            var exists = await _databaseContext.FeishuApps
                .AnyAsync(x => x.Id == feishuAppId.Value && x.TeamId == teamId, cancellationToken);

            if (!exists)
            {
                throw new BusinessException("所选飞书应用连接不存在或不属于该团队.") { StatusCode = 404 };
            }

            return feishuAppId.Value;
        }

        if (string.IsNullOrWhiteSpace(newAppId) || string.IsNullOrWhiteSpace(newAppSecret))
        {
            throw new BusinessException("请选择已有飞书应用连接，或填写飞书开放平台 AppID 与 AppSecret 新建连接.") { StatusCode = 400 };
        }

        var created = await _mediator.Send(new CreateFeishuAppCommand
        {
            TeamId = teamId,
            Name = string.IsNullOrWhiteSpace(newAppName) ? newAppId : newAppName,
            Description = "知识库外部源自动创建",
            AppId = newAppId,
            AppSecret = newAppSecret,
            Domain = string.IsNullOrWhiteSpace(newAppDomain) ? null : newAppDomain,
            ContextUserId = userId,
            ContextUserType = userType,
        }, cancellationToken);

        return created.Value;
    }

    /// <summary>
    /// 建立外部源渠道绑定.
    /// </summary>
    /// <param name="feishuAppId">飞书应用连接 id.</param>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="userId">操作用户 id.</param>
    /// <param name="userType">操作用户类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task BindAsync(Guid feishuAppId, Guid sourceId, long userId, UserType userType, CancellationToken cancellationToken)
    {
        await _mediator.Send(new BindFeishuAppCommand
        {
            FeishuAppId = feishuAppId,
            ChannelType = FeishuChannelType.WikiSource,
            ChannelId = sourceId.ToString("D"),
            ContextUserId = userId,
            ContextUserType = userType,
        }, cancellationToken);
    }

    /// <summary>
    /// 解除外部源渠道绑定；未绑定时静默跳过.
    /// </summary>
    /// <param name="feishuAppId">飞书应用连接 id.</param>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="userId">操作用户 id.</param>
    /// <param name="userType">操作用户类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task UnbindAsync(Guid feishuAppId, Guid sourceId, long userId, UserType userType, CancellationToken cancellationToken)
    {
        var channelId = sourceId.ToString("D");
        var bound = await _databaseContext.FeishuAppBindings
            .AnyAsync(x => x.FeishuAppId == feishuAppId
                && x.ChannelType == (int)FeishuChannelType.WikiSource
                && x.ChannelId == channelId, cancellationToken);

        if (!bound)
        {
            return;
        }

        await _mediator.Send(new UnbindFeishuAppCommand
        {
            FeishuAppId = feishuAppId,
            ChannelType = FeishuChannelType.WikiSource,
            ChannelId = channelId,
            ContextUserId = userId,
            ContextUserType = userType,
        }, cancellationToken);
    }
}
