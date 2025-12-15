using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
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
[Authorize]
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
    /// 启动插件任务.
    /// </summary>
    /// <param name="req">启动任务命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回任务标识.</returns>
    [HttpPost("lanuch_task")]
    public async Task<SimpleGuid> StartWikiPluginTask([FromBody] StartWikiPluginTaskCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 取消插件任务.
    /// </summary>
    /// <param name="req">取消任务命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("cancel_task")]
    public async Task<EmptyCommandResponse> CancalWikiPluginTask([FromBody] CancalWikiPluginTaskCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
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
    /// 查询插件配置列表.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回配置列表.</returns>
    [HttpGet("config_list")]
    public async Task<QueryWikiPluginConfigListCommandResponse> QueryWikiPluginConfigList([FromQuery] QueryWikiPluginConfigListCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = wikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
