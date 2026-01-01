using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using MoAI.Wiki.Plugins.Feishu.Commands;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Feishu.Queries;
using MoAI.Wiki.Plugins.Template.Commands;
using MoAI.Wiki.Plugins.Template.Models;
using MoAI.Wiki.Plugins.Template.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins;

/// <summary>
/// 飞书插件配置接口.
/// </summary>
[ApiController]
[Route("/wiki/plugin/feishu")]
[Authorize]
public class FeishuController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public FeishuController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询已经配置的飞书插件实例列表.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("config_list")]
    public async Task<QueryWikiFeishuPluginConfigListCommandResponse> ConfigListAsync([FromBody] QueryWikiFeishuPluginConfigListCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建飞书配置.
    /// </summary>
    /// <param name="req">创建配置命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回配置 Id.</returns>
    [HttpPost("add_config")]
    public async Task<SimpleInt> AddWikiFeishuConfig([FromBody] AddWikiFeishuConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改飞书配置.
    /// </summary>
    /// <param name="req">修改配置命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("update_config")]
    public async Task<EmptyCommandResponse> UpdateWikiFeishuConfig([FromBody] UpdateWikiFeishuConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 启动插件任务.
    /// </summary>
    /// <param name="req">启动任务命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回任务标识.</returns>
    [HttpPost("lanuch_task")]
    public async Task<EmptyCommandResponse> StartWikiPluginTask([FromBody] StartWikiFeishuPluginTaskCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取飞书配置详情.
    /// </summary>
    /// <param name="req">查询配置命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回配置内容.</returns>
    [HttpGet("config")]
    public async Task<QueryWikiFeishuConfigCommandResponse> QueryWikiFeishuConfig([FromQuery] QueryWikiFeishuConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询飞书爬虫任务状态.
    /// </summary>
    /// <param name="req">查询任务状态命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回任务与页面状态.</returns>
    [HttpGet("query_page_state")]
    public async Task<QueryWikiFeishuPageTasksCommandResponse> QueryWikiFeishuConfigTaskStateList([FromQuery] QueryWikiFeishuPageTasksCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryWikiCreatorCommand
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
