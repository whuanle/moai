using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 知识库接口.
/// </summary>
[ApiController]
[Route("/wiki")]
public class WikiController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public WikiController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建知识库，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回知识库 <see cref="SimpleLong"/>.</returns>
    [HttpPost]
    public Task<SimpleLong> CreateWiki([FromBody] CreateWikiCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队下的知识库列表，仅团队成员可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikisCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryWikisCommandResponse> QueryWikis([FromQuery] long teamId, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikisCommand { TeamId = teamId }, ct);
    }

    /// <summary>
    /// 查询知识库详情，仅团队成员可访问.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QueryWikiCommandResponse> QueryWiki(long id, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiCommand { WikiId = id }, ct);
    }

    /// <summary>
    /// 更新知识库，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateWiki(long id, [FromBody] UpdateWikiCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiCommand { WikiId = id, Name = req.Name, Description = req.Description };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除知识库，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">知识库 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DeleteWiki(long id, CancellationToken ct)
    {
        return _mediator.Send(new DeleteWikiCommand { WikiId = id }, ct);
    }

    /// <summary>
    /// 查询知识库的文档列表（不含正文），仅团队成员可访问.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentsCommandResponse"/>.</returns>
    [HttpGet("{wikiId}/documents")]
    public Task<QueryWikiDocumentsCommandResponse> QueryWikiDocuments(long wikiId, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiDocumentsCommand { WikiId = wikiId }, ct);
    }

    /// <summary>
    /// 创建文档，全体团队成员可协作.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回文档 <see cref="SimpleLong"/>.</returns>
    [HttpPost("{wikiId}/documents")]
    public async Task<SimpleLong> CreateWikiDocument(long wikiId, [FromBody] CreateWikiDocumentCommand req, CancellationToken ct)
    {
        var cmd = new CreateWikiDocumentCommand { WikiId = wikiId, Title = req.Title, Content = req.Content };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询文档详情（含正文），仅团队成员可访问.
    /// </summary>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentCommandResponse"/>.</returns>
    [HttpGet("document/{documentId}")]
    public Task<QueryWikiDocumentCommandResponse> QueryWikiDocument(long documentId, CancellationToken ct)
    {
        return _mediator.Send(new QueryWikiDocumentCommand { DocumentId = documentId }, ct);
    }

    /// <summary>
    /// 更新文档，全体团队成员可协作.
    /// </summary>
    /// <param name="documentId">文档 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("document/{documentId}")]
    public async Task<EmptyCommandResponse> UpdateWikiDocument(long documentId, [FromBody] UpdateWikiDocumentCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWikiDocumentCommand { DocumentId = documentId, Title = req.Title, Content = req.Content };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除文档，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="documentId">文档 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("document/{documentId}")]
    public Task<EmptyCommandResponse> DeleteWikiDocument(long documentId, CancellationToken ct)
    {
        return _mediator.Send(new DeleteWikiDocumentCommand { DocumentId = documentId }, ct);
    }
}
