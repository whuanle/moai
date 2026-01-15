using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Wiki.Plugins.Crawler.Commands;
using MoAI.Wiki.Plugins.Template.Commands;
using MoAI.Wiki.Plugins.Template.Models;
using MoAI.Wiki.Plugins.Template.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins;

/// <summary>
/// 知识库插件管理接口.
/// </summary>
[ApiController]
[Route("/wiki/plugin")]
[EndpointGroupName("wiki")]
public class WikiPluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiPluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public WikiPluginController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 删除插件配置.
    /// </summary>
    /// <param name="req">删除配置命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpDelete("delete_config")]
    public async Task<EmptyCommandResponse> DeleteWikiPluginConfig([FromBody] DeleteWikiPluginConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 启动或取消插件定时任务.
    /// </summary>
    /// <param name="req">操作.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("start_job")]
    public async Task<EmptyCommandResponse> DeleteWikiPluginConfig([FromBody] AddWikiPluginRecuringJobCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询插件的定时任务.
    /// </summary>
    /// <param name="req">操作.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("query_job")]
    public async Task<QueryRecurringJobCommandResponse> DeleteWikiPluginConfig([FromBody] QueryRecurringJobCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryUserIsWikiMemberCommand
            {
                ContextUserId = _userContext.UserId,
                WikiId = wikiId
            },
            ct);

        if (userIsWikiUser.TeamRole == TeamRole.None)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
