using System;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 知识库外部源接口：飞书文档/爬虫等外部数据入口，支持手动同步、定时同步与事件订阅.
/// </summary>
[ApiController]
[Route("/wiki/{wikiId}/sources")]
public class WikiSourceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者，用于回填无请求体命令的操作者.</param>
    public WikiSourceController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 创建外部源，需要团队 Admin 及以上角色；创建后自动绑定飞书渠道并立即拉取一次.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回外部源 <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateSource(long wikiId, [FromBody] CreateWikiSourceCommand req, CancellationToken ct)
    {
        req.SetProperty(x => x.WikiId, wikiId);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询外部源列表，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiSourcesCommandResponse"/>.</returns>
    [HttpGet]
    public Task<QueryWikiSourcesCommandResponse> QuerySources(long wikiId, [FromQuery] QueryWikiSourcesCommand req, CancellationToken ct)
    {
        req.SetProperty(x => x.WikiId, wikiId);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新外部源，需要团队 Admin 及以上角色；未传字段保持不变，cron 传空串表示关闭定时同步.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{sourceId}")]
    public Task<EmptyCommandResponse> UpdateSource(long wikiId, Guid sourceId, [FromBody] UpdateWikiSourceCommand req, CancellationToken ct)
    {
        req.SetProperty(x => x.WikiId, wikiId);
        req.SetProperty(x => x.SourceId, sourceId);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除外部源，需要团队 Admin 及以上角色；同时解绑飞书渠道并移除定时任务，已同步进知识库的文档保留.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{sourceId}")]
    public Task<EmptyCommandResponse> DeleteSource(long wikiId, Guid sourceId, CancellationToken ct)
    {
        // 无请求体的删除命令由 Controller 构造，需显式回填用户上下文（自动填充过滤器只处理入参）
        var req = new DeleteWikiSourceCommand { WikiId = wikiId, SourceId = sourceId };
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 手动同步外部源，需要团队 Admin 及以上角色；逐篇比对内容哈希，仅变化文档才更新并触发工作流.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="req">同步请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SyncWikiSourceCommandResponse"/>.</returns>
    [HttpPost("{sourceId}/sync")]
    public Task<SyncWikiSourceCommandResponse> SyncSource(long wikiId, Guid sourceId, [FromBody] SyncWikiSourceCommand req, CancellationToken ct)
    {
        req.SetProperty(x => x.WikiId, wikiId);
        req.SetProperty(x => x.SourceId, sourceId);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询外部源已同步的文档列表，仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiSourceDocumentsCommandResponse"/>.</returns>
    [HttpPost("{sourceId}/documents")]
    public Task<QueryWikiSourceDocumentsCommandResponse> QuerySourceDocuments(long wikiId, Guid sourceId, [FromBody] QueryWikiSourceDocumentsCommand req, CancellationToken ct)
    {
        req.SetProperty(x => x.WikiId, wikiId);
        req.SetProperty(x => x.SourceId, sourceId);
        return _mediator.Send(req, ct);
    }
}
