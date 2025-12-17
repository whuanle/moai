using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Commands;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using MoAI.Wiki.Plugins.Feishu.Commands;
using MoAI.Wiki.Plugins.Template.Commands;
using MoAI.Wiki.Plugins.Template.Models;
using MoAI.Wiki.Plugins.Template.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins;

/// <summary>
/// 爬虫插件相关接口.
/// </summary>
[ApiController]
[Route("/wiki/plugin/crawler")]
[Authorize]
public class CrawlerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlerController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContext">当前请求的用户上下文.</param>
    public CrawlerController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询已经配置的爬虫插件实例列表.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("config_list")]
    public async Task<QueryWikiCrawlerPluginConfigListCommandResponse> ConfigListAsync([FromBody] QueryWikiCrawlerPluginConfigListCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建爬虫实例.
    /// </summary>
    /// <param name="req">创建配置的命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回新配置的标识.</returns>
    [HttpPost("add_config")]
    public async Task<SimpleInt> AddWikiCrawlerConfig([FromBody] AddWikiCrawlerConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改爬虫配置.
    /// </summary>
    /// <param name="req">修改配置的命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回操作结果.</returns>
    [HttpPost("update_config")]
    public async Task<EmptyCommandResponse> UpdateWikiCrawlerConfig([FromBody] UpdateWikiCrawlerConfigCommand req, CancellationToken ct = default)
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
    public async Task<EmptyCommandResponse> StartWikiPluginTask([FromBody] StartWikiCrawlerPluginTaskCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取爬虫配置详细信息.
    /// </summary>
    /// <param name="req">查询配置的命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回配置内容.</returns>
    [HttpGet("config")]
    public async Task<QueryWikiCrawlerConfigCommandResponse> QueryWikiCrawlerConfig([FromQuery] QueryWikiCrawlerConfigCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询爬虫任务的页面状态列表.
    /// </summary>
    /// <param name="req">查询任务状态的命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回任务状态列表.</returns>
    [HttpGet("query_page_state")]
    public async Task<QueryWikiCrawlerPageTasksCommandResponse> QueryWikiCrawlerConfigTaskStateList([FromQuery] QueryWikiCrawlerPageTasksCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckUserIsMemberAsync(int wikiId, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(
            new QueryUserIsWikiUserCommand
            {
                ContextUserId = _userContext.UserId,
                WikiId = wikiId
            },
            ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
