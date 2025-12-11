using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.DocumentEmbeddings.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Controllers;

/// <summary>
/// 文档分片相关接口控制器.
/// </summary>
[ApiController]
[Route(ApiPrefix.Document)]
public class EmbeddingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingsController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者接口.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public EmbeddingsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取切割文档分片信息，根据请求参数返回分片数据.
    /// </summary>
    /// <param name="req">查询分片的命令对象，包含 WikiId 和 DocumentId 等参数.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="QueryWikiDocumentTextPartitionCommandResponse"/>，包含分片信息.</returns>
    [HttpPost("get_partition_document")]
    public async Task<QueryWikiDocumentTextPartitionCommandResponse> GetPartitionDocument([FromBody] QueryWikiDocumentTextPartitionCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预览文档切割结果，根据请求参数返回分片预览数据.
    /// </summary>
    /// <param name="req">预览切割的命令对象，包含 WikiId、DocumentId、MaxTokensPerChunk、Overlap 等参数.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="WikiDocumentTextPartitionPreviewCommandResponse"/>，包含预览结果.</returns>
    [HttpPost("text_partition_document")]
    public async Task<WikiDocumentTextPartitionPreviewCommandResponse> PreviewPartitionDocument([FromBody] WikiDocumentTextPartitionPreviewCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 预览文档切割结果，根据请求参数返回分片预览数据.
    /// </summary>
    /// <param name="req">预览切割的命令对象，包含 WikiId、DocumentId、MaxTokensPerChunk、Overlap 等参数.</param>
    /// <param name="ct">取消令牌，可选.</param>
    /// <returns>返回 <see cref="WikiDocumentTextPartitionPreviewCommandResponse"/>，包含预览结果.</returns>
    [HttpPost("update_text_partition_document")]
    public async Task<EmptyCommandResponse> UpdatePartitionDocument([FromBody] UpdateWikiDocumentTextPartitionCommand req, CancellationToken ct = default)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        }, ct);

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}