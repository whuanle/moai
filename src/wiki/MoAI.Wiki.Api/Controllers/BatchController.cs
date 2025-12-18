using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Batch.Commands;
using MoAI.Wiki.Batch.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 批处理任务.
/// </summary>
[ApiController]
[Route("/wiki/batch")]
public class BatchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public BatchController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 批处理文档.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<EmptyCommandResponse> BatchProcessDocument([FromBody] WikiBatchProcessDocumentCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除或取消批处理文档.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("delete")]
    public async Task<EmptyCommandResponse> DeleteBatchProcessDocument([FromBody] DeleteBatchProcessDocumentCommand req, CancellationToken ct = default)
    {
        await CheckUserIsMemberAsync(req.WikiId, ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询知识库的批处理文档列表.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("query_list")]
    public async Task<QueryWikiBatchProcessDocumentListCommandResponse> QueryWikiBatchProcessDocumentList([FromBody] QueryWikiBatchProcessDocumentListCommand req, CancellationToken ct = default)
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
